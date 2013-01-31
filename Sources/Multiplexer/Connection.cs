namespace Multiplexer
{
    using System;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Threading;

    public class Connection
    {
        private Socket socket;
        private int streamId;
        private Dictionary<int, Channel> streams;

        private FrameWriter frameWriter;
        private FrameFragmentReader frameFragmentReader;

        public Connection(Socket socket)
        {
            this.socket = socket;
            this.streams = new Dictionary<int, Channel>();
            this.frameWriter = new FrameWriter(new TcpWriter(socket));
            this.frameFragmentReader = new FrameFragmentReader(new TcpReader(socket));
        }

        public Channel CreateStream()
        {
            int nextStreamId = Interlocked.Increment(ref this.streamId);
            Sender sender = new Sender(this.frameWriter, nextStreamId);
            Channel newStream = new Channel(sender, this.frameFragmentReader.CreateReceiver(nextStreamId));
            Console.WriteLine(this == null);
            Console.WriteLine(this.streams == null);
            this.streams.Add(nextStreamId, newStream);
            return newStream;
        }

        public IAsyncResult BeginAccept(AsyncCallback callback, object state)
        {
            return this.frameFragmentReader.BeginAccept(callback, state);
        }

        public Channel EndAccept(IAsyncResult ar)
        {
            Receiver receiver = this.frameFragmentReader.EndAccept(ar);
            return new Channel(new Sender(this.frameWriter, receiver.StreamId), receiver);
        }

        // TODO: Implement close 
        public void BeginClose(AsyncCallback callback, object state)
        {
            //throw new NotImplementedException();
        }

        public void EndClose(IAsyncResult ar)
        {
            //throw new NotImplementedException();
        }
    }
}