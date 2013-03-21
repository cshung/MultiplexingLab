namespace Connector
{
    using System;

    internal class WriteAsyncResult : AsyncResult
    {
        internal WriteAsyncResult(AsyncCallback callback, object state, object owner)
            : base(callback, state, owner, "Write")
        {
        }

        internal void NotifyCompleted()
        {
            this.Complete(null);
        }
    }
}
