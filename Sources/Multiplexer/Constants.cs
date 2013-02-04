namespace Multiplexer
{
    internal static class Constants
    {
        /* This small value is intended for testing only */
        internal static int FrameSize = 16000;

        /* This rather odd value is trying to make sure the logic is right */
        internal static int DecodingBufferSize = 16000000;

        /* This is handy */
        internal static int HeaderLength = 8;
    }
}