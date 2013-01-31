namespace Multiplexer
{
    using System.Collections.Generic;
    using System.Linq;

    internal class FrameHeader
    {
        public FrameHeader(byte flag, int length, int streamId)
        {
            this.Flag = flag;
            this.Length = length;
            this.StreamId = streamId;
        }

        internal int Length { get; private set; }

        internal int StreamId { get; private set; }

        internal byte Flag { get; private set; }

        internal byte[] Encode()
        {
            byte[] header = new byte[Constants.HeaderLength];

            header[0] = this.Flag;
            header[1] = (byte)((this.Length & 0x0F00) / 0x0100);
            header[2] = (byte)((this.Length & 0x00F0) / 0x0010);
            header[3] = (byte)((this.Length & 0x000F) / 0x0001);

            header[4] = (byte)((this.StreamId & 0xF000) / 0x1000);
            header[5] = (byte)((this.StreamId & 0x0F00) / 0x0100);
            header[6] = (byte)((this.StreamId & 0x00F0) / 0x0010);
            header[7] = (byte)((this.StreamId & 0x000F) / 0x0001);

            return header;
        }

        internal static FrameHeader Decode(IEnumerable<byte> headerBytes)
        {
            byte[] headerBytesArray = headerBytes.ToArray();

            byte flag = headerBytesArray[0];
            int lengthB = headerBytesArray[1];
            int lengthC = headerBytesArray[2];
            int lengthD = headerBytesArray[3];
            int length = lengthB * 0x0100 + lengthC * 0x0010 + lengthD * 0x0001;

            int streamA = headerBytesArray[4];
            int streamB = headerBytesArray[5];
            int streamC = headerBytesArray[6];
            int streamD = headerBytesArray[7];
            int streamId = streamA * 0x1000 + streamB * 0x0100 + streamC * 0x0010 + streamD * 0x0001;

            return new FrameHeader(flag, length, streamId);
        }
    }
}
