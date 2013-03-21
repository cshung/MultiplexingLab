namespace Connector
{
    using System.Collections.Generic;
    using System.Linq;

    internal class FrameHeader
    {
        public FrameHeader(int length, int channelId)
        {
            this.Length = length;
            this.ChannelId = channelId;
        }

        internal int Length { get; private set; }

        internal int ChannelId { get; private set; }

        internal static FrameHeader Decode(IEnumerable<byte> headerBytes)
        {
            byte[] headerBytesArray = headerBytes.ToArray();

            int lengthA = headerBytesArray[0];
            int lengthB = headerBytesArray[1];
            int lengthC = headerBytesArray[2];
            int lengthD = headerBytesArray[3];
            int length = (lengthA * 0x1000) + (lengthB * 0x0100) + (lengthC * 0x0010) + (lengthD * 0x0001);

            int channelA = headerBytesArray[4];
            int channelB = headerBytesArray[5];
            int channelC = headerBytesArray[6];
            int channelD = headerBytesArray[7];
            int channelId = (channelA * 0x1000) + (channelB * 0x0100) + (channelC * 0x0010) + (channelD * 0x0001);

            return new FrameHeader(length, channelId);
        }

        internal byte[] Encode()
        {
            byte[] header = new byte[8];
            header[0] = (byte)((this.Length & 0xF000) / 0x1000);
            header[1] = (byte)((this.Length & 0x0F00) / 0x0100);
            header[2] = (byte)((this.Length & 0x00F0) / 0x0010);
            header[3] = (byte)((this.Length & 0x000F) / 0x0001);

            header[4] = (byte)((this.ChannelId & 0xF000) / 0x1000);
            header[5] = (byte)((this.ChannelId & 0x0F00) / 0x0100);
            header[6] = (byte)((this.ChannelId & 0x00F0) / 0x0010);
            header[7] = (byte)((this.ChannelId & 0x000F) / 0x0001);

            return header;
        }
    }
}
