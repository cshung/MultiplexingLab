namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading;

    public class FrameWriter : IFrameWriter
    {
        private readonly ITransportWriter transportWriter;

        private int writing;
        private ReaderWriterLockSlim writingLock;
        private ConcurrentQueue<List<WriteFrame>> pendingRequests;

        // This lists are manipulated on the I/O thread and therefore coordinated to have no multi-threaded access
        private List<ArraySegment<byte>> writingSegments;
        private List<WriteAsyncResult> writingResults;
        private bool completedOneSegment;

        public FrameWriter(ITransportWriter transportWriter)
        {
            this.transportWriter = transportWriter;
            this.pendingRequests = new ConcurrentQueue<List<WriteFrame>>();
            this.writingSegments = new List<ArraySegment<byte>>();
            this.writingResults = new List<WriteAsyncResult>();
            this.writingLock = new ReaderWriterLockSlim();
        }

        public bool BeginWriteFrames(List<WriteFrame> frames)
        {
            bool result;
            this.writingLock.EnterReadLock();
            if (Interlocked.Exchange(ref this.writing, 1) == 0)
            {
                // No write is in progress - let's write it out
                List<List<WriteFrame>> writingRequests = new List<List<WriteFrame>> { frames };
                result = this.BeginWriteAllFrames(writingRequests);
            }
            else
            {
                this.pendingRequests.Enqueue(frames);
                result = false;
            }
            this.writingLock.ExitReadLock();
            return result;
        }

        private bool BeginWriteAllFrames(IEnumerable<List<WriteFrame>> writingRequests)
        {
            foreach (List<WriteFrame> writingRequest in writingRequests)
            {
                foreach (WriteFrame writingFrame in writingRequest)
                {
                    this.writingSegments.Add(writingFrame.Header);
                    this.writingSegments.Add(writingFrame.Payload);
                    this.writingResults.Add(writingFrame.WriteAsyncResult);
                }
            }

            IAsyncResult ar = this.transportWriter.BeginWrite(this.writingSegments, OnTransportWriteCompletedCallback, this);

            if (ar.CompletedSynchronously)
            {
                this.OnTransportWriteCompleted(this.transportWriter.EndWrite(ar));
                return true;
            }
            else
            {
                return false;
            }
        }

        private void OnTransportWriteCompleted(int byteWritten)
        {
            int byteCompleted = 0;
            int byteRemaining = byteWritten;
            while (byteCompleted < byteWritten)
            {
                // Pick a frame from the window
                ArraySegment<byte> segment = this.writingSegments[0];
                this.writingSegments.RemoveAt(0);

                // Can I complete this frame?
                if (byteRemaining >= segment.Count)
                {
                    // Complete it
                    byteCompleted += segment.Count;
                    byteRemaining -= segment.Count;
                    if (this.completedOneSegment)
                    {
                        this.completedOneSegment = false;
                        WriteAsyncResult completedResult = this.writingResults[0];
                        this.writingResults.RemoveAt(0);
                        completedResult.CompleteFrame();
                    }
                    else
                    {
                        this.completedOneSegment = true;
                    }
                }
                else
                {
                    ArraySegment<byte> incompleteSegment = new ArraySegment<byte>(segment.Array, segment.Offset + byteRemaining, segment.Count - byteRemaining);
                    this.writingSegments.Insert(0, incompleteSegment);
                    byteCompleted = byteWritten;
                    byteRemaining = 0;
                }
            }

            // drain this.pendingRequest and continue to write if any.
            // The synchronization here is tricky.
            // First - drain this.pendingRequest using the ConcurrentQueue - it is likely there is pending requests
            // Then  - if it turns out it 'SEEMS' there is nothing to do - enter write lock - that acts to wait until all callers that is holding the read lock to go away
            //       - the check within the write lock is real, we can safely reset the writing flag and make no callbacks.
            //       - but if the check within the write lock tell you there were user in the block, then we need to redrain the queue again, just to make sure writingRequests is non-empty.
            List<List<WriteFrame>> writingRequests = new List<List<WriteFrame>>();
            List<WriteFrame> writingRequest;
            do
            {
                while (this.pendingRequests.TryDequeue(out writingRequest))
                {
                    writingRequests.Add(writingRequest);
                }

                if (this.writingSegments.Count == 0 && writingRequests.Count == 0)
                {
                    this.writingLock.EnterWriteLock();
                    if (this.pendingRequests.Count == 0)
                    {
                        this.writing = 0;
                    }
                    this.writingLock.ExitWriteLock();
                    if (this.writing == 0)
                    {
                        return;
                    }
                }
            } while (writingRequests.Count == 0);
            this.BeginWriteAllFrames(writingRequests);
        }

        private static void OnTransportWriteCompletedCallback(IAsyncResult ar)
        {
            if (ar.CompletedSynchronously)
            {
                return;
            }
            else
            {
                FrameWriter thisPtr = (FrameWriter)ar.AsyncState;
                thisPtr.OnTransportWriteCompleted(thisPtr.transportWriter.EndWrite(ar));
            }
        }
    }
}