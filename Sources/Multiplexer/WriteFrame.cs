namespace Multiplexer
{
    using System;

    internal class WriteFrame
    {
        internal WriteFrame(ArraySegment<byte> header, ArraySegment<byte> payload, WriteAsyncResult writeAsyncResult)
        {
            this.Header = header;
            this.Payload = payload;
            this.WriteAsyncResult = writeAsyncResult;
        }

        internal ArraySegment<byte> Header { get; private set; }

        internal ArraySegment<byte> Payload { get; private set; }

        internal WriteAsyncResult WriteAsyncResult { get; private set; }
    }
}