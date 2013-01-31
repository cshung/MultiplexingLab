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

    /** 
     * Design ideas:
     * 1.) Any Stream.Read() call requires filling a buffer - copy must happen.
     * 2.) buffer should be free for modification after Write operation completes.

         * 3.) Socket can write ArraySegments as optimization. Need to carefully callback individual Write calls.
               Sender writes to Stream, synchronously wrap the request, creates frame as ArraySegment 
               When Socket ready for send - update remaining data buffer with new ArraySegments, and relationship of Segment to requests
               On Socket send completes, update remaining data buffer, completes any request that should be completed ... -> when Socket ready for Send

         * 4.) Socket always read in single circular buffer trunks
     *     after receiving data, reader should synchronously split them into streams of frame fragments. 
               Each trunk should count the number of frame fragments and each frame fragment should remember the trunk
         *     If new stream is created and accept is pending - complete the pending operation.
         *     If new data is available and read is pending - complete the pending operation. 
         *     If all frame fragments of a trunk is consumed, free the trunk.
         *     If no read pending while a trunk is freed, issue a read. 
         *     If read is completed and trunk available, issue a read.
         *     Time out new stream creation - and kill all fragments.
         */
}
