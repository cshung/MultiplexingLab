namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Threading;
    
    public class Stream
    {
        private int streamId;
        private Sender sender;

        internal Stream(int streamId, Sender sender)
        {
            this.streamId = streamId;
            this.sender = sender;
        }

        public void BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotImplementedException();
        }

        public int EndRead(IAsyncResult ar)
        {
            throw new NotImplementedException();
        }

        public void BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            this.sender.BeginWrite(buffer, offset, count, streamId, callback, state);
        }

        public void EndWrite(IAsyncResult ar)
        {
            this.sender.EndWrite(ar);
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
