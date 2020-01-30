//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="PlaceholderCompany">
//     Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

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
            ThreadPool.QueueUserWorkItem(async (state) => { await this.WorkAsync(client); });
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
                        Console.WriteLine(await reader.ReadLineAsync());
                        await channel.StopSendingAsync();
                        if (reader.EndOfStream)
                        {
                            Console.WriteLine("Server feel right!");
                        }
                    }
                }
            }
        }
    }
}
