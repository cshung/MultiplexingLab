namespace Multiplexer.UnitTest
{
    using Multiplexer;
    using System;
    using System.Collections.Generic;

    internal static class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                TestSmallFrame();
                TestJustFitFrame();
                Console.WriteLine("Phew");
            }
            catch
            {
                Console.WriteLine("Ooops");
            }
        }

        private static void TestSmallFrame()
        {
            FakeFrameWriter fakeFrameWriter = new FakeFrameWriter();
            Sender sender = new Sender(fakeFrameWriter, 0);
            sender.BeginWrite(new byte[] { 1, 2, 3, 4 }, 1, 2, null, null);
            fakeFrameWriter.VerifySingleFrame(1, 2);
            Console.WriteLine("TestSmallFrame passed.");
        }

        private static void TestJustFitFrame()
        {
            FakeFrameWriter fakeFrameWriter = new FakeFrameWriter();
            Sender sender = new Sender(fakeFrameWriter, 0);
            sender.BeginWrite(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 10, null, null);
            fakeFrameWriter.VerifySingleFrame(0, 10);
            Console.WriteLine("TestJustFitFrame passed.");
        }

        // TODO: Unit Testing other components as well
    }

    internal class FakeFrameWriter : IFrameWriter
    {
        List<List<WriteFrame>> writtenFrames = new List<List<WriteFrame>>();

        public bool BeginWriteFrames(List<WriteFrame> frames)
        {
            this.writtenFrames.Add(frames);
            return false;
        }

        public void VerifySingleFrame(int expectedOffset, int expectedFrameSize)
        {
            if (this.writtenFrames.Count != 1)
            {
                throw new Exception();
            }

            if (this.writtenFrames[0].Count != 1)
            {
                throw new Exception();
            }

            if (this.writtenFrames[0][0].Payload == null)
            {
                throw new Exception();
            }

            if (this.writtenFrames[0][0].Payload.Offset != expectedOffset)
            {
                throw new Exception();
            }

            if (this.writtenFrames[0][0].Payload.Count != expectedFrameSize)
            {
                throw new Exception();
            }
        }
    }
}
