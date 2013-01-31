namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;

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

        internal int NonBlockingFillBuffer(byte[] buffer, int offset, int count)
        {
            int sizeRemaining = count;
            int byteCopied = 0;
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
                    this.frameFragmentReader.ArraySegmentCompleted();
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
                        this.frameFragmentReader.ArraySegmentCompleted();
                    }
                }
                else
                {
                    break;
                }
                
            }

            if (byteCopied > 0)
            {
                this.readRequest = null;
            }

            return byteCopied;
        }

        internal void Enqueue(ArraySegment<byte> payload)
        {
            this.dataQueue.Enqueue(payload);
            if (this.readRequest != null)
            {
                this.readRequest.TryComplete(false);
            }
        }        
    }
}