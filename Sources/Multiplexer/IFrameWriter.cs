namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading;

    public interface IFrameWriter
    {
        /* This is reduced APM - all I care is whether the call completed synchronously or not */
        bool BeginWriteFrames(List<WriteFrame> frames);
    }
}
