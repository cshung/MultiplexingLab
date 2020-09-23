namespace Auth
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;

    internal class Program
    {
        private static void Main(string[] args)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 30624);
            listener.Start();
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("Connected");
                NetworkStream stream = client.GetStream();
                StreamReader reader = new StreamReader(stream);
                StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };
                string line = reader.ReadLine();
                Console.WriteLine("Type yes to accept");
                string response = Console.ReadLine();
                writer.WriteLine(response);
                writer.Close();
                reader.Close();
                client.Close();
            }
        }
    }
}
