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
        private Timer keepAliveTimer;

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
            this.client.BeginConnect(IPAddress.Loopback, Constants.TunnelPort, OnConnectCompletedCallback, this);
        }

        private void OnConnectCompleted()
        {
            this.connection = new Connection(this.client.Client, ConnectionType.Client);
            this.keepAliveTimer = new Timer(this.KeepAlive);
            this.keepAliveTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
            this.connection.BeginAcceptChannel(OnAcceptChannelCallback, this);
        }

        private void KeepAlive(object state)
        {
            this.connection.KeepAlive();
        }

        private void OnAcceptChannel(Channel channel)
        {
            // Accept more channels
            this.connection.BeginAcceptChannel(OnAcceptChannelCallback, this);
            ThreadPool.QueueUserWorkItem(async (state) => { await this.WorkAsync(channel); });
        }

        private async Task WorkAsync(Channel tunnelChannel)
        {
            using (tunnelChannel)
            {
                TcpClient remoteClient = new TcpClient();
                await remoteClient.ConnectAsync("localhost", 8080);
                using (var remoteChannel = remoteClient.GetStream())
                {
                    // Client
                    Task forwardRemoteWriteTask = remoteChannel.CopyToAsync(tunnelChannel).ContinueWith((t) => { tunnelChannel.StopSendingAsync(); }).ContinueWith((t) => { try { t.Wait(); } catch { } });
                    Task forwardTunnelWriteTask = tunnelChannel.CopyToAsync(remoteChannel).ContinueWith((t) => { remoteClient.Client.Shutdown(SocketShutdown.Send); }).ContinueWith((t) => { try { t.Wait(); } catch { } });
                    Task.WaitAll(forwardRemoteWriteTask, forwardTunnelWriteTask);
                }
            }
        }
    }
}
