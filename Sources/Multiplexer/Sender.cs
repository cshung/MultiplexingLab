namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading;

    public class Sender
    {
        private IFrameWriter frameWriter;

        public Sender(IFrameWriter frameWriter)
        {
            this.frameWriter = frameWriter;
        }

        public IAsyncResult BeginWrite(byte[] buffer, int offset, int count, int streamId, AsyncCallback callback, object state)
        {
            return new WriteAsyncResult(this, frameWriter, streamId, new ArraySegment<byte>(buffer, offset, count), callback, state);
        }

        public void EndWrite(IAsyncResult ar)
        {
            AsyncResult.End(ar, this, "Write");
        }
    }
}