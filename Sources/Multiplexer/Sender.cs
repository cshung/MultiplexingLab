﻿namespace Multiplexer
{
    using System;

    internal class Sender
    {
        private IFrameWriter frameWriter;
        private int streamId;

        internal Sender(IFrameWriter frameWriter, int streamId)
        {
            this.frameWriter = frameWriter;
            this.streamId = streamId;
        }

        internal IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return new WriteAsyncResult(this, frameWriter, streamId, new ArraySegment<byte>(buffer, offset, count), callback, state);
        }

        internal void EndWrite(IAsyncResult ar)
        {
            AsyncResult.End(ar, this, "Write");
        }
    }
}