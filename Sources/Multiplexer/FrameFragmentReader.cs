namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;

    internal class FrameFragmentReader : IFrameFragmentReader
    {
        private ITransportReader transportReader;
        private byte[] buffer;
        private bool bufferFull;
        private int segmentCount;
        private int bufferStart = 0;

        // Frame decoding state that need to go cross trunks
        private bool lastFramePayloadIncomplete;
        private int lastIncompletePayloadStreamId;
        private int lastIncompletePayloadRemainingSize;

        private bool lastFrameHeaderIncomplete;
        private List<byte> lastIncompleteHeader;

        private ConcurrentQueue<Receiver> newReceivers;
        private ConcurrentQueue<AcceptAsyncResult> pendingAcceptRequests;
        private Dictionary<int, Receiver> receivers;

        public FrameFragmentReader(ITransportReader transportReader)
        {
            this.transportReader = transportReader;
            this.buffer = new byte[Constants.DecodingBufferSize];
            this.receivers = new Dictionary<int, Receiver>();
            this.newReceivers = new ConcurrentQueue<Receiver>();
            this.pendingAcceptRequests = new ConcurrentQueue<AcceptAsyncResult>();
            this.BeginFillBuffer();
        }

        public IAsyncResult BeginAccept(AsyncCallback callback, object state)
        {
            AcceptAsyncResult result = new AcceptAsyncResult(this, callback, state);
            Receiver newReceiver;
            if (newReceivers.TryDequeue(out newReceiver))
            {
                result.ExternalComplete(newReceiver, true);
            }
            else
            {
                this.pendingAcceptRequests.Enqueue(result);
            }
            return result;
        }

        public Receiver EndAccept(IAsyncResult ar)
        {
            return AsyncResult<Receiver>.End(ar, this, "Accept");
        }

        private void BeginFillBuffer()
        {
            IAsyncResult ar = this.transportReader.BeginRead(buffer, this.bufferStart, this.buffer.Length - this.bufferStart, OnTransportReadCallback, this);
            if (ar.CompletedSynchronously)
            {
                int byteRead = this.transportReader.EndRead(ar);
                this.OnTransportRead(byteRead);
            }
        }

        private void OnTransportRead(int byteRead)
        {
            int decodingBufferStart = this.bufferStart;
            int decodingBufferSize = byteRead;

            // Decoding the buffer here to make sure the decoding process is not run concurrently
            int decodingPointer = decodingBufferStart;
            int sizeRemaining = decodingBufferSize;

            while (sizeRemaining > 0)
            {
                if (this.lastFrameHeaderIncomplete)
                {
                    this.lastFrameHeaderIncomplete = false;
                    while (lastIncompleteHeader.Count < 8 && sizeRemaining > 0)
                    {
                        lastIncompleteHeader.Add(this.buffer[decodingPointer]);
                        decodingPointer++;
                        sizeRemaining--;
                    }
                    if (lastIncompleteHeader.Count == 8)
                    {

                        // TODO: Remove this duplicated code
                        int lengthA = this.lastIncompleteHeader[0];
                        int lengthB = this.lastIncompleteHeader[1];
                        int lengthC = this.lastIncompleteHeader[2];
                        int lengthD = this.lastIncompleteHeader[3];
                        int length = lengthA * 0x1000 + lengthB * 0x0100 + lengthC * 0x0010 + lengthD * 0x0001;

                        int streamA = this.lastIncompleteHeader[4];
                        int streamB = this.lastIncompleteHeader[5];
                        int streamC = this.lastIncompleteHeader[6];
                        int streamD = this.lastIncompleteHeader[7];
                        int streamId = streamA * 0x1000 + streamB * 0x0100 + streamC * 0x0010 + streamD * 0x0001;

                        int consumed = DecodePayload(decodingPointer, sizeRemaining, length, streamId);
                        decodingPointer += consumed;
                        sizeRemaining -= consumed;
                    }
                    else
                    {
                        this.lastFrameHeaderIncomplete = true;
                    }
                }
                else if (this.lastFramePayloadIncomplete)
                {
                    this.lastFramePayloadIncomplete = false;
                    int consumed = DecodePayload(decodingPointer, sizeRemaining, this.lastIncompletePayloadRemainingSize, this.lastIncompletePayloadStreamId);
                    decodingPointer += consumed;
                    sizeRemaining -= consumed;
                }
                else
                {
                    // Can I grab the whole header?
                    if (sizeRemaining >= 8)
                    {
                        int lengthA = this.buffer[decodingPointer + 0];
                        int lengthB = this.buffer[decodingPointer + 1];
                        int lengthC = this.buffer[decodingPointer + 2];
                        int lengthD = this.buffer[decodingPointer + 3];
                        int length = lengthA * 0x1000 + lengthB * 0x0100 + lengthC * 0x0010 + lengthD * 0x0001;

                        int streamA = this.buffer[decodingPointer + 4];
                        int streamB = this.buffer[decodingPointer + 5];
                        int streamC = this.buffer[decodingPointer + 6];
                        int streamD = this.buffer[decodingPointer + 7];
                        int streamId = streamA * 0x1000 + streamB * 0x0100 + streamC * 0x0010 + streamD * 0x0001;

                        decodingPointer += 8;
                        sizeRemaining -= 8;

                        int consumed = DecodePayload(decodingPointer, sizeRemaining, length, streamId);

                        decodingPointer += consumed;
                        sizeRemaining -= consumed;
                    }
                    else
                    {
                        this.lastFrameHeaderIncomplete = true;
                        lastIncompleteHeader = new List<byte>();
                        while (sizeRemaining > 0)
                        {
                            lastIncompleteHeader.Add(this.buffer[decodingPointer]);
                            decodingPointer++;
                            sizeRemaining--;
                        }
                    }
                }
            }

            // Done Parsing: Notify any pending accepts
            while (pendingAcceptRequests.Count > 0 && this.newReceivers.Count > 0)
            {
                AcceptAsyncResult pendingAcceptRequest;
                Receiver newReceiver;

                bool dequeueResult = pendingAcceptRequests.TryDequeue(out pendingAcceptRequest);
                Debug.Assert(dequeueResult, "There is only ONE thread doing dequeue, we already checked the queue has element in it");

                dequeueResult = newReceivers.TryDequeue(out newReceiver);
                Debug.Assert(dequeueResult, "There is only ONE thread doing dequeue, we already checked the queue has element in it");

                pendingAcceptRequest.ExternalComplete(newReceiver, false);
            }

            if (decodingPointer != buffer.Length)
            {
                this.bufferStart = decodingPointer;
                this.BeginFillBuffer();
            }
            else
            {
                if (this.segmentCount == 0)
                {
                    this.bufferStart = 0;
                    this.BeginFillBuffer();
                }
                else
                {
                    this.bufferFull = true;
                }
            }
        }

        private int DecodePayload(int decodingPointer, int sizeRemaining, int length, int streamId)
        {
            int consumed;

            // Can I grab the whole payload?
            if (sizeRemaining >= length)
            {
                ArraySegment<byte> payload = new ArraySegment<byte>(buffer, decodingPointer, length);
                EnqueuePayload(streamId, payload);

                consumed = length;
            }
            else
            {
                if (sizeRemaining > 0)
                {
                    ArraySegment<byte> payload = new ArraySegment<byte>(buffer, decodingPointer, sizeRemaining);
                    EnqueuePayload(streamId, payload);
                }
                this.lastFramePayloadIncomplete = true;
                this.lastIncompletePayloadStreamId = streamId;
                this.lastIncompletePayloadRemainingSize = length - sizeRemaining;

                consumed = sizeRemaining;
            }
            return consumed;
        }

        private void EnqueuePayload(int streamId, ArraySegment<byte> payload)
        {
            Receiver receiver;
            if (!this.receivers.TryGetValue(streamId, out receiver))
            {
                receiver = new Receiver(this, streamId);
                this.receivers.Add(streamId, receiver);
                this.newReceivers.Enqueue(receiver);
            }

            receiver.Enqueue(payload);
            Interlocked.Increment(ref this.segmentCount);
        }

        private static void OnTransportReadCallback(IAsyncResult ar)
        {
            if (ar.CompletedSynchronously)
            {
                return;
            }

            FrameFragmentReader thisPtr = (FrameFragmentReader)ar.AsyncState;
            int byteRead = thisPtr.transportReader.EndRead(ar);
            thisPtr.OnTransportRead(byteRead);
        }

        public void ArraySegmentCompleted()
        {
            // If the reading of network is stopped because buffer is full, and all segnent created are comsumed, then read again
            if (Interlocked.Decrement(ref this.segmentCount) == 0)
            {
                if (bufferFull)
                {
                    this.bufferFull = false;
                    this.bufferStart = 0;
                    this.BeginFillBuffer();
                }
            }
        }
    }
}
