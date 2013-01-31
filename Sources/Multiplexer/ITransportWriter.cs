namespace Multiplexer
{
    using System;
    using System.Collections.Generic;

    internal interface ITransportWriter
    {
        IAsyncResult BeginWrite(IList<ArraySegment<byte>> buffers, AsyncCallback callback, object state);

        int EndWrite(IAsyncResult ar);
    }
}