namespace Multiplexer
{
    using System;

    internal class AcceptAsyncResult : AsyncResult<Receiver>
    {
        internal AcceptAsyncResult(FrameFragmentReader frameFragmentReader, AsyncCallback callback, object state)
            : base(callback, state, frameFragmentReader, "Accept")
        {
        }

        internal void OnReceiverAvailable(Receiver receiver, bool completedSynchronously)
        {
            this.SetResult(receiver);
            this.Complete(null, completedSynchronously);
        }
    }
}