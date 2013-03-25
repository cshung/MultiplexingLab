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
        private TcpListener tunnelListener;
        private TcpListener clientListener;
        private Connection connection;

        private static void Main(string[] args)
        {
            Program program = new Program();
            program.Run();
            Console.ReadLine();
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
            this.tunnelListener = new TcpListener(IPAddress.Any, Constants.TunnelPort);
            this.tunnelListener.Start();
            this.tunnelListener.BeginAcceptTcpClient(OnAcceptTunnelCallback, this);
        }

        private void OnAcceptTunnel(TcpClient tunnel)
        {
            // Accept more tunnel connections
            this.tunnelListener.BeginAcceptTcpClient(OnAcceptTunnelCallback, this);

            this.connection = new Connection(tunnel.Client, ConnectionType.Server);

            this.clientListener = new TcpListener(IPAddress.Any, 12580);
            this.clientListener.Start();
            this.clientListener.BeginAcceptTcpClient(OnAcceptClientCallback, this);
        }

        private void OnAcceptClient(TcpClient client)
        {
            // Accept more client connections
            this.clientListener.BeginAcceptTcpClient(OnAcceptClientCallback, this);
            ThreadPool.QueueUserWorkItem((state) => { WorkAsync(client); });
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
                        Task forwardClientWriteTask = clientChannel.CopyToAsync(tunnelChannel).ContinueWith((t) => { tunnelChannel.StopSendingAsync(); }).ContinueWith((t) => { try { t.Wait(); } catch { } }); ;
                        Task forwardTunnelWriteTask = tunnelChannel.CopyToAsync(clientChannel).ContinueWith((t) => { client.Client.Shutdown(SocketShutdown.Send); }).ContinueWith((t) => { try { t.Wait(); } catch { } });
                        Task.WaitAll(forwardClientWriteTask, forwardTunnelWriteTask);
                    }
                }
            }
        }
    }
}
