namespace Connector
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using Actor;

    public class Connection
    {
        private Socket socket;
        private ConnectionType connectionType;
        private int nextChannelId;
        private Dictionary<int, Channel> channels;
        private ActorManager actorManager;
        private SenderActor senderActor;
        private TransportReceivingActor transportReceivingActor;

        public Connection(Socket socket, ConnectionType connectionType)
        {
            this.socket = socket;
            this.connectionType = connectionType;

            this.channels = new Dictionary<int, Channel>();
            if (connectionType == ConnectionType.Client)
            {
                this.nextChannelId = 1;
            }
            else
            {
                this.nextChannelId = 2;
            }

            this.actorManager = new ActorManager();
            this.senderActor = new SenderActor(this, this.actorManager);
            this.transportReceivingActor = new TransportReceivingActor(this, this.actorManager);
            this.actorManager.RunActor(this.senderActor);
            this.actorManager.RunActor(this.transportReceivingActor);
        }

        public Channel ConnectChannel()
        {
            Channel result = null;
            lock (this.channels)
            {
                result = new Channel(this, this.nextChannelId);
                this.channels.Add(this.nextChannelId, result);
                this.actorManager.SendMessage(this.transportReceivingActor, new ChannelCreatedMessage(this.nextChannelId));
                result.BeginStopSending(null, null);
                this.nextChannelId += 2;
            }

            return result;
        }

        public IAsyncResult BeginAcceptChannel(AsyncCallback callback, object state)
        {
            AcceptAsyncResult completionHandle = new AcceptAsyncResult(callback, state, this);
            this.actorManager.SendMessage(this.transportReceivingActor, new AcceptRequestMessage(completionHandle));
            return completionHandle;
        }

        public Channel EndAcceptChannel(IAsyncResult ar)
        {
            return AcceptAsyncResult.End(ar, this, "Accept");
        }

        public void KeepAlive()
        {
            this.EndKeepAlive(this.BeginKeepAlive(null, null));
        }

        public Task KeepAliveAsync()
        {
            return new TaskFactory().FromAsync(this.BeginKeepAlive, this.EndKeepAlive, null);
        }

        public IAsyncResult BeginKeepAlive(AsyncCallback callback, object state)
        {
            return this.BeginWrite(0, new byte[0], 0, 0, callback, state);
        }

        public void EndKeepAlive(IAsyncResult ar)
        {
            this.EndWrite(ar);
        }

        internal IAsyncResult BeginWrite(int channelId, byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            // Framing logic happen here!
            List<ArraySegment<byte>> data = new List<ArraySegment<byte>>();
            int remainingLength = count;
            int encodingPointer = offset;
            while (remainingLength > 0)
            {
                if (remainingLength >= Constants.FrameSize)
                {
                    // A full frame can be sent
                    data.Add(new ArraySegment<byte>(new FrameHeader(Constants.FrameSize, channelId).Encode()));
                    data.Add(new ArraySegment<byte>(buffer, encodingPointer, Constants.FrameSize));
                    encodingPointer += Constants.FrameSize;
                    remainingLength -= Constants.FrameSize;
                }
                else
                {
                    // A partial frame can be sent
                    data.Add(new ArraySegment<byte>(new FrameHeader(remainingLength, channelId).Encode()));
                    data.Add(new ArraySegment<byte>(buffer, encodingPointer, remainingLength));
                    encodingPointer += remainingLength;
                    remainingLength -= remainingLength;
                }
            }

            // Handling the close packet
            if (count == 0)
            {
                data.Add(new ArraySegment<byte>(new FrameHeader(0, channelId).Encode()));
            }

            WriteAsyncResult asyncResult = new WriteAsyncResult(callback, state, this);
            this.actorManager.SendMessage(this.senderActor, new SendRequestMessage(asyncResult, data));
            return asyncResult;
        }

        internal void EndWrite(IAsyncResult asyncResult)
        {
            WriteAsyncResult.End(asyncResult, this, "Write");
        }

        internal IAsyncResult BeginRead(int channelId, byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            ReadAsyncResult completionHandle = new ReadAsyncResult(callback, state, this);
            this.actorManager.SendMessage(this.transportReceivingActor, new ReadRequestMessage(channelId, buffer, offset, count, completionHandle));
            return completionHandle;
        }

        internal int EndRead(IAsyncResult ar)
        {
            return ReadAsyncResult.End(ar, this, "Read");
        }

        private static void OnTransportReadCompletedCallback(IAsyncResult ar)
        {
            Connection thisPtr = (Connection)ar.AsyncState;
            int bytesRead = thisPtr.socket.EndReceive(ar);
            thisPtr.OnTransportReadCompleted(bytesRead);
        }

        private static void OnTransportWriteCompletedCallback(IAsyncResult ar)
        {
            Connection thisPtr = (Connection)ar.AsyncState;
            int bytesWritten = thisPtr.socket.EndSend(ar);
            thisPtr.OnTransportWriteCompleted(bytesWritten);
        }

        private void TransportWrite(List<ArraySegment<byte>> data)
        {
            this.socket.BeginSend(data, SocketFlags.None, OnTransportWriteCompletedCallback, this);
        }

        private void OnTransportWriteCompleted(int bytesWritten)
        {
            this.actorManager.SendMessage(this.senderActor, new TransportWriteCompletionMessage(bytesWritten));
        }

        private void TransportRead(byte[] buffer, int bufferStart)
        {
            this.socket.BeginReceive(buffer, bufferStart, buffer.Length - bufferStart, SocketFlags.None, OnTransportReadCompletedCallback, this);
        }

        private void OnTransportReadCompleted(int bytesRead)
        {
            this.actorManager.SendMessage(this.transportReceivingActor, new TransportReadCompletionMessage(bytesRead));
        }

        private class SenderActor : Actor
        {
            private Connection parent;
            private Queue<SegmentHandlePair> toSend;
            private bool transportSendInProgress;

            public SenderActor(Connection parent, ActorManager actorManager)
                : base(actorManager)
            {
                this.parent = parent;
                this.toSend = new Queue<SegmentHandlePair>();
                this.transportSendInProgress = false;
            }

            public override ActorContinuation OnReceiveMessage(IMessage message)
            {
                if (message is StartMessage)
                {
                    return ActorContinuation.BlockOnReceive;
                }
                else if (message is SendRequestMessage)
                {
                    return this.OnSendRequestMessage((SendRequestMessage)message);
                }
                else if (message is TransportWriteCompletionMessage)
                {   
                    return this.OnTransportWriteCompletionMessage((TransportWriteCompletionMessage)message);
                }
                else
                {
                    throw new Exception("Unrecognized Message Format");
                }
            }

            private ActorContinuation OnTransportWriteCompletionMessage(TransportWriteCompletionMessage transportWriteCompletionMessage)
            {
                int written = transportWriteCompletionMessage.BytesWritten;
                int accounted = 0;
                int unaccounted = written;
                List<WriteAsyncResult> completedHandles = new List<WriteAsyncResult>();
                while (accounted < written)
                {
                    SegmentHandlePair first = this.toSend.Peek();
                    if (unaccounted >= first.Segment.Count)
                    {
                        // the segment is completely written
                        this.toSend.Dequeue();
                        if (first.CompletionHandle != null)
                        {
                            completedHandles.Add(first.CompletionHandle);
                        }

                        accounted += first.Segment.Count;
                        unaccounted -= first.Segment.Count;
                    }
                    else
                    {
                        first.Segment = new ArraySegment<byte>(first.Segment.Array, first.Segment.Offset + unaccounted, first.Segment.Count - unaccounted);
                        accounted += unaccounted;
                        unaccounted -= unaccounted;
                    }
                }

                if (this.toSend.Count > 0)
                {
                    this.parent.TransportWrite(this.toSend.Select(t => t.Segment).ToList());
                }
                else
                {
                    this.transportSendInProgress = false;
                }

                foreach (WriteAsyncResult asyncResult in completedHandles)
                {
                    asyncResult.NotifyCompleted();
                }

                return ActorContinuation.BlockOnReceive;
            }

            private ActorContinuation OnSendRequestMessage(SendRequestMessage sendRequestMessage)
            {
                SegmentHandlePair last = null;
                foreach (ArraySegment<byte> segment in sendRequestMessage.Segments)
                {
                    last = new SegmentHandlePair { Segment = segment, CompletionHandle = null };
                    this.toSend.Enqueue(last);
                }

                last.CompletionHandle = sendRequestMessage.CompletionHandle;

                if (!this.transportSendInProgress)
                {
                    this.transportSendInProgress = true;
                    this.parent.TransportWrite(this.toSend.Select(t => t.Segment).ToList());
                }

                return ActorContinuation.BlockOnReceive;
            }

            private class SegmentHandlePair
            {
                public ArraySegment<byte> Segment { get; set; }

                public WriteAsyncResult CompletionHandle { get; set; }
            }
        }

        private class SendRequestMessage : IMessage
        {
            private WriteAsyncResult completionHandle;
            private List<ArraySegment<byte>> segments;

            public SendRequestMessage(WriteAsyncResult completionHandle, List<ArraySegment<byte>> segments)
            {
                this.completionHandle = completionHandle;
                this.segments = segments;
            }

            public WriteAsyncResult CompletionHandle
            {
                get { return this.completionHandle; }
            }

            public List<ArraySegment<byte>> Segments
            {
                get { return this.segments; }
            }
        }

        private class TransportWriteCompletionMessage : IMessage
        {
            private int bytesWritten;

            public TransportWriteCompletionMessage(int bytesWritten)
            {
                this.bytesWritten = bytesWritten;
            }

            public int BytesWritten
            {
                get { return this.bytesWritten; }
            }
        }

        private class TransportReceivingActor : Actor
        {
            private Connection parent;
            private byte[] buffer;
            private int bufferStart;
            private DecodingState decodingState;
            private Queue<AcceptAsyncResult> pendingAcceptRequests;
            private Queue<Channel> pendingAcceptChannels;
            private Dictionary<int, ChannelReceivingActor> channelReceivingActors;
            private int unreadBytes;
            private bool bufferFull;

            public TransportReceivingActor(Connection parent, ActorManager actorManager)
                : base(actorManager)
            {
                this.parent = parent;
                this.buffer = new byte[Constants.BufferSize];
                this.bufferStart = 0;
                this.bufferFull = false;
                this.decodingState = new DecodingState();
                this.pendingAcceptRequests = new Queue<AcceptAsyncResult>();
                this.pendingAcceptChannels = new Queue<Channel>();
                this.channelReceivingActors = new Dictionary<int, ChannelReceivingActor>();
            }

            public override ActorContinuation OnReceiveMessage(IMessage message)
            {
                if (message is StartMessage)
                {
                    this.parent.TransportRead(this.buffer, this.bufferStart);
                    return ActorContinuation.BlockOnReceive;
                }
                else if (message is AcceptRequestMessage)
                {
                    return this.OnAcceptRequestMessage((AcceptRequestMessage)message);
                }
                else if (message is TransportReadCompletionMessage)
                {
                    return this.OnTransportReadCompletionMessage((TransportReadCompletionMessage)message);
                }
                else if (message is ReadRequestMessage)
                {
                    return this.OnReadRequestMessage((ReadRequestMessage)message);
                }
                else if (message is DataConsumedMessage)
                {
                    return this.OnDataConsumedMessage((DataConsumedMessage)message);
                }
                else if (message is ChannelCreatedMessage)
                {
                    return this.OnChannelCreatedMessage((ChannelCreatedMessage)message);
                }
                else
                {
                    throw new Exception("Unrecognized Message Format");
                }
            }

            private ActorContinuation OnTransportReadCompletionMessage(TransportReadCompletionMessage transportReadCompletionMessage)
            {
                int bytesRead = transportReadCompletionMessage.BytesRead;

                int decodingBufferStart = this.bufferStart;
                int decodingBufferSize = bytesRead;

                int decodingPointer = decodingBufferStart;
                int sizeRemaining = decodingBufferSize;

                // Buffering the decoded payload so that we don't notify receiver before we are done with the parsing
                Dictionary<int, List<ArraySegment<byte>>> decodedPayloadLists = new Dictionary<int, List<ArraySegment<byte>>>();

                while (sizeRemaining > 0)
                {
                    if (this.decodingState.LastFrameHeaderIncomplete)
                    {
                        this.decodingState.LastFrameHeaderIncomplete = false;
                        while (this.decodingState.LastIncompleteHeader.Count < Constants.HeaderSize && sizeRemaining > 0)
                        {
                            this.decodingState.LastIncompleteHeader.Add(this.buffer[decodingPointer]);
                            decodingPointer++;
                            sizeRemaining--;
                        }

                        if (this.decodingState.LastIncompleteHeader.Count == Constants.HeaderSize)
                        {
                            FrameHeader header = FrameHeader.Decode(this.decodingState.LastIncompleteHeader);

                            int consumed = this.DecodePayload(decodingPointer, sizeRemaining, header, decodedPayloadLists);
                            decodingPointer += consumed;
                            sizeRemaining -= consumed;
                        }
                        else
                        {
                            this.decodingState.LastFrameHeaderIncomplete = true;
                        }
                    }
                    else if (this.decodingState.LastFramePayloadIncomplete)
                    {
                        this.decodingState.LastFramePayloadIncomplete = false;
                        int consumed = this.DecodePayload(decodingPointer, sizeRemaining, this.decodingState.LastIncompletePayloadHeader, decodedPayloadLists);
                        decodingPointer += consumed;
                        sizeRemaining -= consumed;
                    }
                    else
                    {
                        // Can I grab the whole header?
                        if (sizeRemaining >= Constants.HeaderSize)
                        {
                            FrameHeader header = FrameHeader.Decode(new ArraySegment<byte>(this.buffer, decodingPointer, Constants.HeaderSize));

                            decodingPointer += Constants.HeaderSize;
                            sizeRemaining -= Constants.HeaderSize;

                            int consumed = this.DecodePayload(decodingPointer, sizeRemaining, header, decodedPayloadLists);

                            decodingPointer += consumed;
                            sizeRemaining -= consumed;
                        }
                        else
                        {
                            this.decodingState.LastFrameHeaderIncomplete = true;
                            this.decodingState.LastIncompleteHeader = new List<byte>();
                            while (sizeRemaining > 0)
                            {
                                this.decodingState.LastIncompleteHeader.Add(this.buffer[decodingPointer]);
                                decodingPointer++;
                                sizeRemaining--;
                            }
                        }
                    }
                }

                foreach (KeyValuePair<int, List<ArraySegment<byte>>> decodedPayloadList in decodedPayloadLists)
                {
                    int channelId = decodedPayloadList.Key;
                    ChannelReceivingActor channelReceivingActor;
                    if (!this.channelReceivingActors.TryGetValue(channelId, out channelReceivingActor))
                    {
                        channelReceivingActor = new ChannelReceivingActor(this, this.ActorManager);
                        this.ActorManager.RunActor(channelReceivingActor);
                        this.channelReceivingActors.Add(channelId, channelReceivingActor);
                        this.pendingAcceptChannels.Enqueue(new Channel(this.parent, channelId));
                        this.TrySatisfyAcceptRequest();
                    }

                    // Book keeping the amount of data held unread
                    this.unreadBytes += decodedPayloadList.Value.Select(t => t.Count).Sum();
                    this.Send(channelReceivingActor, new DataArrivedMessage(decodedPayloadList.Value));
                }

                if (bytesRead == 0)
                {
                    // The other side closed the connection
                    // TODO: Handle the connection close situation 
                    return ActorContinuation.BlockOnReceive;
                }

                if (this.bufferStart + bytesRead < Constants.BufferSize)
                {
                    this.bufferStart = this.bufferStart + bytesRead;
                    this.parent.TransportRead(this.buffer, this.bufferStart);
                }
                else
                {
                    this.bufferFull = true;
                    this.TryReuseBuffer();
                }

                return ActorContinuation.BlockOnReceive;
            }

            private void TrySatisfyAcceptRequest()
            {
                while (this.pendingAcceptRequests.Count > 0 && this.pendingAcceptChannels.Count > 0)
                {
                    AcceptAsyncResult pendingAcceptRequest = this.pendingAcceptRequests.Dequeue();
                    Channel pendingAcceptChannel = this.pendingAcceptChannels.Dequeue();
                    pendingAcceptRequest.NotifyCompleted(pendingAcceptChannel);
                }
            }

            private ActorContinuation OnAcceptRequestMessage(AcceptRequestMessage acceptMessage)
            {
                this.pendingAcceptRequests.Enqueue(acceptMessage.CompletionHandle);
                this.TrySatisfyAcceptRequest();
                return ActorContinuation.BlockOnReceive;
            }

            private ActorContinuation OnReadRequestMessage(ReadRequestMessage readRequestMessage)
            {
                ChannelReceivingActor channelReceivingActor;
                if (this.channelReceivingActors.TryGetValue(readRequestMessage.ChannelId, out channelReceivingActor))
                {
                    this.Send(channelReceivingActor, readRequestMessage);
                }
                else
                {
                    throw new Exception("Receiving read request from unknown channel");
                }

                return ActorContinuation.BlockOnReceive;
            }

            private ActorContinuation OnDataConsumedMessage(DataConsumedMessage dataConsumedMessage)
            {
                this.unreadBytes -= dataConsumedMessage.BytesConsumed;
                this.TryReuseBuffer();

                return ActorContinuation.BlockOnReceive;
            }

            private void TryReuseBuffer()
            {
                if (this.unreadBytes == 0 && this.bufferFull)
                {
                    this.bufferStart = 0;
                    this.bufferFull = false;
                    this.parent.TransportRead(this.buffer, this.bufferStart);
                }
            }

            private ActorContinuation OnChannelCreatedMessage(ChannelCreatedMessage channelCreatedMessage)
            {
                ChannelReceivingActor channelReceivingActor = new ChannelReceivingActor(this, this.ActorManager);
                this.channelReceivingActors.Add(channelCreatedMessage.ChannelId, channelReceivingActor);
                this.ActorManager.RunActor(channelReceivingActor);
                return ActorContinuation.BlockOnReceive;
            }

            private int DecodePayload(int decodingPointer, int sizeRemaining, FrameHeader header, Dictionary<int, List<ArraySegment<byte>>> decodedPayloadLists)
            {
                int length = header.Length;
                int channelId = header.ChannelId;
                int consumed;

                // Can I grab the whole payload?
                if (sizeRemaining >= length)
                {
                    ArraySegment<byte> payload = new ArraySegment<byte>(this.buffer, decodingPointer, length);
                    this.EnqueuePayload(channelId, payload, decodedPayloadLists);
                    consumed = length;
                }
                else
                {
                    if (sizeRemaining > 0)
                    {
                        ArraySegment<byte> payload = new ArraySegment<byte>(this.buffer, decodingPointer, sizeRemaining);
                        this.EnqueuePayload(channelId, payload, decodedPayloadLists);
                    }

                    this.decodingState.LastFramePayloadIncomplete = true;
                    this.decodingState.LastIncompletePayloadHeader = new FrameHeader(length - sizeRemaining, channelId);

                    consumed = sizeRemaining;
                }

                return consumed;
            }

            private void EnqueuePayload(int channelId, ArraySegment<byte> payload, Dictionary<int, List<ArraySegment<byte>>> decodedPayloadLists)
            {
                if (channelId != 0)
                {
                    List<ArraySegment<byte>> decodedPayloadList;
                    if (!decodedPayloadLists.TryGetValue(channelId, out decodedPayloadList))
                    {
                        decodedPayloadList = new List<ArraySegment<byte>>();
                        decodedPayloadLists.Add(channelId, decodedPayloadList);
                    }

                    decodedPayloadList.Add(payload);
                }
                else
                {
                    // Got a KeepAlive request - safe to ignore
                }
            }

            private class DecodingState
            {
                public bool LastFramePayloadIncomplete { get; set; }

                public FrameHeader LastIncompletePayloadHeader { get; set; }

                public bool LastFrameHeaderIncomplete { get; set; }

                public List<byte> LastIncompleteHeader { get; set; }
            }
        }

        private class ChannelReceivingActor : Actor
        {
            private TransportReceivingActor parent;
            private ReadRequestMessage readRequest;
            private Queue<SegmentWrapper> data;
            private bool accepted;

            public ChannelReceivingActor(TransportReceivingActor parent, ActorManager actorManager)
                : base(actorManager)
            {
                this.parent = parent;
                this.data = new Queue<SegmentWrapper>();
                this.accepted = false;
            }

            public override ActorContinuation OnReceiveMessage(IMessage message)
            {
                if (message is StartMessage)
                {
                    return ActorContinuation.BlockOnReceive;
                }
                else if (message is DataArrivedMessage)
                {
                    return this.OnDataArrivedMessage((DataArrivedMessage)message);
                }
                else if (message is ReadRequestMessage)
                {
                    return this.OnReadRequestMessage((ReadRequestMessage)message);
                }
                else
                {
                    throw new Exception("Unrecognized Message Format");
                }
            }

            private ActorContinuation OnDataArrivedMessage(DataArrivedMessage dataArrivedMessage)
            {
                foreach (ArraySegment<byte> segment in dataArrivedMessage.DataRead)
                {
                    this.data.Enqueue(new SegmentWrapper { Segment = segment });
                }

                this.TrySatisfyReadRequest();
                return ActorContinuation.BlockOnReceive;
            }

            private ActorContinuation OnReadRequestMessage(ReadRequestMessage readRequestMessage)
            {
                if (this.readRequest == null)
                {
                    this.readRequest = readRequestMessage;
                    this.TrySatisfyReadRequest();
                }
                else
                {
                    throw new Exception("Receiving read request while existing read request haven't completed yet");
                }

                return ActorContinuation.BlockOnReceive;
            }

            private void TrySatisfyReadRequest()
            {
                if (this.readRequest != null)
                {
                    int copiedCount = 0;
                    bool isEof = false;
                    int spaceToFill = this.readRequest.Count;
                    while (spaceToFill > 0 && this.data.Count > 0)
                    {
                        SegmentWrapper firstSegmentWrapper = this.data.Peek();
                        ArraySegment<byte> firstSegment = firstSegmentWrapper.Segment;
                        int bytesToCopy = Math.Min(firstSegment.Count, spaceToFill);
                        if (firstSegment.Count == 0)
                        {
                            if (this.accepted)
                            {
                                isEof = true;
                            }
                            else
                            {
                                this.accepted = true;
                            }
                        }

                        if (bytesToCopy == firstSegment.Count)
                        {
                            this.data.Dequeue();
                        }
                        else
                        {
                            firstSegmentWrapper.Segment = new ArraySegment<byte>(firstSegment.Array, firstSegment.Offset + bytesToCopy, firstSegment.Count - bytesToCopy);
                        }

                        Array.Copy(firstSegment.Array, firstSegment.Offset, this.readRequest.Buffer, this.readRequest.Offset + copiedCount, bytesToCopy);
                        copiedCount += bytesToCopy;
                        spaceToFill -= bytesToCopy;
                    }

                    if (copiedCount > 0 || isEof)
                    {
                        this.Send(this.parent, new DataConsumedMessage(copiedCount));
                        ReadAsyncResult completionHandle = this.readRequest.CompletionHandle;
                        this.readRequest = null;
                        completionHandle.NotifyCompleted(copiedCount);
                    }

                    // TODO: Here is when a channel is closed by the other side 
                }
            }

            // Intentionally wrap ArraySegment so that it can be changed in partial read case
            private class SegmentWrapper
            {
                public ArraySegment<byte> Segment { get; set; }
            }
        }

        private class ReadRequestMessage : IMessage
        {
            private int channelId;
            private ReadAsyncResult completionHandle;
            private byte[] buffer;
            private int offset;
            private int count;

            public ReadRequestMessage(int channelId, byte[] buffer, int offset, int count, ReadAsyncResult completionHandle)
            {
                this.channelId = channelId;
                this.buffer = buffer;
                this.offset = offset;
                this.count = count;
                this.completionHandle = completionHandle;
            }

            public int ChannelId
            {
                get { return this.channelId; }
            }

            public byte[] Buffer
            {
                get { return this.buffer; }
            }

            public int Offset
            {
                get { return this.offset; }
            }

            public int Count
            {
                get { return this.count; }
            }

            public ReadAsyncResult CompletionHandle
            {
                get { return this.completionHandle; }
            }
        }

        private class DataArrivedMessage : IMessage
        {
            private List<ArraySegment<byte>> dataRead;

            public DataArrivedMessage(List<ArraySegment<byte>> dataRead)
            {
                this.dataRead = dataRead;
            }

            public List<ArraySegment<byte>> DataRead
            {
                get { return this.dataRead; }
            }
        }

        private class DataConsumedMessage : IMessage
        {
            private int bytesConsumed;

            public DataConsumedMessage(int bytesConsumed)
            {
                this.bytesConsumed = bytesConsumed;
            }

            public int BytesConsumed
            {
                get { return this.bytesConsumed; }
            }
        }

        private class AcceptRequestMessage : IMessage
        {
            private AcceptAsyncResult completionHandle;

            public AcceptRequestMessage(AcceptAsyncResult completionHandle)
            {
                this.completionHandle = completionHandle;
            }

            public AcceptAsyncResult CompletionHandle
            {
                get { return this.completionHandle; }
            }
        }

        private class TransportReadCompletionMessage : IMessage
        {
            private int bytesRead;

            public TransportReadCompletionMessage(int bytesRead)
            {
                this.bytesRead = bytesRead;
            }

            public int BytesRead
            {
                get { return this.bytesRead; }
            }
        }

        private class ChannelCreatedMessage : IMessage
        {
            private int channelId;

            public ChannelCreatedMessage(int channelId)
            {
                this.channelId = channelId;
            }

            public int ChannelId
            {
                get { return this.channelId; }
            }
        }
    }
}
