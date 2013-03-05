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
            ThreadPool.QueueUserWorkItem((o) => { Console.ReadLine(); Logger.Dump(); });
            new Program().Run();
        }

        private void Run()
        {
            this.client = new TcpClient();
            this.client.BeginConnect(IPAddress.Loopback, Constants.ServerPort, OnConnectCompleted, this);
            terminationLock.WaitOne();
            Console.WriteLine("Done!");
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
            this.requestCount = 1;
            for (int i = 0; i < this.requestCount; i++)
            {
                ThreadPool.QueueUserWorkItem((state) => { new Executor(connection, this).Run((int)state); }, i);
            }
        }

        private void OnExecutionCompleted()
        {
            int decreased = Interlocked.Decrement(ref this.requestCount);
            if (decreased == 0)
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

            public Executor(Connection connection, Program program)
            {
                this.channel = connection.CreateChannel();
                this.program = program;
            }

            internal void Run(int value)
            {
                using (StreamWriter writer = new StreamWriter(this.channel))
                {
                    using (StreamReader reader = new StreamReader(this.channel))
                    {
                        for (int i = 0; i < 500; i++)
                        {
                            string request = "Sending over " + value + " to server";
                            writer.WriteLine(request);
                            writer.Flush();
                            string response = reader.ReadLine();
                            if (string.Equals(request, response))
                            {
                                Console.WriteLine("Completed request");
                            }
                            else
                            {
                                Console.WriteLine("Inconsistent reply detected");
                            }
                        }
                        //writer.WriteLine(request);
                        //writer.Flush();
                        //response = reader.ReadLine();
                        //if (!string.Equals(request, response))
                        //{
                        //    Console.WriteLine("Inconsistent reply detected");
                        //}
                    }
                }

                program.OnExecutionCompleted();
            }
        }
    }
}
