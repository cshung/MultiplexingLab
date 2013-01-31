namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading;

    public class WriteAsyncResult : AsyncResult
    {
        private int incompleteFrameCounts;

        public WriteAsyncResult(Sender sender, IFrameWriter frameWriter, int streamId, ArraySegment<byte> buffer, AsyncCallback callback, object state)
            : base(callback, state, sender, "Write")
        {
            // Break the buffer into frames.
            int current = buffer.Offset;
            int sizeRemaining = buffer.Count;
            List<WriteFrame> writeFrames = new List<WriteFrame>();
            while (sizeRemaining > 0)
            {
                int nextFrameSize = Constants.FrameSize;
                if (nextFrameSize > sizeRemaining)
                {
                    nextFrameSize = sizeRemaining;
                }

                byte[] header = new byte[8];

                header[0] = (byte)((nextFrameSize & 0xF000) / 0x1000);
                header[1] = (byte)((nextFrameSize & 0x0F00) / 0x0100);
                header[2] = (byte)((nextFrameSize & 0x00F0) / 0x0010);
                header[3] = (byte)((nextFrameSize & 0x000F) / 0x0001);

                header[4] = (byte)((streamId & 0xF000) / 0x1000);
                header[5] = (byte)((streamId & 0x0F00) / 0x0100);
                header[6] = (byte)((streamId & 0x00F0) / 0x0010);
                header[7] = (byte)((streamId & 0x000F) / 0x0001);

                writeFrames.Add(new WriteFrame(new ArraySegment<byte>(header, 0, 8), new ArraySegment<byte>(buffer.Array, current, nextFrameSize), this));
                current += nextFrameSize;
                sizeRemaining -= nextFrameSize;
            }

            bool completedSynchronously = frameWriter.BeginWriteFrames(writeFrames);
            if (completedSynchronously)
            {
                base.Complete(null, true);
            }
            else
            {
                this.incompleteFrameCounts = writeFrames.Count;
            }
        }

        public void CompleteFrame()
        {
            if (--this.incompleteFrameCounts == 0)
            {
                base.Complete(null);
            }
        }
    }
}