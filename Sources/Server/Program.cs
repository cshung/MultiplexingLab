namespace Multiplexing
{
    using Common;
    using Multiplexer;
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    internal class Program
    {
        private TcpListener listener;

        private static void Main(string[] args)
        {
            new Program().Run();
        }

        private void Run()
        {
            this.listener = new TcpListener(IPAddress.Any, Constants.ServerPort);
            this.listener.Start();
            listener.BeginAcceptTcpClient(OnAccept, this);
            Console.WriteLine("[Server] Listening");
            Console.WriteLine("Press any key to stop");
            Console.ReadLine();
        }

        private static void OnAccept(IAsyncResult ar)
        {
            Program thisPtr = (Program)ar.AsyncState;
            TcpClient client = thisPtr.listener.EndAcceptTcpClient(ar);
            thisPtr.OnAccept(client);
        }

        private void OnAccept(TcpClient client)
        {
            Console.WriteLine("Accepted client request");
            this.listener.BeginAcceptTcpClient(OnAccept, this);
            new ConnectionHandler(client).Run();
        }
    }

    #region Debugger
    internal class DebuggingConnectionHandler
    {
        private NetworkStream stream;
        private byte[] buffer = new byte[10];

        public DebuggingConnectionHandler(TcpClient client)
        {
            this.stream = client.GetStream();
        }

        public void Run()
        {
            Next();
        }

        private void Next()
        {
            this.stream.BeginRead(buffer, 0, 10, OnRead, this);
        }

        private static void OnRead(IAsyncResult ar)
        {
            DebuggingConnectionHandler thisPtr = (DebuggingConnectionHandler)ar.AsyncState;
            try
            {
                int byteRead = thisPtr.stream.EndRead(ar);
                thisPtr.OnRead(byteRead);
            }
            catch
            {
                // Game over on this 'client', but no big deal for the rest
            }

        }

        private int lineNumber;

        private void OnRead(int byteRead)
        {
            if (byteRead != 0)
            {
                for (int i = 0; i < byteRead; i++)
                {
                    Console.WriteLine("{0}\t{1}", ++this.lineNumber, this.buffer[i]);
                }

                this.Next();
            }
        }
    }
    #endregion

    public class ConnectionHandler
    {
        private Connection connection;

        public ConnectionHandler(TcpClient client)
        {
            this.connection = new Connection(client.Client);
            this.connection.BeginAccept(OnAcceptedCallback, this);
        }

        private static void OnAcceptedCallback(IAsyncResult ar)
        {
            Console.WriteLine("Server accepting");
            ConnectionHandler thisPtr = (ConnectionHandler)ar.AsyncState;
            Channel stream = thisPtr.connection.EndAccept(ar);
            thisPtr.connection.BeginAccept(OnAcceptedCallback, thisPtr);
            new StreamHandler(stream);
        }

        internal void Run()
        {
        }
    }

    public class StreamHandler
    {
        private Channel channel;
        private Guid identity;

        public StreamHandler(Channel channel)
        {
            this.identity = Guid.NewGuid();
            this.channel = channel;
            // Stream processing code must not block on stream operation - that will lead to deadlock
            ThreadPool.QueueUserWorkItem(HandleChannel, this);
        }

        private static void HandleChannel(object state)
        {
            StreamHandler thisPtr = (StreamHandler)state;
            thisPtr.HandleChannel();            
        }

        private void HandleChannel()
        {
            using (StreamReader reader = new StreamReader(this.channel))
            {
                using (StreamWriter writer = new StreamWriter(this.channel))
                {
                    writer.WriteLine(reader.ReadLine());
                }
            }
        }
    }
}
