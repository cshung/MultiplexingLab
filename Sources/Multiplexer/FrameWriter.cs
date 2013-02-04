namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;

    internal class FrameWriter : IFrameWriter
    {
        private readonly ITransportWriter transportWriter;

        private int writing;
        private ReaderWriterLockSlim writingLock;
        private ConcurrentQueue<List<WriteFrame>> pendingRequests;

        private class WriteState
        {
            public WriteState(FrameWriter thisPtr)
            {
                this.ThisPtr = thisPtr;
                this.WritingSegments = new List<ArraySegment<byte>>();
                this.WritingResults = new List<WriteAsyncResult>();
            }

            internal FrameWriter ThisPtr { get; private set; }
            internal List<ArraySegment<byte>> WritingSegments { get; private set; }
            internal List<WriteAsyncResult> WritingResults { get; private set; }
            internal bool CompletedOneSegment { get; set; }
        }

        internal FrameWriter(ITransportWriter transportWriter)
        {
            this.transportWriter = transportWriter;
            this.pendingRequests = new ConcurrentQueue<List<WriteFrame>>();
            this.writingLock = new ReaderWriterLockSlim();
        }

        public bool BeginWriteFrames(List<WriteFrame> frames)
        {
            bool result = false;
            IAsyncResult ar = null;
            this.writingLock.EnterReadLock();
            if (Interlocked.Exchange(ref this.writing, 1) == 0)
            {
                // No write is in progress - let's write it out
                List<List<WriteFrame>> writingRequests = new List<List<WriteFrame>> { frames };
                ar = this.BeginWriteAllFrames(writingRequests, new WriteState(this));
            }
            else
            {
                this.pendingRequests.Enqueue(frames);
                result = false;
            }
            this.writingLock.ExitReadLock();
            if (ar != null)
            {
                result = ProcessBeginWriteAllFrameAsyncResult(ar);
            }
            return result;
        }

        private IAsyncResult BeginWriteAllFrames(IEnumerable<List<WriteFrame>> writingRequests, WriteState writeState)
        {
            if (this.writing != 1)
            {
                Console.WriteLine("Hit Assertion - I expected this call to be protected");
            }

            foreach (List<WriteFrame> writingRequest in writingRequests)
            {
                foreach (WriteFrame writingFrame in writingRequest)
                {
                    writeState.WritingSegments.Add(writingFrame.Header);
                    writeState.WritingSegments.Add(writingFrame.Payload);
                    writeState.WritingResults.Add(writingFrame.WriteAsyncResult);
                }
            }

            foreach (var segment in writeState.WritingSegments)
            {
                Logger.LogStream("Transport Write", segment);
            }

            IAsyncResult ar = this.transportWriter.BeginWrite(writeState.WritingSegments, OnTransportWriteCompletedCallback, writeState);
            return ar;
        }

        private bool ProcessBeginWriteAllFrameAsyncResult(IAsyncResult ar)
        {
            if (ar.CompletedSynchronously)
            {
                WriteState writeState = (WriteState)ar.AsyncState;
                this.OnTransportWriteCompleted(this.transportWriter.EndWrite(ar), writeState);
                return true;
            }
            else
            {
                return false;
            }
        }

        private void OnTransportWriteCompleted(int byteWritten, WriteState writeState)
        {
            if (this.writing != 1)
            {
                Console.WriteLine("Hit Assertion - I expected this call to be protected");
            }

            int byteCompleted = 0;
            int byteRemaining = byteWritten;
            while (byteCompleted < byteWritten)
            {
                // Pick a frame from the window
                ArraySegment<byte> segment = writeState.WritingSegments[0];
                writeState.WritingSegments.RemoveAt(0);

                // Can I complete this frame?
                if (byteRemaining >= segment.Count)
                {
                    // Complete it
                    byteCompleted += segment.Count;
                    byteRemaining -= segment.Count;
                    if (writeState.CompletedOneSegment)
                    {
                        writeState.CompletedOneSegment = false;
                        WriteAsyncResult completedResult = writeState.WritingResults[0];
                        writeState.WritingResults.RemoveAt(0);
                        completedResult.OnFrameCompleted();
                    }
                    else
                    {
                        writeState.CompletedOneSegment = true;
                    }
                }
                else
                {
                    ArraySegment<byte> incompleteSegment = new ArraySegment<byte>(segment.Array, segment.Offset + byteRemaining, segment.Count - byteRemaining);
                    writeState.WritingSegments.Insert(0, incompleteSegment);
                    byteCompleted = byteWritten;
                    byteRemaining = 0;
                }
            }

            if (this.writing != 1)
            {
                Console.WriteLine("Hit Assertion - I expected this call to be protected");
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

                if (writeState.WritingSegments.Count == 0 && writingRequests.Count == 0)
                {
                    bool shouldReturn = false;
                    this.writingLock.EnterWriteLock();
                    if (this.pendingRequests.Count == 0)
                    {
                        Interlocked.Exchange(ref this.writing, 0);
                        shouldReturn = true;
                    }
                    this.writingLock.ExitWriteLock();
                    if (shouldReturn)
                    {
                        return;
                    }
                }
            } while (writingRequests.Count == 0);
            this.ProcessBeginWriteAllFrameAsyncResult(this.BeginWriteAllFrames(writingRequests, writeState));
        }

        private static void OnTransportWriteCompletedCallback(IAsyncResult ar)
        {
            if (ar.CompletedSynchronously)
            {
                return;
            }
            else
            {
                WriteState writeState = (WriteState)ar.AsyncState;
                FrameWriter thisPtr = writeState.ThisPtr;
                thisPtr.OnTransportWriteCompleted(thisPtr.transportWriter.EndWrite(ar), writeState);
            }
        }

        internal IAsyncResult BeginClose(AsyncCallback callback, object state)
        {
            return new WriteAsyncResult(this, callback, state);
        }

        internal void EndWrite(IAsyncResult ar)
        {
            AsyncResult.End(ar, this, "Write");
        }

        internal void EndClose(IAsyncResult ar)
        {
            AsyncResult.End(ar, this, "Close");
        }
    }
}