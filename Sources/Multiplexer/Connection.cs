namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Net.Sockets;
    using System.Threading;

    public class Connection
    {
        private Socket socket;
        private int streamId;
        private ConcurrentDictionary<int, Channel> channels;

        private FrameWriter frameWriter;
        private FrameFragmentReader frameFragmentReader;
        private CloseAsyncResult pendingCloseRequest;

        public Connection(Socket socket)
        {
            this.socket = socket;
            this.channels = new ConcurrentDictionary<int, Channel>();
            this.frameWriter = new FrameWriter(new TcpWriter(socket));
            this.frameFragmentReader = new FrameFragmentReader(this, new TcpReader(socket));
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
            this.pendingCloseRequest = new CloseAsyncResult(callback, state, this);
            this.BeginCloseSender(null, null);
            return this.pendingCloseRequest;
        }

        public void EndClose(IAsyncResult ar)
        {
            AsyncResult.End(ar, this, "Close");
        }

        internal void OnCloseReceived()
        {
            if (this.pendingCloseRequest == null)
            {
                this.BeginCloseSender(OnSenderClosedCallback, this);
            }
            else
            {
                this.socket.Close();
                this.pendingCloseRequest.Complete();
            }
        }

        private IAsyncResult BeginCloseSender(AsyncCallback callback, object state)
        {
            return this.frameWriter.BeginClose(callback, state);
        }

        private void EndCloseSender(IAsyncResult ar)
        {
            this.frameWriter.EndClose(ar);
        }

        private static void OnSenderClosedCallback(IAsyncResult ar)
        {
            Connection thisPtr = (Connection)ar.AsyncState;
            thisPtr.socket.Close();
        }
    }
}