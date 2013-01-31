﻿namespace Multiplexer
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    internal class AsyncResult : IAsyncResult
    {
        // Fields set at construction which never change while operation is pending
        private readonly AsyncCallback m_AsyncCallback;
        private readonly Object m_AsyncState;

        // Fields set at construction which do change after operation completes
        private const Int32 c_StatePending = 0;
        private const Int32 c_StateCompletedSynchronously = 1;
        private const Int32 c_StateCompletedAsynchronously = 2;
        private Int32 m_CompletedState = c_StatePending;

        // Field that may or may not get set depending on usage
        private ManualResetEvent m_AsyncWaitHandle;

        // Fields set when operation completes
        private Exception m_exception;

        /// <summary>
        /// The object which started the operation.
        /// </summary>
        private object m_owner;

        /// <summary>
        /// Used to verify BeginXXX and EndXXX calls match.
        /// </summary>

        private string m_operationId;

        protected AsyncResult(AsyncCallback asyncCallback, object state, object owner, string operationId)
        {
            m_AsyncCallback = asyncCallback;
            m_AsyncState = state;
            m_owner = owner;
            m_operationId = String.IsNullOrEmpty(operationId) ? String.Empty : operationId;
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
            Int32 prevState = Interlocked.Exchange(ref m_CompletedState, completedSynchronously ? c_StateCompletedSynchronously : c_StateCompletedAsynchronously);

            if (prevState == c_StatePending)
            {
                // Passing null for exception means no error occurred. This is the common case
                m_exception = exception;

                // Do any processing before completion.
                this.Completing(exception, completedSynchronously);

                // If the event exists, set it
                if (m_AsyncWaitHandle != null)
                {
                    m_AsyncWaitHandle.Set();
                }

                this.MakeCallback(m_AsyncCallback, this);

                // Do any final processing after completion
                this.Completed(exception, completedSynchronously);
                result = true;
            }

            return result;
        }

        private void CheckUsage(object owner, string operationId)
        {
            if (!object.ReferenceEquals(owner, m_owner))
            {
                throw new InvalidOperationException("End was called on a different object than Begin.");
            }

            // Reuse the operation ID to detect multiple calls to end.
            if (object.ReferenceEquals(null, m_operationId))
            {
                throw new InvalidOperationException("End was called multiple times for this operation.");

            }

            if (!String.Equals(operationId, m_operationId))
            {
                throw new ArgumentException("End operation type was different than Begin.");
            }

            // Mark that End was already called.
            m_operationId = null;
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
                asyncResult.m_AsyncWaitHandle = null;  // Allow early GC
            }

            // Operation is done: if an exception occurred, throw it
            if (asyncResult.m_exception != null)
            {
                throw asyncResult.m_exception;
            }
        }

        #region Implementation of IAsyncResult

        public Object AsyncState { get { return m_AsyncState; } }

        public bool CompletedSynchronously
        {
            get
            {
                return Thread.VolatileRead(ref m_CompletedState) == c_StateCompletedSynchronously;
            }
        }

        public WaitHandle AsyncWaitHandle
        {
            get
            {
                if (m_AsyncWaitHandle == null)
                {
                    bool done = IsCompleted;
                    ManualResetEvent mre = new ManualResetEvent(done);
                    if (Interlocked.CompareExchange(ref m_AsyncWaitHandle, mre, null) != null)
                    {
                        // Another thread created this object's event; dispose the event we just created
                        mre.Close();
                    }
                    else
                    {
                        if (!done && IsCompleted)
                        {
                            // If the operation wasn't done when we created the event but now it is done, set the event
                            m_AsyncWaitHandle.Set();
                        }
                    }
                }

                return m_AsyncWaitHandle;
            }
        }

        public bool IsCompleted
        {
            get
            {
                return Thread.VolatileRead(ref m_CompletedState) != c_StatePending;
            }
        }

        #endregion

        #region Extensibility

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

        #endregion
    }

    internal class AsyncResult<TResult> : AsyncResult
    {
        // Field set when operation completes
        private TResult m_result = default(TResult);

        protected void SetResult(TResult result)
        {
            m_result = result;
        }

        protected AsyncResult(AsyncCallback asyncCallback, object state, object owner, string operationId) : base(asyncCallback, state, owner, operationId)
        {
        }

        new public static TResult End(IAsyncResult result, object owner, string operationId)
        {
            AsyncResult<TResult> asyncResult = result as AsyncResult<TResult>;

            Debug.Assert(asyncResult != null);
            // Wait until operation has completed 
            AsyncResult.End(result, owner, operationId);
            // Return the result (if above didn't throw)
            return asyncResult.m_result;
        }
    }
}