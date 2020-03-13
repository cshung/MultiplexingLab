//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="PlaceholderCompany">
//     Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Server
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Connector;

    internal class Program
    {
        private TcpListener tunnelListener;
        private TcpListener clientListener;
        private Connection connection;

        // Configuration values
        private int tunnelPort;
        private int clientPort;

        private static void Main(string[] args)
        {
            Program program = new Program();
            program.Run();
            new ManualResetEvent(false).WaitOne();
        }

        private static void OnAcceptTunnelCallback(IAsyncResult ar)
        {
            Program thisPtr = (Program)ar.AsyncState;
            thisPtr.OnAcceptTunnel(thisPtr.tunnelListener.EndAcceptTcpClient(ar));
        }

        private static void OnAcceptClientCallback(IAsyncResult ar)
        {
            Program thisPtr = (Program)ar.AsyncState;
            thisPtr.OnAcceptClient(thisPtr.tunnelListener.EndAcceptTcpClient(ar));
        }

        private void Run()
        {
            if (!this.ReadConfiguration())
            {
                Console.WriteLine("Configuration file error - please fix your configuration file.");
                return;
            }

            this.tunnelListener = new TcpListener(IPAddress.Any, this.tunnelPort);
            this.tunnelListener.Start();
            this.tunnelListener.BeginAcceptTcpClient(OnAcceptTunnelCallback, this);
        }

        private bool ReadConfiguration()
        {
            string tunnelPortString = ConfigurationManager.AppSettings["tunnelPort"];
            if (string.IsNullOrEmpty(tunnelPortString))
            {
                return false;
            }

            if (!int.TryParse(tunnelPortString, out this.tunnelPort))
            {
                return false;
            }

            string clientPortString = ConfigurationManager.AppSettings["clientPort"];
            if (string.IsNullOrEmpty(clientPortString))
            {
                return false;
            }

            if (!int.TryParse(clientPortString, out this.clientPort))
            {
                return false;
            }

            return true;
        }

        private void OnAcceptTunnel(TcpClient tunnel)
        {
            // Accept more tunnel connections
            this.tunnelListener.BeginAcceptTcpClient(OnAcceptTunnelCallback, this);

            this.connection = new Connection(tunnel.Client, ConnectionType.Server);

            this.clientListener = new TcpListener(IPAddress.Any, this.clientPort);
            this.clientListener.Start();
            this.clientListener.BeginAcceptTcpClient(OnAcceptClientCallback, this);
        }

        private void OnAcceptClient(TcpClient client)
        {
            // Accept more client connections
            this.clientListener.BeginAcceptTcpClient(OnAcceptClientCallback, this);
            ThreadPool.QueueUserWorkItem((state) => { this.WorkAsync(client); });
        }

        private void WorkAsync(TcpClient client)
        {
            using (client)
            {
                using (Channel tunnelChannel = this.connection.ConnectChannel())
                {
                    using (Stream clientChannel = client.GetStream())
                    {
                        // Server
                        Task forwardClientWriteTask = clientChannel.CopyToAsync(tunnelChannel).ContinueWith((t) =>
                        {
                            tunnelChannel.StopSendingAsync();
                        }).ContinueWith((t) =>
                        {
                            try
                            {
                                t.Wait();
                            }
                            catch
                            {
                            }
                        });
                        Task forwardTunnelWriteTask = tunnelChannel.CopyToAsync(clientChannel).ContinueWith((t) =>
                        {
                            client.Client.Shutdown(SocketShutdown.Send);
                        }).ContinueWith((t) =>
                        {
                            try
                            {
                                t.Wait();
                            }
                            catch
                            {
                            }
                        });
                        Task.WaitAll(forwardClientWriteTask, forwardTunnelWriteTask);
                    }
                }
            }
        }
    }
}
