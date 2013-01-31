namespace Multiplexer
{
    using System.Collections.Generic;

    internal interface IFrameWriter
    {
        /* This is reduced APM - all I care is whether the call completed synchronously or not */
        bool BeginWriteFrames(List<WriteFrame> frames);
    }
}
