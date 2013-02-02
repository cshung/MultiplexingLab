namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Threading;

    public class Connection
    {
        private Socket socket;
        private int streamId;
        private ConcurrentDictionary<int, Channel> channels;

        private FrameWriter frameWriter;
        private FrameFragmentReader frameFragmentReader;

        public Connection(Socket socket)
        {
            this.socket = socket;
            this.channels = new ConcurrentDictionary<int, Channel>();
            this.frameWriter = new FrameWriter(new TcpWriter(socket));
            this.frameFragmentReader = new FrameFragmentReader(new TcpReader(socket));
        }

        public Channel CreateChannel()
        {
            int nextStreamId = Interlocked.Increment(ref this.streamId);
            Sender sender = new Sender(this.frameWriter, nextStreamId);
            Channel newStream = new Channel(sender, this.frameFragmentReader.CreateReceiver(nextStreamId));
            // TODO, possible to fail at all?
            this.channels.TryAdd(nextStreamId, newStream);
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

        public IAsyncResult BeginClose(AsyncCallback callback, object state)
        {
            return this.frameWriter.BeginClose(callback, state);
        }

        public void EndClose(IAsyncResult ar)
        {
            this.frameWriter.EndClose(ar);
            this.socket.Close();
        }
    }
}