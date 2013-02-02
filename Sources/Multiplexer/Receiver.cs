namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Text;

    internal class Receiver
    {
        private IFrameFragmentReader frameFragmentReader;
        private ConcurrentQueue<ArraySegment<byte>> dataQueue;
        private ArraySegment<byte>? lastBlock;

        private ReadAsyncResult readRequest;

        internal Receiver(IFrameFragmentReader reader, int streamId)
        {
            this.frameFragmentReader = reader;
            this.StreamId = streamId;
            this.dataQueue = new ConcurrentQueue<ArraySegment<byte>>();
        }

        internal int StreamId { get; private set; }

        internal IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            Debug.Assert(this.readRequest == null, "Expected single thread use");
            ReadAsyncResult currentResult = new ReadAsyncResult(this, buffer, offset, count, callback, state);
            this.readRequest = currentResult;
            this.readRequest.TryComplete(true);
            return currentResult;
        }

        internal int EndRead(IAsyncResult ar)
        {
            return AsyncResult<int>.End(ar, this, "Read");
        }

        private readonly object fillLock = new object();

        internal int NonBlockingFillBuffer(byte[] buffer, int offset, int count)
        {
            int numSegmentsCompleted = 0;
            int byteCopied = 0;
            lock (fillLock)
            {   
                int sizeRemaining = count;
                int bufferPointer = offset;
                if (lastBlock.HasValue)
                {
                    int lengthToCopy = lastBlock.Value.Count;
                    bool lastBlockCompleted = false;
                    if (lengthToCopy > count)
                    {
                        lengthToCopy = count;
                    }
                    else
                    {
                        lastBlockCompleted = true;
                    }

                    Buffer.BlockCopy(lastBlock.Value.Array, lastBlock.Value.Offset, buffer, bufferPointer, lengthToCopy);
                    if (lastBlockCompleted)
                    {
                        numSegmentsCompleted++;
                        lastBlock = null;
                    }

                    sizeRemaining -= lengthToCopy;
                    byteCopied += lengthToCopy;
                    bufferPointer += lengthToCopy;
                }

                while (sizeRemaining > 0)
                {
                    ArraySegment<byte> currentBlock;
                    if (this.dataQueue.TryDequeue(out currentBlock))
                    {
                        int lengthToCopy = currentBlock.Count;
                        if (sizeRemaining < currentBlock.Count)
                        {
                            lengthToCopy = sizeRemaining;
                        }

                        Buffer.BlockCopy(currentBlock.Array, currentBlock.Offset, buffer, bufferPointer, lengthToCopy);
                        sizeRemaining -= lengthToCopy;
                        byteCopied += lengthToCopy;
                        bufferPointer += lengthToCopy;

                        if (lengthToCopy < currentBlock.Count)
                        {
                            lastBlock = new ArraySegment<byte>(currentBlock.Array, currentBlock.Offset + lengthToCopy, currentBlock.Count - lengthToCopy);
                        }
                        else
                        {
                            numSegmentsCompleted++;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                if (byteCopied > 0)
                {
                    Logger.Log("NBFB " + this.StreamId + " resetting readRequest to null '");
                    this.readRequest = null;
                }

                // DEBUGGING
                string received = System.Text.Encoding.ASCII.GetString(buffer, offset, byteCopied);
                allReceived.Add(received);

                StringBuilder builder = new StringBuilder();
                Logger.Log("NBFB " + this.StreamId + " received '" + string.Join(string.Empty, allReceived) + "'");
            }

            for (int i = 0; i < numSegmentsCompleted; i++)
            {
                this.frameFragmentReader.OnSegmentCompleted();
            }

            return byteCopied;
        }

        private System.Collections.Generic.List<string> allReceived = new System.Collections.Generic.List<string>();
        private System.Collections.Generic.List<string> allEnqueued = new System.Collections.Generic.List<string>();

        internal void Enqueue(ArraySegment<byte> payload)
        {
            this.dataQueue.Enqueue(payload);

            string enqueued = System.Text.Encoding.ASCII.GetString(payload.Array, payload.Offset, payload.Count);
            allEnqueued.Add(enqueued);

            StringBuilder builder = new StringBuilder();
            Logger.Log("NBFB " + this.StreamId + " enqueued '" + string.Join(string.Empty, allEnqueued) + "'");
            Logger.Log("Enqueue " + this.StreamId + " this.Request is null?" + ((this.readRequest == null) ? "Yes" : "No"));
            if (this.readRequest != null)
            {
                this.readRequest.TryComplete(false);
            }
        }
    }
}