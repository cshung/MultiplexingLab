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
        private TcpClient client;
        private AutoResetEvent terminationLock = new AutoResetEvent(false);
        private int requestCount;
        private Connection connection;

        private static void Main(string[] args)
        {
            new Program().Run();
        }

        private void Run()
        {
            this.client = new TcpClient();
            this.client.BeginConnect(IPAddress.Loopback, Constants.ServerPort, OnConnectCompleted, this);
            terminationLock.WaitOne();
        }

        private static void OnConnectCompleted(IAsyncResult ar)
        {
            Program thisPtr = (Program)ar.AsyncState;
            thisPtr.client.EndConnect(ar);
            thisPtr.OnConnectCompleted();
        }

        private void OnConnectCompleted()
        {
            this.connection = new Connection(this.client.Client);
            ThreadPool.QueueUserWorkItem((state) => { new Executor(connection, this).Run(13); }, this);
            ThreadPool.QueueUserWorkItem((state) => { new Executor(connection, this).Run(4); }, this);
            ThreadPool.QueueUserWorkItem((state) => { new Executor(connection, this).Run(27); }, this);
        }

        private void OnExecutionCompleted()
        {
            if (Interlocked.Increment(ref this.requestCount) == 3)
            {
                this.connection.BeginClose(OnConnectionClosed, this);
            }
        }

        private static void OnConnectionClosed(IAsyncResult ar)
        {
            Program thisPtr = (Program)ar.AsyncState;
            thisPtr.connection.EndClose(ar);
            thisPtr.OnConnectionClosed();
        }

        private void OnConnectionClosed()
        {
            this.terminationLock.Set();
        }

        private class Executor
        {
            private Channel channel;
            private Program program;
            private byte[] buffer = new byte[1];

            public Executor(Connection connection, Program program)
            {
                this.channel = connection.CreateStream();
                this.program = program;
            }

            internal void Run(byte value)
            {
                using (StreamWriter writer = new StreamWriter(this.channel))
                {
                    writer.WriteLine("Sending over " + value + " to server");
                    writer.Flush();
                    using (StreamReader reader = new StreamReader(this.channel))
                    {
                        Console.WriteLine(reader.ReadLine());
                    }
                }
            }
        }
    }
}
