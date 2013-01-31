namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading;

    public class TcpReader : ITransportReader
    {
        private Socket socket;

        public TcpReader(Socket socket)
        {
            this.socket = socket;
        }

        public IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return socket.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndRead(IAsyncResult ar)
        {
            return socket.EndReceive(ar);
        }
    }
}