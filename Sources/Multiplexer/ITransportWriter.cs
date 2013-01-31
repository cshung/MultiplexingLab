namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading;

    public interface ITransportWriter
    {
        IAsyncResult BeginWrite(IList<ArraySegment<byte>> buffers, AsyncCallback callback, object state);

        int EndWrite(IAsyncResult ar);
    }
}