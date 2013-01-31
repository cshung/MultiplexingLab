namespace Multiplexing
{
    using Common;
    using Multiplexer;
    using System;
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
            private Stream stream;
            private Program program;
            private byte[] buffer = new byte[1];

            public Executor(Connection connection, Program program)
            {
                this.stream = connection.CreateStream();
                this.program = program;
            }

            internal void Run(byte value)
            {
                byte[] buffer = new byte[value];
                for (byte i = 0; i < value; i++)
                {
                    buffer[i] = i;
                }
                this.stream.BeginWrite(buffer, 0, value, OnStreamWriteCompleted, this);
            }

            private static void OnStreamWriteCompleted(IAsyncResult ar)
            {
                Executor thisPtr = (Executor)ar.AsyncState;
                thisPtr.stream.EndWrite(ar);
                thisPtr.OnStreamWriteCompleted();
            }

            private void OnStreamWriteCompleted()
            {
                //this.stream.BeginRead(buffer, 0, 1, OnStreamReadCompleted, this);
                this.program.OnExecutionCompleted();
            }

            private static void OnStreamReadCompleted(IAsyncResult ar)
            {
                Executor thisPtr = (Executor)ar.AsyncState;
                int count = thisPtr.stream.EndRead(ar);
                thisPtr.OnStreamReadCompleted(count);
            }

            private void OnStreamReadCompleted(int count)
            {
                Console.WriteLine("Got response!");
                Console.WriteLine(count);
                for (int i = 0; i < count; i++)
                {
                    Console.WriteLine(buffer[i]);
                }

                this.program.OnExecutionCompleted();
            }
        }
    }
}
