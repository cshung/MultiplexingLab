namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading;

    class TcpWriter : ITransportWriter
    {
        private Socket socket;

        public TcpWriter(Socket socket)
        {
            this.socket = socket;
        }

        public IAsyncResult BeginWrite(IList<ArraySegment<byte>> buffers, AsyncCallback callback, object state)
        {
            return this.socket.BeginSend(buffers, SocketFlags.None, callback, state);
        }

        public int EndWrite(IAsyncResult ar)
        {
            return this.socket.EndSend(ar);
        }
    }
}