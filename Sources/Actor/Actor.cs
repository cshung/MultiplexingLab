//-----------------------------------------------------------------------
// <copyright file="Actor.cs" company="PlaceholderCompany">
//     Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Actor
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    public abstract class Actor
    {
        private Queue<IMessage> mailBox;
        private ActorManager actorManager;
        private ActorState state;

        public Actor(ActorManager actorManager)
        {
            this.actorManager = actorManager;
            this.mailBox = new Queue<IMessage>();
            this.state = ActorState.Idle;
        }

        private enum ActorState
        {
            Idle,
            Scheduled,
            Running,
            Completed,
        }

        protected ActorManager ActorManager
        {
            get { return this.actorManager; }
        }

        public abstract ActorContinuation OnReceiveMessage(IMessage message);

        internal void EnqueueByActorManager(IMessage message)
        {
            if (this.state == ActorState.Completed)
            {
                throw new Exception("Actor already completed!");
            }

            bool shouldScheduleWork = false;
            lock (this.mailBox)
            {
                if (this.mailBox.Count == 0)
                {
                    shouldScheduleWork = true;
                }

                this.mailBox.Enqueue(message);
            }

            if (shouldScheduleWork)
            {
                this.state = ActorState.Scheduled;
                ThreadPool.QueueUserWorkItem(this.Work);
            }
        }

        protected void Send(Actor to, IMessage message)
        {
            this.actorManager.SendMessage(to, message);
        }

        private void Work(object state)
        {
            this.state = ActorState.Running;
            ActorContinuation actorContinuation = ActorContinuation.Done;
            do
            {
                IMessage message = null;
                lock (this.mailBox)
                {
                    message = this.mailBox.Peek();
                }

                actorContinuation = this.OnReceiveMessage(message);
                lock (this.mailBox)
                {
                    this.mailBox.Dequeue();
                    if (this.mailBox.Count == 0)
                    {
                        this.state = ActorState.Idle;
                        break;
                    }
                }
            }
            while (actorContinuation == ActorContinuation.BlockOnReceive);

            if (actorContinuation == ActorContinuation.Done)
            {
                this.actorManager.CompleteByActor(this);
                this.state = ActorState.Completed;
            }
        }
    }
}