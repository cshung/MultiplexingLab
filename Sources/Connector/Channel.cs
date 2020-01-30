//-----------------------------------------------------------------------
// <copyright file="Channel.cs" company="PlaceholderCompany">
//     Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Connector
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public sealed class Channel : Stream
    {
        private Connection connection;
        private int channelId;

        internal Channel(Connection connection, int channelId)
        {
            this.connection = connection;
            this.channelId = channelId;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Length
        {
            get { throw new System.NotImplementedException(); }
        }

        public override long Position
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public override void Flush()
        {
            // no-op
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.EndRead(this.BeginRead(buffer, offset, count, null, null));
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (offset < 0 || offset >= buffer.Length)
            {
                throw new ArgumentNullException("offset");
            }

            if (count <= 0 || offset + count > buffer.Length)
            {
                throw new ArgumentNullException("count");
            }

            return this.connection.BeginRead(this.channelId, buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult ar)
        {
            return this.connection.EndRead(ar);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.EndWrite(this.BeginWrite(buffer, offset, count, null, null));
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (offset < 0 || offset >= buffer.Length)
            {
                throw new ArgumentNullException("offset");
            }

            if (count <= 0 || offset + count > buffer.Length)
            {
                throw new ArgumentNullException("count");
            }

            return this.connection.BeginWrite(this.channelId, buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            this.connection.EndWrite(asyncResult);
        }

        public void StopSending()
        {
            this.EndStopSending(this.BeginStopSending(null, null));
        }

        public IAsyncResult BeginStopSending(AsyncCallback callback, object state)
        {
            // Sending a zero size frame means closing the channel
            return this.connection.BeginWrite(this.channelId, new byte[0], 0, 0, callback, state);
        }

        public void EndStopSending(IAsyncResult ar)
        {
            this.connection.EndWrite(ar);
        }

        public Task StopSendingAsync()
        {
            return new TaskFactory().FromAsync(this.BeginStopSending, this.EndStopSending, null);
        }
    }
}
