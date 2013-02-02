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
            try
            {
                return socket.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state);
            }
            catch
            {
                return null;
            }
        }

        public int EndRead(IAsyncResult ar)
        {
            try
            {
                return socket.EndReceive(ar);
            }
            catch
            {
                return 0;
            }
        }

        public void Close()
        {
            this.socket.Close();
        }
    }
}