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
        private Dictionary<int, Stream> streams;

        private FrameWriter frameWriter;
        private FrameFragmentReader frameFragmentReader;

        public Connection(Socket socket)
        {
            this.socket = socket;
            this.streams = new Dictionary<int, Stream>();
            this.frameWriter = new FrameWriter(new TcpWriter(socket));
            this.frameFragmentReader = new FrameFragmentReader(new TcpReader(socket));
        }

        public Stream CreateStream()
        {
            int nextStreamId = Interlocked.Increment(ref this.streamId);
            Sender sender = new Sender(this.frameWriter, nextStreamId);
            // TODO: Client should be able to read too!
            Stream newStream = new Stream(sender, null);
            Console.WriteLine(this == null);
            Console.WriteLine(this.streams == null);
            this.streams.Add(nextStreamId, newStream);
            return newStream;
        }

        public IAsyncResult BeginAccept(AsyncCallback callback, object state)
        {
            return this.frameFragmentReader.BeginAccept(callback, state);
        }

        public Stream EndAccept(IAsyncResult ar)
        {
            Receiver receiver = this.frameFragmentReader.EndAccept(ar);
            return new Stream(new Sender(this.frameWriter, receiver.StreamId), receiver);
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