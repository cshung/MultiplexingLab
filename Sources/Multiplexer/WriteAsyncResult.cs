namespace Multiplexer
{
    using System;
    using System.Collections.Generic;

    internal class WriteAsyncResult : AsyncResult
    {
        private int incompleteFrameCounts;

        internal WriteAsyncResult(Sender sender, IFrameWriter frameWriter, int streamId, ArraySegment<byte> buffer, AsyncCallback callback, object state)
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

                FrameHeader header = new FrameHeader(nextFrameSize, streamId);

                writeFrames.Add(new WriteFrame(new ArraySegment<byte>(header.Encode(), 0, Constants.HeaderLength), new ArraySegment<byte>(buffer.Array, current, nextFrameSize), this));
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

        // TODO: Naming?
        internal WriteAsyncResult(IFrameWriter frameWriter, AsyncCallback callback, object state)
            : base(callback, state, frameWriter, "Close")
        {
            // The frame decoder requires at least one byte per frame to decode correctly
            // TODO: Get rid of this constraint
            List<WriteFrame> writeFrames = new List<WriteFrame> { new WriteFrame(new ArraySegment<byte>(new FrameHeader(1, 0).Encode(), 0, Constants.HeaderLength), new ArraySegment<byte>(new byte[1], 0, 1), this) };
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

        internal void OnFrameCompleted()
        {
            if (--this.incompleteFrameCounts == 0)
            {
                base.Complete(null);
            }
        }
    }
}