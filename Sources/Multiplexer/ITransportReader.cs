namespace Multiplexer
{
    using System;

    internal interface ITransportReader
    {
        IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state);

        int EndRead(IAsyncResult ar);
    }
}