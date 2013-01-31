namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading;

    internal static class Constants
    {
        /* This small value is intended for testing only */
        public static int FrameSize = 10;

        /* This rather odd value is trying to make sure the logic is right */
        public static int DecodingBufferSize = 14;
    }
}