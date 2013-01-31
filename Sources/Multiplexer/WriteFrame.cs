namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading;

    public class WriteFrame
    {
        public WriteFrame(ArraySegment<byte> header, ArraySegment<byte> payload, WriteAsyncResult writeAsyncResult)
        {
            this.Header = header;
            this.Payload = payload;
            this.WriteAsyncResult = writeAsyncResult;
        }

        public ArraySegment<byte> Header { get; private set; }

        public ArraySegment<byte> Payload { get; private set; }

        public WriteAsyncResult WriteAsyncResult { get; private set; }
    }
}