namespace Multiplexing
{
    using Common;
    using Multiplexer;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    internal class Program
    {
        private TcpListener listener;
        private List<ConnectionHandler> connectionHandlers = new List<ConnectionHandler>();

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
            while (true)
            {
                Console.ReadLine();
                Console.Clear();
                Logger.Dump();
                Logger.Clean();
            }
        }

        private static void OnAccept(IAsyncResult ar)
        {
            Program thisPtr = (Program)ar.AsyncState;
            TcpClient client = thisPtr.listener.EndAcceptTcpClient(ar);
            Logger.Dump();
            Logger.Clean();
            thisPtr.OnAccept(client);
        }

        private void OnAccept(TcpClient client)
        {
            Logger.Log("[Server] Accepting TCP connection");
            this.listener.BeginAcceptTcpClient(OnAccept, this);
            this.connectionHandlers.Add(new ConnectionHandler(client));
            //new DebuggingConnectionHandler(client);
        }
    }

    #region Debugger
    internal class DebuggingConnectionHandler
    {
        private NetworkStream stream;
        private byte[] buffer = new byte[10];
        private int lineNumber;

        public DebuggingConnectionHandler(TcpClient client)
        {
            this.stream = client.GetStream();
            this.Next();
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
        private List<ChannelHandler> channelHandlers = new List<ChannelHandler>();

        public ConnectionHandler(TcpClient client)
        {
            this.connection = new Connection(client.Client);
            this.connection.BeginAccept(OnAcceptedCallback, this);
        }

        private static void OnAcceptedCallback(IAsyncResult ar)
        {
            Logger.Log("[Server] Accepting Channel");
            ConnectionHandler thisPtr = (ConnectionHandler)ar.AsyncState;
            Channel stream = thisPtr.connection.EndAccept(ar);
            thisPtr.OnAccepted(stream);
        }

        private void OnAccepted(Channel stream)
        {
            this.connection.BeginAccept(OnAcceptedCallback, this);
            this.channelHandlers.Add(new ChannelHandler(stream));
        }
    }

    public class ChannelHandler
    {
        private static Random random = new Random();
        private Guid identity;
        private StreamReader reader;
        private StreamWriter writer;
        private Timer timer;

        public ChannelHandler(Channel channel)
        {
            this.identity = Guid.NewGuid();
            this.reader = new StreamReader(channel);
            this.writer = new StreamWriter(channel);
            this.HandleStream();
        }

        private void HandleStream()
        {
            reader.ReadLineAsync().ContinueWith(OnReadLineCallback);
        }

        private void OnReadLineCallback(Task<string> task)
        {
            string line = task.Result;
            this.timer = new Timer(OnTimeUp, line, random.Next(5, 200), Timeout.Infinite);
        }

        private void OnTimeUp(object state)
        {
            this.timer = null;
            string line = (string)state;
            this.writer.WriteLineAsync(line).ContinueWith(OnWriteLineCallback);
        }

        private void OnWriteLineCallback(Task task)
        {
            task.Wait();
            writer.FlushAsync().ContinueWith(OnFlushCallback);
        }

        private void OnFlushCallback(Task t)
        {
            t.Wait();
            writer.Dispose();
            reader.Dispose();
        }
    }
}
