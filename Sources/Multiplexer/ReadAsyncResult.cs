namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;

    internal class ReadAsyncResult : AsyncResult<int>
    {
        private Receiver receiver;
        private byte[] buffer;
        private int offset;
        private int count;

        internal ReadAsyncResult(Receiver receiver, byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            : base(callback, state, receiver, "Read")
        {
            this.receiver = receiver;
            this.buffer = buffer;
            this.offset = offset;
            this.count = count;
        }

        internal void TryComplete(bool isAttemptingSynchronously)
        {
            int bytesRead = this.receiver.NonBlockingFillBuffer(buffer, offset, count);
            if (bytesRead > 0)
            {
                this.SetResult(bytesRead);
                this.Complete(null, isAttemptingSynchronously);
            }
        }
    }
}