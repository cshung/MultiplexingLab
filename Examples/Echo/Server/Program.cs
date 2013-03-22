namespace Server
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Connector;

    internal class Program
    {
        private TcpListener listener;
        private Connection connection;

        private static void Main(string[] args)
        {
            Program program = new Program();
            program.Run();
            Console.ReadLine();
        }

        private static void OnAcceptCallback(IAsyncResult ar)
        {
            Program thisPtr = (Program)ar.AsyncState;
            thisPtr.OnAccept(thisPtr.listener.EndAcceptTcpClient(ar));
        }

        private static void OnAcceptChannelCallback(IAsyncResult ar)
        {
            Program thisPtr = (Program)ar.AsyncState;
            thisPtr.OnAcceptChannel(thisPtr.connection.EndAcceptChannel(ar));
        }

        private static async Task WorkAsync(Channel channel)
        {
            using (channel)
            {
                using (var reader = new StreamReader(channel))
                {
                    using (var writer = new StreamWriter(channel))
                    {
                        string request = await reader.ReadLineAsync();
                        await writer.WriteLineAsync(request);
                        await writer.FlushAsync();

                        request = await reader.ReadLineAsync();
                        await writer.WriteLineAsync(request);
                        await writer.FlushAsync();

                        Console.WriteLine("Server waiting for close");

                        if (reader.EndOfStream)
                        {
                            Console.WriteLine("Server feel right!");
                        }

                        await channel.StopSendingAsync();
                        await channel.FlushAsync();
                    }
                }
            }
        }

        private void Run()
        {
            this.listener = new TcpListener(IPAddress.Any, Constants.Port);
            this.listener.Start();
            this.listener.BeginAcceptTcpClient(OnAcceptCallback, this);
        }

        private void OnAccept(TcpClient client)
        {
            // Accept more connections
            this.listener.BeginAcceptTcpClient(OnAcceptCallback, this);
            this.connection = new Connection(client.Client, ConnectionType.Server);
            this.connection.BeginAcceptChannel(OnAcceptChannelCallback, this);
        }

        // TODO: Be careful with async callback - they can't be blocked - or actor will block.
        private void OnAcceptChannel(Channel channel)
        {
            this.connection.BeginAcceptChannel(OnAcceptChannelCallback, this);
            ThreadPool.QueueUserWorkItem(async (state) => { await WorkAsync(channel); });            
        }
    }
}
