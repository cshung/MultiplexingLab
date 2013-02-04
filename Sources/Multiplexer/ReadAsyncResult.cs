namespace Multiplexer
{
    using System;

    internal class ReadAsyncResult : AsyncResult<int>
    {
        private Receiver receiver;
        private byte[] buffer;
        private int offset;
        private int count;

        private int numSegmentsCompleted;

        internal ReadAsyncResult(Receiver receiver, byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            : base(callback, state, receiver, "Read")
        {
            this.receiver = receiver;
            this.buffer = buffer;
            this.offset = offset;
            this.count = count;
        }

        internal bool CanComplete()
        {
            if (this.IsCompleted)
            {
                return false;
            }
            int bytesRead = this.receiver.NonBlockingFillBuffer(buffer, offset, count, out this.numSegmentsCompleted);
            if (bytesRead > 0)
            {
                this.SetResult(bytesRead);
                Logger.LogStream(string.Format("Stream {0} reported", receiver.StreamId), new ArraySegment<byte>(buffer, offset, bytesRead));
                return true;
            }

            return false;
        }

        internal void Complete(bool isAttemptingSynchronously)
        {
            if (!this.IsCompleted)
            {
                this.receiver.NotifySegmentsCompleted(this.numSegmentsCompleted);
                this.Complete(null, isAttemptingSynchronously);
            }
        }
    }
}