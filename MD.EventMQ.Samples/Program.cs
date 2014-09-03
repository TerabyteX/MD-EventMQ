using System;
using System.Collections.Generic;
using System.Threading;

namespace MD.EventMQ.Samples
{
    class Program
    {
        private static readonly EventMQ<string> mq = new EventMQ<string>();

        static void Main(string[] args)
        {
            mq.OnMessage += ConsumerA;
            mq.OnMessage += (obj, e) =>
            {
                Console.WriteLine("调用ConsumerB的线程ID为：{0}", Thread.CurrentThread.ManagedThreadId);
                Console.WriteLine("ConsumerB: " + obj);
            };
            mq.OnMessage += ConsumerC;
            mq.OnMessage += ConsumerD;
            mq.OnMessage -= ConsumerD;

            //for (var i = 0; i < 1000; i++)
            //{
            mq.Publish("test1");
            mq.Publish("test2");
            mq.Publish("test3");
            mq.Publish(new List<string> { "7", "8", "9" });
            //}

            Console.Read();
        }

        private static void ConsumerA(object sender, EventArgs e)
        {
            Console.WriteLine("调用ConsumerA的线程ID为：{0}", Thread.CurrentThread.ManagedThreadId);
            Console.WriteLine("ConsumerA: " + sender);
        }

        private static void ConsumerC(object sender, EventArgs e)
        {
            Console.WriteLine("调用ConsumerC的线程ID为：{0}", Thread.CurrentThread.ManagedThreadId);
            Console.WriteLine("ConsumerC: " + sender);
        }

        private static void ConsumerD(object sender, EventArgs e)
        {
            Console.WriteLine("调用ConsumerD的线程ID为：{0}", Thread.CurrentThread.ManagedThreadId);
            Console.WriteLine("ConsumerD: " + sender);
        }
    }
}
