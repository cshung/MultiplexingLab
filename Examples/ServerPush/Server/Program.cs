﻿namespace Server
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
            ThreadPool.QueueUserWorkItem(async (state) => { await WorkAsync(client); });
        }

        private async Task WorkAsync(TcpClient client)
        {
            this.connection = new Connection(client.Client, ConnectionType.Server);
            using (Channel channel = this.connection.ConnectChannel())
            {
                using (StreamReader reader = new StreamReader(channel))
                {
                    using (StreamWriter writer = new StreamWriter(channel))
                    {
                        await writer.WriteLineAsync("Server is pushing");
                        await writer.FlushAsync();
                        Console.WriteLine("Server got response!" + await reader.ReadLineAsync());
                        await writer.WriteLineAsync("Go away");
                        await writer.FlushAsync();
                    }
                }
            }
        }
    }
}