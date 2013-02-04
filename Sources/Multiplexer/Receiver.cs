namespace Multiplexer
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Text;
	using System.Threading;

	internal class Receiver
	{
		private readonly object fillLock = new object();

		private IFrameFragmentReader frameFragmentReader;
		private Queue<ArraySegment<byte>> dataQueue;
		private ArraySegment<byte>? lastBlock;

		private ReadAsyncResult readRequest;

		internal Receiver(IFrameFragmentReader reader, int streamId)
		{
			this.frameFragmentReader = reader;
			this.StreamId = streamId;
			this.dataQueue = new Queue<ArraySegment<byte>>();
		}

		internal int StreamId { get; private set; }

		internal IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			if (this.readRequest != null)
			{
				Logger.Log("Multiple pending read request detected!");
			}
			ReadAsyncResult currentResult = new ReadAsyncResult(this, buffer, offset, count, callback, state);
			bool shouldComplete = false;
			if (IsLocked(fillLock))
			{
				Logger.Log("Oh - got fillLock already - no wonder it stinks" + new StackTrace());
			}
			lock(fillLock)
			{
				if (currentResult.CanComplete())
				{
					shouldComplete = true;
				}
				else
				{
					this.readRequest = currentResult;
				}
			}
			if (shouldComplete)
			{
				currentResult.Complete(true);
			}
			return currentResult;
		}

		internal int EndRead(IAsyncResult ar)
		{
			return AsyncResult<int>.End(ar, this, "Read");
		}

		internal int NonBlockingFillBuffer(byte[] buffer, int offset, int count, out int numSegmentsCompleted)
		{
			int byteCopied = 0;
			numSegmentsCompleted = 0;

			int sizeRemaining = count;
			int bufferPointer = offset;
			if (lastBlock.HasValue)
			{
				int lengthToCopy = lastBlock.Value.Count;
				bool lastBlockCompleted = false;
				if (lengthToCopy > count)
				{
					lengthToCopy = count;
				}
				else
				{
					lastBlockCompleted = true;
				}

				Buffer.BlockCopy(lastBlock.Value.Array, lastBlock.Value.Offset, buffer, bufferPointer, lengthToCopy);
				if (lastBlockCompleted)
				{
					numSegmentsCompleted++;
					lastBlock = null;
				}

				sizeRemaining -= lengthToCopy;
				byteCopied += lengthToCopy;
				bufferPointer += lengthToCopy;
			}

			while (sizeRemaining > 0)
			{
				ArraySegment<byte> currentBlock;
				if (this.dataQueue.Count > 0)
				{
					if (Logger.Tracing)
					{
						Logger.Log("Entered NonBlockingFillBuffer -> Dequeue returned true");
					}
					currentBlock = this.dataQueue.Dequeue();
					int lengthToCopy = currentBlock.Count;
					if (sizeRemaining < currentBlock.Count)
					{
						lengthToCopy = sizeRemaining;
					}

					Buffer.BlockCopy(currentBlock.Array, currentBlock.Offset, buffer, bufferPointer, lengthToCopy);
					sizeRemaining -= lengthToCopy;
					byteCopied += lengthToCopy;
					bufferPointer += lengthToCopy;

					if (lengthToCopy < currentBlock.Count)
					{
						lastBlock = new ArraySegment<byte>(currentBlock.Array, currentBlock.Offset + lengthToCopy, currentBlock.Count - lengthToCopy);
					}
					else
					{
						numSegmentsCompleted++;
					}
				}
				else
				{
					break;
				}
			}

			Logger.LogStream(string.Format("Stream {0} received", this.StreamId), new ArraySegment<byte>(buffer, offset, byteCopied));
			
			return byteCopied;
		}

		internal void NotifySegmentsCompleted(int numSegmentsCompleted)
		{
			for (int i = 0; i < numSegmentsCompleted; i++)
			{
				this.frameFragmentReader.OnSegmentCompleted();
			}
		}

		bool IsLocked(object o)   
		{   
			if (!Monitor.TryEnter(o))
			{
				return true;   
			}
			Monitor.Exit(o);   
			return false;   
		}  

		internal void Enqueue(ArraySegment<byte> payload)
		{
			ReadAsyncResult pendingReadRequest = null;
			bool shouldComplete = false;
			if (IsLocked(fillLock))
			{
				Logger.Log("Oh - got fillLock already in enqueue - no wonder it stinks" + new StackTrace());
			}
			lock (fillLock)
			{
				this.dataQueue.Enqueue(payload);
				Logger.LogStream(string.Format("Stream {0} enqueued", this.StreamId), payload);
				pendingReadRequest = Interlocked.Exchange(ref this.readRequest, null);
				if (pendingReadRequest != null)
				{
					if (pendingReadRequest.CanComplete())
					{
						shouldComplete = true;
					}
				}
			}
			if (shouldComplete)
			{
				pendingReadRequest.Complete(false);
			}
		}
	}
}