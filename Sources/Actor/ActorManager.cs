//-----------------------------------------------------------------------
// <copyright file="ActorManager.cs" company="PlaceholderCompany">
//     Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Actor
{
    using System.Collections.Generic;
    using System.Threading;

    public class ActorManager
    {
        private readonly object locker = new object();
        private List<Actor> actors;

        public ActorManager()
        {
            this.actors = new List<Actor>();
        }

        public void RunActor(Actor actor)
        {
            this.actors.Add(actor);
            ThreadPool.QueueUserWorkItem((state) => actor.EnqueueByActorManager(new StartMessage()));
        }

        public void SendMessage(Actor to, IMessage message)
        {
            to.EnqueueByActorManager(message);
        }

        public void WaitForActors()
        {
            lock (this.locker)
            {
                while (this.actors.Count > 0)
                {
                    Monitor.Wait(this.locker);
                }
            }
        }

        public void CompleteByActor(Actor actor)
        {
            lock (this.locker)
            {
                this.actors.Remove(actor);
                Monitor.Pulse(this.locker);
            }
        }
    }
}