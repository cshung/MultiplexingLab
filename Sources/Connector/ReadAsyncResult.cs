//-----------------------------------------------------------------------
// <copyright file="ReadAsyncResult.cs" company="PlaceholderCompany">
//     Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Connector
{
    using System;

    internal class ReadAsyncResult : AsyncResult<int>
    {
        internal ReadAsyncResult(AsyncCallback callback, object state, object owner)
            : base(callback, state, owner, "Read")
        {
        }

        internal void NotifyCompleted(int copiedCount)
        {
            this.SetResult(copiedCount);
            this.Complete(null);
        }
    }
}
