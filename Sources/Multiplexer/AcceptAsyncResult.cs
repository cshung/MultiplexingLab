namespace Multiplexer
{
    using System;

    internal class AcceptAsyncResult : AsyncResult<Receiver>
    {
        public AcceptAsyncResult(FrameFragmentReader frameFragmentReader, AsyncCallback callback, object state)
            : base(callback, state, frameFragmentReader, "Accept")
        {
        }

        public void ExternalComplete(Receiver receiver, bool completedSynchronously)
        {
            this.SetResult(receiver);
            this.Complete(null, completedSynchronously);
        }
    }
}