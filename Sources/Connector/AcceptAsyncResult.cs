//-----------------------------------------------------------------------
// <copyright file="AcceptAsyncResult.cs" company="PlaceholderCompany">
//     Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Connector
{
    using System;

    internal class AcceptAsyncResult : AsyncResult<Channel>
    {
        internal AcceptAsyncResult(AsyncCallback callback, object state, object owner)
            : base(callback, state, owner, "Accept")
        {
        }

        internal void NotifyCompleted(Channel channel)
        {
            this.SetResult(channel);
            this.Complete(null);
        }
    }
}
