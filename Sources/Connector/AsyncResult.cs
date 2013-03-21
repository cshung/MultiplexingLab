namespace Connector
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    internal class AsyncResult : IAsyncResult
    {
        // Defining what does it mean by the state 
        private const int PendingState = 0;
        private const int CompletedSynchronouslyState = 1;
        private const int CompletedAsynchronouslyState = 2;

        // Fields set at construction which never change while operation is pending
        private readonly AsyncCallback asyncCallback;
        private readonly object asyncState;

        // Fields set at construction which do change after operation completes
        private int completedState = PendingState;

        // Field that may or may not get set depending on usage
        private ManualResetEvent asyncWaitHandle;

        // Fields set when operation completes
        private Exception exception;

        /// <summary>
        /// The object which started the operation.
        /// </summary>
        private object owner;

        /// <summary>
        /// Used to verify BeginXXX and EndXXX calls match.
        /// </summary>
        private string operationId;

        protected AsyncResult(AsyncCallback asyncCallback, object state, object owner, string operationId)
        {
            this.asyncCallback = asyncCallback;
            this.asyncState = state;
            this.owner = owner;
            this.operationId = string.IsNullOrEmpty(operationId) ? string.Empty : operationId;
        }

        public object AsyncState
        {
            get { return this.asyncState; }
        }

        public bool CompletedSynchronously
        {
            get
            {
                return Thread.VolatileRead(ref this.completedState) == CompletedSynchronouslyState;
            }
        }

        public WaitHandle AsyncWaitHandle
        {
            get
            {
                if (this.asyncWaitHandle == null)
                {
                    bool done = this.IsCompleted;
                    ManualResetEvent mre = new ManualResetEvent(done);
                    if (Interlocked.CompareExchange(ref this.asyncWaitHandle, mre, null) != null)
                    {
                        // Another thread created this object's event; dispose the event we just created
                        mre.Close();
                    }
                    else
                    {
                        if (!done && this.IsCompleted)
                        {
                            // If the operation wasn't done when we created the event but now it is done, set the event
                            this.asyncWaitHandle.Set();
                        }
                    }
                }

                return this.asyncWaitHandle;
            }
        }

        public bool IsCompleted
        {
            get
            {
                return Thread.VolatileRead(ref this.completedState) != PendingState;
            }
        }

        public static void End(IAsyncResult result, object owner, string operationId)
        {
            AsyncResult asyncResult = result as AsyncResult;

            if (asyncResult == null)
            {
                throw new ArgumentException("Result passed represents an operation not supported by this framework.", "result");
            }

            asyncResult.CheckUsage(owner, operationId);

            // This method assumes that only 1 thread calls EndInvoke for this object
            if (!asyncResult.IsCompleted)
            {
                // If the operation isn't done, wait for it
                asyncResult.AsyncWaitHandle.WaitOne();
                asyncResult.AsyncWaitHandle.Close();
                asyncResult.asyncWaitHandle = null;  // Allow early GC
            }

            // Operation is done: if an exception occurred, throw it
            if (asyncResult.exception != null)
            {
                throw asyncResult.exception;
            }
        }

        internal virtual void Process()
        {
            // Starts processing of the operation.
        }

        protected bool Complete(Exception exception)
        {
            return this.Complete(exception, false /*completedSynchronously*/);
        }

        protected bool Complete(Exception exception, bool completedSynchronously)
        {
            bool result = false;

            // The m_CompletedState field MUST be set prior calling the callback
            int prevState = Interlocked.Exchange(ref this.completedState, completedSynchronously ? CompletedSynchronouslyState : CompletedAsynchronouslyState);

            if (prevState == PendingState)
            {
                // Passing null for exception means no error occurred. This is the common case
                this.exception = exception;

                // Do any processing before completion.
                this.Completing(exception, completedSynchronously);

                // If the event exists, set it
                if (this.asyncWaitHandle != null)
                {
                    this.asyncWaitHandle.Set();
                }

                this.MakeCallback(this.asyncCallback, this);

                // Do any final processing after completion
                this.Completed(exception, completedSynchronously);
                result = true;
            }

            return result;
        }

        protected virtual void Completing(Exception exception, bool completedSynchronously)
        {
        }

        protected virtual void MakeCallback(AsyncCallback callback, AsyncResult result)
        {
            // If a callback method was set, call it
            if (callback != null)
            {
                callback(result);
            }
        }

        protected virtual void Completed(Exception exception, bool completedSynchronously)
        {
        }

        private void CheckUsage(object owner, string operationId)
        {
            if (!object.ReferenceEquals(owner, this.owner))
            {
                throw new InvalidOperationException("End was called on a different object than Begin.");
            }

            // Reuse the operation ID to detect multiple calls to end.
            if (object.ReferenceEquals(null, this.operationId))
            {
                throw new InvalidOperationException("End was called multiple times for this operation.");
            }

            if (!string.Equals(operationId, this.operationId))
            {
                throw new ArgumentException("End operation type was different than Begin.");
            }

            // Mark that End was already called.
            this.operationId = null;
        }
    }

    internal class AsyncResult<TResult> : AsyncResult
    {
        // Field set when operation completes
        private TResult result = default(TResult);

        protected AsyncResult(AsyncCallback asyncCallback, object state, object owner, string operationId)
            : base(asyncCallback, state, owner, operationId)
        {
        }

        public static new TResult End(IAsyncResult result, object owner, string operationId)
        {
            AsyncResult<TResult> asyncResult = result as AsyncResult<TResult>;

            Debug.Assert(asyncResult != null, "Should not happen");

            // Wait until operation has completed 
            AsyncResult.End(result, owner, operationId);

            // Return the result (if above didn't throw)
            return asyncResult.result;
        }

        protected void SetResult(TResult result)
        {
            this.result = result;
        }
    }
}