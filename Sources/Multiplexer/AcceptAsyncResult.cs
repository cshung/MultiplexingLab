namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading;

    class AcceptAsyncResult : AsyncResult<Receiver>
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