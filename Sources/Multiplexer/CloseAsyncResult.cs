namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Net.Sockets;
    using System.Threading;

    internal class CloseAsyncResult : AsyncResult
    {
        internal CloseAsyncResult(AsyncCallback callback, object state, Connection owner)
            : base(callback, state, owner, "Close")
        {
        }

        internal void Complete()
        {
            base.Complete(null);
        }
    }
}

