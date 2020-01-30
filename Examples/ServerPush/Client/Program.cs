//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="PlaceholderCompany">
//     Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Client
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
        private static object termLock = new object();
        private TcpClient client;
        private Connection connection;

        private static void Main(string[] args)
        {
            Program program = new Program();
            program.Run();
            Console.ReadLine();
        }

        private static void OnConnectCompletedCallback(IAsyncResult ar)
        {
            Program thisPtr = (Program)ar.AsyncState;
            thisPtr.client.EndConnect(ar);
            thisPtr.OnConnectCompleted();
        }

        private static void OnAcceptChannelCallback(IAsyncResult ar)
        {
            Program thisPtr = (Program)ar.AsyncState;
            Channel channel = thisPtr.connection.EndAcceptChannel(ar);
            thisPtr.OnAcceptChannel(channel);
        }

        private void Run()
        {
            this.client = new TcpClient();
            this.client.BeginConnect(IPAddress.Loopback, Constants.Port, OnConnectCompletedCallback, this);
        }

        private void OnConnectCompleted()
        {
            this.connection = new Connection(this.client.Client, ConnectionType.Client);
            this.connection.BeginAcceptChannel(OnAcceptChannelCallback, this);
        }

        private void OnAcceptChannel(Channel channel)
        {
            ThreadPool.QueueUserWorkItem(async (state) => { await this.WorkAsync(channel); });
        }

        private async Task WorkAsync(Channel channel)
        {
            using (channel)
            {
                using (StreamReader reader = new StreamReader(channel))
                {
                    using (StreamWriter writer = new StreamWriter(channel))
                    {
                        await writer.WriteLineAsync(await reader.ReadLineAsync());
                        await writer.FlushAsync();
                        if (reader.EndOfStream)
                        {
                            Console.WriteLine("Client feel right!");
                        }

                        await channel.StopSendingAsync();
                    }
                }
            }

            // TODO: Figure out way to close the connection!
            this.client.Close();
        }
    }
}
