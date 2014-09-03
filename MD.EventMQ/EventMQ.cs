using System;
using System.Collections.Generic;

namespace MD.EventMQ
{
    public class EventMQ<T>
    {
        //public event EventHandler<MqEventArgs> OnMessage;
        public event EventHandler OnMessage
        {
            add { _event.Add(value); }
            remove { _event.Remove(value); }
        }
        private WeakEvent<EventHandler> _event = new WeakEvent<EventHandler>();

        public void Publish(T msg)
        {
            OnPublish(msg, MqEventArgs.Empty);
        }

        public void Publish(IEnumerable<T> msgs)
        {
            foreach (var msg in msgs)
            {
                OnPublish(msg, MqEventArgs.Empty);
            }
        }

        public void OnPublish(T msg, MqEventArgs e)
        {
            //System.Threading.ThreadPool.QueueUserWorkItem((obj) => OnMessage(msg, e));
            System.Threading.ThreadPool.QueueUserWorkItem((obj) => _event.Raise(msg, e));
        }
    }

    public class MqEventArgs : EventArgs
    {
        public new static readonly MqEventArgs Empty;
    }
}
