namespace Multiplexer
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Text;
	using System.Threading;

	internal class FrameFragmentReader : IFrameFragmentReader
	{
		private ITransportReader transportReader;
		private byte[] buffer;

		// These fields could be accessed by multiple threads at the same time using Interlocked.
		private int segmentCompletedShouldTriggerRead;
		private int unconsumedSegmentCount;

		// Frame decoding state that need to go cross trunks
		private DecodingState decodingState;

		private ConcurrentQueue<Receiver> newReceivers;
		private ConcurrentQueue<AcceptAsyncResult> pendingAcceptRequests;
		private ConcurrentDictionary<int, Receiver> receivers;

		private bool shouldClose;

		private int decodingBlockCallCount;

		private class ReadState
		{
			public FrameFragmentReader ThisPtr { get; set; }
			public int BufferStart { get; set; }
		}

		private class DecodingState
		{
			public bool LastFramePayloadIncomplete { get; set; }
			public FrameHeader LastIncompletePayloadHeader { get; set; }
			public bool LastFrameHeaderIncomplete { get; set; }
			public List<byte> LastIncompleteHeader { get; set; }
		}

		internal FrameFragmentReader(ITransportReader transportReader)
		{
			this.transportReader = transportReader;
			this.buffer = new byte[Constants.DecodingBufferSize];
			this.receivers = new ConcurrentDictionary<int, Receiver>();
			this.newReceivers = new ConcurrentQueue<Receiver>();
			this.pendingAcceptRequests = new ConcurrentQueue<AcceptAsyncResult>();
			this.decodingState = new DecodingState();
			this.BeginFillBuffer(0);
		}

		public void OnSegmentCompleted()
		{
			// If the reading of network is stopped because buffer is full, and all segments created are comsumed, then read again
			if (Interlocked.Decrement(ref this.unconsumedSegmentCount) == 0)
			{
				if (Interlocked.CompareExchange(ref this.segmentCompletedShouldTriggerRead, 0, 1) == 1)
				{
					this.BeginFillBuffer(0);
				}
			}
		}

		internal IAsyncResult BeginAccept(AsyncCallback callback, object state)
		{
			AcceptAsyncResult result = new AcceptAsyncResult(this, callback, state);
			Receiver newReceiver;
			if (newReceivers.TryDequeue(out newReceiver))
			{
				result.OnReceiverAvailable(newReceiver, true);
			}
			else
			{
				this.pendingAcceptRequests.Enqueue(result);
			}
			return result;
		}

		internal Receiver EndAccept(IAsyncResult ar)
		{
			return AsyncResult<Receiver>.End(ar, this, "Accept");
		}

		internal Receiver CreateReceiver(int nextStreamId)
		{
			Receiver newReceiver = new Receiver(this, nextStreamId);
			// TODO: Possible to fail at all?
			this.receivers.TryAdd(nextStreamId, newReceiver);
			return newReceiver;
		}

		private void BeginFillBuffer(int bufferStart)
		{			
			if (!shouldClose)
			{
				IAsyncResult ar = this.transportReader.BeginRead(buffer, bufferStart, this.buffer.Length - bufferStart, OnTransportReadCallback, new ReadState { ThisPtr = this, BufferStart = bufferStart});
				if (ar.CompletedSynchronously)
				{
					int byteRead = this.transportReader.EndRead(ar);
					this.OnTransportRead(byteRead, bufferStart);
				}
			}
			else
			{
				this.transportReader.Close();
			}
		}

		private void OnTransportRead(int byteRead, int bufferStart)
		{
			if (Interlocked.Increment(ref this.decodingBlockCallCount) != 1)
			{
				Console.WriteLine("Two decoding blocks happen together");
			}

			Logger.LogStream("Transport Read", new ArraySegment<byte>(this.buffer, bufferStart, byteRead));

			int decodingBufferStart = bufferStart;
			int decodingBufferSize = byteRead;

			// Decoding the buffer here to make sure the decoding process is not run concurrently
			int decodingPointer = decodingBufferStart;
			int sizeRemaining = decodingBufferSize;

			// Buffering the decoded payload so that we don't notify receiver before we are done with the parsing
			Dictionary<int, List<ArraySegment<byte>>> decodedPayloadLists = new Dictionary<int, List<ArraySegment<byte>>>();

			while (sizeRemaining > 0)
			{
				if (this.decodingState.LastFrameHeaderIncomplete)
				{
					this.decodingState.LastFrameHeaderIncomplete = false;
					while (this.decodingState.LastIncompleteHeader.Count < Constants.HeaderLength && sizeRemaining > 0)
					{
						this.decodingState.LastIncompleteHeader.Add(this.buffer[decodingPointer]);
						decodingPointer++;
						sizeRemaining--;
					}
					if (this.decodingState.LastIncompleteHeader.Count == Constants.HeaderLength)
					{
						FrameHeader header = FrameHeader.Decode(this.decodingState.LastIncompleteHeader);

						int consumed = DecodePayload(decodingPointer, sizeRemaining, header, decodedPayloadLists);
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
					int consumed = DecodePayload(decodingPointer, sizeRemaining, this.decodingState.LastIncompletePayloadHeader, decodedPayloadLists);
					decodingPointer += consumed;
					sizeRemaining -= consumed;
				}
				else
				{
					// Can I grab the whole header?
					if (sizeRemaining >= Constants.HeaderLength)
					{
						FrameHeader header = FrameHeader.Decode(new ArraySegment<byte>(this.buffer, decodingPointer, Constants.HeaderLength));

						decodingPointer += Constants.HeaderLength;
						sizeRemaining -= Constants.HeaderLength;

						int consumed = DecodePayload(decodingPointer, sizeRemaining, header, decodedPayloadLists);

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

			Interlocked.Decrement(ref this.decodingBlockCallCount);

			// Done Parsing: Notify any pending accepts			
			while (pendingAcceptRequests.Count > 0 && this.newReceivers.Count > 0)
			{
				AcceptAsyncResult pendingAcceptRequest;
				Receiver newReceiver;

				bool dequeueResult = pendingAcceptRequests.TryDequeue(out pendingAcceptRequest);
				Debug.Assert(dequeueResult, "There is only ONE thread doing dequeue, we already checked the queue has element in it");

				dequeueResult = newReceivers.TryDequeue(out newReceiver);
				Debug.Assert(dequeueResult, "There is only ONE thread doing dequeue, we already checked the queue has element in it");

				pendingAcceptRequest.OnReceiverAvailable(newReceiver, false);
			}

			if (decodingPointer == buffer.Length)
			{
				decodingPointer = 0;
				if (this.unconsumedSegmentCount > 0)
				{
					Interlocked.Exchange(ref this.segmentCompletedShouldTriggerRead, 1);
				}
			}
			int mySegmentCompletedShouldTriggerRead = this.segmentCompletedShouldTriggerRead;

			// Done Parsing: Notify any receivers
			foreach (KeyValuePair<int, List<ArraySegment<byte>>> decodedPayloadList in decodedPayloadLists)
			{
				Receiver receiver = this.receivers[decodedPayloadList.Key];
				foreach (ArraySegment<byte> decodedPayload in decodedPayloadList.Value)
				{
					receiver.Enqueue(decodedPayload);
				}
			}

			if (mySegmentCompletedShouldTriggerRead == 0)
			{
				this.BeginFillBuffer(decodingPointer);
			}
		}

		private int DecodePayload(int decodingPointer, int sizeRemaining, FrameHeader header, Dictionary<int, List<ArraySegment<byte>>> decodedPayloadLists)
		{
			int length = header.Length;
			int streamId = header.StreamId;
			int consumed;

			if (streamId == 0)
			{
				this.shouldClose = true;
			}

			// Can I grab the whole payload?
			if (sizeRemaining >= length)
			{
				if (length > 0)
				{
					ArraySegment<byte> payload = new ArraySegment<byte>(buffer, decodingPointer, length);
					EnqueuePayload(streamId, payload, decodedPayloadLists);
				}

				consumed = length;
			}
			else
			{
				if (sizeRemaining > 0)
				{
					ArraySegment<byte> payload = new ArraySegment<byte>(buffer, decodingPointer, sizeRemaining);
					EnqueuePayload(streamId, payload, decodedPayloadLists);
				}
				this.decodingState.LastFramePayloadIncomplete = true;
				this.decodingState.LastIncompletePayloadHeader = new FrameHeader(length - sizeRemaining, streamId);

				consumed = sizeRemaining;
			}
			return consumed;
		}

		private void EnqueuePayload(int streamId, ArraySegment<byte> payload, Dictionary<int, List<ArraySegment<byte>>> decodedPayloadLists)
		{
			if (streamId != 0)
			{
				Receiver receiver;
				if (!this.receivers.TryGetValue(streamId, out receiver))
				{
					receiver = new Receiver(this, streamId);
					// TODO, possible to fail at all?
					this.receivers.TryAdd(streamId, receiver);
					this.newReceivers.Enqueue(receiver);
				}

				List<ArraySegment<byte>> decodedPayloadList;
				if (!decodedPayloadLists.TryGetValue(streamId, out decodedPayloadList))
				{
					decodedPayloadList = new List<ArraySegment<byte>>();
					decodedPayloadLists.Add(streamId, decodedPayloadList);
				}
				decodedPayloadList.Add(payload);
				Interlocked.Increment(ref this.unconsumedSegmentCount);
			}
		}

		private static void OnTransportReadCallback(IAsyncResult ar)
		{
			if (ar.CompletedSynchronously)
			{
				return;
			}

			ReadState readState = (ReadState)ar.AsyncState;
			FrameFragmentReader thisPtr = readState.ThisPtr;
			int byteRead = thisPtr.transportReader.EndRead(ar);
			thisPtr.OnTransportRead(byteRead, readState.BufferStart);
		}
	}
}
