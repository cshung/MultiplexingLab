namespace Multiplexer
{
    using System;
    using System.IO;

    public class Channel : Stream
    {
        private Sender sender;
        private Receiver receiver;

        internal Channel(Sender sender, Receiver receiver)
        {
            this.sender = sender;
            this.receiver = receiver;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return this.receiver.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult ar)
        {
            return this.receiver.EndRead(ar);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return this.sender.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult ar)
        {
            this.sender.EndWrite(ar);
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

        public override void Flush()
        {
            // no-op, the implementation don't buffer anything.
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.EndRead(this.BeginRead(buffer, offset, count, null, null));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.EndWrite(this.BeginWrite(buffer, offset, count, null, null));
        }
    }
}
