﻿namespace Multiplexing
{
    using Common;
    using Multiplexer;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    internal class Program
    {
        private TcpListener listener;
        private List<ConnectionHandler> connectionHandlers = new List<ConnectionHandler>();

        private static void Main(string[] args)
        {
            ThreadPool.QueueUserWorkItem((o) => { Console.ReadLine(); Logger.Dump(); });
            new Program().Run();
        }

        private void Run()
        {
            this.listener = new TcpListener(IPAddress.Any, Constants.ServerPort);
            this.listener.Start();
            listener.BeginAcceptTcpClient(OnAccept, this);
            Console.WriteLine("[Server] Listening");
            Console.WriteLine("Press any key to stop");
            // Lock it
            object thisLock = new object();
            lock (thisLock)
            {
                Monitor.Wait(thisLock);
            }
        }

        private static void OnAccept(IAsyncResult ar)
        {
            Program thisPtr = (Program)ar.AsyncState;
            TcpClient client = thisPtr.listener.EndAcceptTcpClient(ar);
            thisPtr.OnAccept(client);
        }

        private void OnAccept(TcpClient client)
        {
            Logger.Log("[Server] Accepting TCP connection");
            this.listener.BeginAcceptTcpClient(OnAccept, this);
            this.connectionHandlers.Add(new ConnectionHandler(client));
            //new DebuggingConnectionHandler(client);
        }
    }

    #region Debugger
    internal class DebuggingConnectionHandler
    {
        private NetworkStream stream;
        private byte[] buffer = new byte[10];
        private int lineNumber;

        public DebuggingConnectionHandler(TcpClient client)
        {
            this.stream = client.GetStream();
            this.Next();
        }

        private void Next()
        {
            this.stream.BeginRead(buffer, 0, 10, OnRead, this);
        }

        private static void OnRead(IAsyncResult ar)
        {
            DebuggingConnectionHandler thisPtr = (DebuggingConnectionHandler)ar.AsyncState;
            try
            {
                int byteRead = thisPtr.stream.EndRead(ar);
                thisPtr.OnRead(byteRead);
            }
            catch
            {
                // Game over on this 'client', but no big deal for the rest
            }

        }

        private void OnRead(int byteRead)
        {
            if (byteRead != 0)
            {
                for (int i = 0; i < byteRead; i++)
                {
                    Console.WriteLine("{0}\t{1}", ++this.lineNumber, this.buffer[i]);
                }

                this.Next();
            }
        }
    }
    #endregion

    public class ConnectionHandler
    {
        private Connection connection;
        private List<ChannelHandler> channelHandlers = new List<ChannelHandler>();

        public ConnectionHandler(TcpClient client)
        {
            this.connection = new Connection(client.Client);
            this.connection.BeginAccept(OnAcceptedCallback, this);
        }

        private static void OnAcceptedCallback(IAsyncResult ar)
        {
            Logger.Log("[Server] Accepting Channel");
            ConnectionHandler thisPtr = (ConnectionHandler)ar.AsyncState;
            Channel stream = thisPtr.connection.EndAccept(ar);
            thisPtr.OnAccepted(stream);
        }

        private void OnAccepted(Channel stream)
        {
            this.connection.BeginAccept(OnAcceptedCallback, this);
            this.channelHandlers.Add(new ChannelHandler(stream));
        }
    }

    public class ChannelHandler
    {
        private static Random random = new Random();
        private Channel channel;

        public ChannelHandler(Channel channel)
        {
            this.channel = channel;

            ThreadPool.QueueUserWorkItem(HandleStreamCallback, this);
            //this.HandleStream();
        }

        private void HandleStreamCallback(object state)
        {
            ChannelHandler thisPtr = (ChannelHandler)state;
            thisPtr.HandleStream();

            //this.reader.ReadLineAsync().ContinueWith(OnReadLineCallback);
        }

        private void HandleStream()
        {
            using (StreamReader reader = new StreamReader(this.channel))
            {
                using (StreamWriter writer = new StreamWriter(this.channel))
                {
                    for (int i = 0; i < 500; i++)
                    {
                        writer.WriteLine(reader.ReadLine());
                        writer.Flush();
                    }
                }
            }
        }
    }
}
