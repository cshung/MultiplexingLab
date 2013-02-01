namespace Multiplexer
{
    using System;
    using System.Net.Sockets;

    internal class TcpReader : ITransportReader
    {
        private Socket socket;

        internal TcpReader(Socket socket)
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

        public void Close()
        {
            this.socket.Close();
        }
    }
}