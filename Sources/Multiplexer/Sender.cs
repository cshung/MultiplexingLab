namespace Multiplexer
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class Sender
    {
        private IFrameWriter frameWriter;
        private int streamId;
        private List<string> allSent = new List<string>();

        internal Sender(IFrameWriter frameWriter, int streamId)
        {
            this.frameWriter = frameWriter;
            this.streamId = streamId;
        }

        internal IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            allSent.Add(Encoding.ASCII.GetString(buffer, offset, count));
            Logger.Log("Sent " + this.streamId + "'" + string.Join(string.Empty, allSent) + "'");
            return new WriteAsyncResult(this, frameWriter, streamId, new ArraySegment<byte>(buffer, offset, count), callback, state);
        }

        internal void EndWrite(IAsyncResult ar)
        {
            AsyncResult.End(ar, this, "Write");
        }
    }
}