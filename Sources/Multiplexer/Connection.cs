namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading;

    public class Connection
    {
        private Socket socket;
        private int streamId;
        private Dictionary<int, Stream> streams;

        private FrameWriter frameWriter;

        public Connection(Socket socket)
        {
            this.socket = socket;
            this.streams = new Dictionary<int, Stream>();
            this.frameWriter = new FrameWriter(new TcpWriter(socket));
        }

        public Stream CreateStream()
        {
            int nextStreamId = Interlocked.Increment(ref this.streamId);
            Sender sender = new Sender(this.frameWriter);
            Stream newStream = new Stream(nextStreamId, sender);
            Console.WriteLine(this == null);
            Console.WriteLine(this.streams == null);
            this.streams.Add(nextStreamId, newStream);
            return newStream;
        }

        public void BeginClose(AsyncCallback callback, object state)
        {
            socket.Close();
            //throw new NotImplementedException();
        }

        public void EndClose(IAsyncResult ar)
        {
            //throw new NotImplementedException();
        }
    }
}