namespace Multiplexer
{
    using System;
    using System.Collections.Generic;
    using System.Net.Sockets;

    internal class TcpWriter : ITransportWriter
    {
        private Socket socket;

        internal TcpWriter(Socket socket)
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

        public void Close()
        {
            this.socket.Close();
        }
    }
}