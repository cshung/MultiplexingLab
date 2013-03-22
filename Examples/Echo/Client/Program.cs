namespace Client
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using Common;
    using Connector;

    internal class Program
    {
        private static object termLock = new object();
        private static int pendingCount;
        private TcpClient client;
        private Connection connection;
        private Timer keepAliveTimer;

        private static void Main(string[] args)
        {
            new Program().Run();
        }

        private static void OnConnectCompletedCallback(IAsyncResult ar)
        {
            Program thisPtr = (Program)ar.AsyncState;
            thisPtr.client.EndConnect(ar);
            thisPtr.OnConnectCompleted();
        }

        private void Run()
        {
            this.client = new TcpClient();
            this.client.BeginConnect(IPAddress.Loopback, Constants.Port, OnConnectCompletedCallback, this);
            lock (termLock)
            {
                Monitor.Wait(termLock);
            }

            this.client.Close();
        }

        private void OnConnectCompleted()
        {
            this.connection = new Connection(this.client.Client, ConnectionType.Client);
            pendingCount = 10;
            this.keepAliveTimer = new Timer(this.KeepAlive);
            this.keepAliveTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
            for (int i = 0; i < pendingCount; i++)
            {
                ThreadPool.QueueUserWorkItem(this.ClientWork, i);
            }
        }

        private void KeepAlive(object state)
        {
            this.connection.KeepAlive();
        }

        private async void ClientWork(object state)
        {
            int clientId = (int)state;
            using (var channel = this.connection.ConnectChannel())
            {
                using (StreamWriter writer = new StreamWriter(channel))
                {
                    using (StreamReader reader = new StreamReader(channel))
                    {
                        string request = string.Format("Hello from {0}", clientId);
                        string response;
                        await writer.WriteLineAsync(request);
                        await writer.FlushAsync();
                        response = await reader.ReadLineAsync();
                        await writer.WriteLineAsync(request);
                        await writer.FlushAsync();
                        response = await reader.ReadLineAsync();
                        await channel.StopSendingAsync();
                        await channel.FlushAsync();
                        if (reader.EndOfStream)
                        {
                            Console.WriteLine("Client feel right!");
                        }
                    }
                }
            }

            if (Interlocked.Decrement(ref pendingCount) == 0)
            {
                lock (termLock)
                {
                    Monitor.Pulse(termLock);
                }
            }
        }
    }
}
