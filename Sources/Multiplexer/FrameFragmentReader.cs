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
        private int unconsumedSegmentCount;
        private int bufferStart = 0;

        // Frame decoding state that need to go cross trunks
        private bool lastFramePayloadIncomplete;
        private FrameHeader lastIncompletePayloadHeader;

        private bool lastFrameHeaderIncomplete;
        private List<byte> lastIncompleteHeader;

        private ConcurrentQueue<Receiver> newReceivers;
        private ConcurrentQueue<AcceptAsyncResult> pendingAcceptRequests;
        private ConcurrentDictionary<int, Receiver> receivers;

        private bool shouldClose;

        internal FrameFragmentReader(ITransportReader transportReader)
        {
            this.transportReader = transportReader;
            this.buffer = new byte[Constants.DecodingBufferSize];
            this.receivers = new ConcurrentDictionary<int, Receiver>();
            this.newReceivers = new ConcurrentQueue<Receiver>();
            this.pendingAcceptRequests = new ConcurrentQueue<AcceptAsyncResult>();
            this.BeginFillBuffer();
        }

        public void OnSegmentCompleted()
        {
            // If the reading of network is stopped because buffer is full, and all segnent created are comsumed, then read again
            if (Interlocked.Decrement(ref this.unconsumedSegmentCount) == 0)
            {
                if (bufferFull)
                {
                    this.bufferFull = false;
                    this.bufferStart = 0;
                    this.BeginFillBuffer();
                }
            }
        }

        internal IAsyncResult BeginAccept(AsyncCallback callback, object state)
        {
            AcceptAsyncResult result = new AcceptAsyncResult(this, callback, state);
            Receiver newReceiver;
            if (newReceivers.TryDequeue(out newReceiver))
            {
                result.OnReceiverAvailable(newReceiver, true);
            }
            else
            {
                this.pendingAcceptRequests.Enqueue(result);
            }
            return result;
        }

        internal Receiver EndAccept(IAsyncResult ar)
        {
            return AsyncResult<Receiver>.End(ar, this, "Accept");
        }

        internal Receiver CreateReceiver(int nextStreamId)
        {
            Receiver newReceiver = new Receiver(this, nextStreamId);
            // TODO: Possible to fail at all?
            this.receivers.TryAdd(nextStreamId, newReceiver);
            return newReceiver;
        }

        private void BeginFillBuffer()
        {
            if (!shouldClose)
            {
                IAsyncResult ar = this.transportReader.BeginRead(buffer, this.bufferStart, this.buffer.Length - this.bufferStart, OnTransportReadCallback, this);
                if (ar.CompletedSynchronously)
                {
                    int byteRead = this.transportReader.EndRead(ar);
                    this.OnTransportRead(byteRead);
                }
            }
            else
            {
                this.transportReader.Close();
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
                    while (lastIncompleteHeader.Count < Constants.HeaderLength && sizeRemaining > 0)
                    {
                        lastIncompleteHeader.Add(this.buffer[decodingPointer]);
                        decodingPointer++;
                        sizeRemaining--;
                    }
                    if (lastIncompleteHeader.Count == Constants.HeaderLength)
                    {
                        FrameHeader header = FrameHeader.Decode(this.lastIncompleteHeader);

                        int consumed = DecodePayload(decodingPointer, sizeRemaining, header);
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
                    int consumed = DecodePayload(decodingPointer, sizeRemaining, this.lastIncompletePayloadHeader);
                    decodingPointer += consumed;
                    sizeRemaining -= consumed;
                }
                else
                {
                    // Can I grab the whole header?
                    if (sizeRemaining >= Constants.HeaderLength)
                    {
                        FrameHeader header = FrameHeader.Decode(new ArraySegment<byte>(this.buffer, decodingPointer, Constants.HeaderLength));

                        decodingPointer += Constants.HeaderLength;
                        sizeRemaining -= Constants.HeaderLength;

                        int consumed = DecodePayload(decodingPointer, sizeRemaining, header);

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

                pendingAcceptRequest.OnReceiverAvailable(newReceiver, false);
            }

            if (decodingPointer != buffer.Length)
            {
                this.bufferStart = decodingPointer;
                this.BeginFillBuffer();
            }
            else
            {
                if (this.unconsumedSegmentCount == 0)
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

        private int DecodePayload(int decodingPointer, int sizeRemaining, FrameHeader header)
        {
            byte flag = header.Flag;
            int length = header.Length;
            int streamId = header.StreamId;
            int consumed;

            if (flag == 1)
            {
                this.shouldClose = true;
            }

            // Can I grab the whole payload?
            if (sizeRemaining >= length)
            {
                if (length > 0)
                {
                    ArraySegment<byte> payload = new ArraySegment<byte>(buffer, decodingPointer, length);
                    EnqueuePayload(streamId, payload);
                }

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
                // TODO: When a frame is split - is it always true that duplicating the flag make sense?
                this.lastIncompletePayloadHeader = new FrameHeader(flag, length - sizeRemaining, streamId);

                consumed = sizeRemaining;
            }
            return consumed;
        }

        private void EnqueuePayload(int streamId, ArraySegment<byte> payload)
        {
            if (streamId != 0)
            {
                Receiver receiver;
                if (!this.receivers.TryGetValue(streamId, out receiver))
                {
                    receiver = new Receiver(this, streamId);
                    // TODO, possible to fail at all?
                    this.receivers.TryAdd(streamId, receiver);
                    this.newReceivers.Enqueue(receiver);
                }

                receiver.Enqueue(payload);
                Interlocked.Increment(ref this.unconsumedSegmentCount);
            }
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
    }
}
