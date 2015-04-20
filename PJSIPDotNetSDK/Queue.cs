using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace pjsipDotNetSDK
{
    /// <summary>
    /// Thread Safe Generic Queue
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Queue<T>
    {
        private static Mutex mutex = new Mutex();
        private Queue queue;

        /// <summary>
        /// Constructor
        /// </summary>
        public Queue()
        {
            queue = new Queue();
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="col"></param>
        public Queue(ICollection iCollection)
        {
            queue = new Queue(iCollection);
        }
        /// <summary>
        /// Contructor
        /// </summary>
        /// <param name="capacity"></param>
        public Queue(int capacity)
        {
            queue = new Queue(capacity);
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="growFactor"></param>
        public Queue(int capacity, float growFactor)
        {
            queue = new Queue(capacity, growFactor);
        }

        /// <summary>
        /// Clears the queue
        /// </summary>
        public void Clear()
        {
            mutex.WaitOne();
            try
            {
                queue.Clear();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        /// <summary>
        /// Clones the queue
        /// </summary>
        /// <returns></returns>
        public Queue<T> Clone()
        {
            mutex.WaitOne();
            Queue<T> obj;
            try
            {
                obj = (Queue<T>)queue.Clone();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
            return obj;
        }
        /// <summary>
        /// Checks to see if the queue contains an object
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool Contains(T obj)
        {
            mutex.WaitOne();
            bool contain = false;
            try
            {
                contain = queue.Contains(obj);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
            return contain;
        }
        /// <summary>
        /// The current number of items in the queue
        /// </summary>
        public int Count
        {
            get
            {
                mutex.WaitOne();
                try
                {
                    return queue.Count;
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }
        /// <summary>
        /// Retrieves the next item in the queue
        /// </summary>
        /// <returns></returns>
        public T Dequeue()
        {
            mutex.WaitOne();
            T item = default(T);
            try
            {
                if (queue.Count > 0)
                    item = (T)queue.Dequeue();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
            return item;
        }
        /// <summary>
        /// Adds an item to the queue
        /// </summary>
        /// <param name="obj"></param>
        public void Enqueue(T obj)
        {
            mutex.WaitOne();
            try
            {
                queue.Enqueue(obj);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        /// <summary>
        /// Checks to see if another queue matches this queue
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool Equals(T obj)
        {
            mutex.WaitOne();
            bool equals = false;
            try
            {
                equals = queue.Equals(obj);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
            return equals;
        }
        /// <summary>
        /// Retrieves the next item from the queue but does not remove it from the queue
        /// </summary>
        /// <returns></returns>
        public T Peek()
        {
            mutex.WaitOne();
            T item = default(T);
            try
            {
                item = (T)queue.Peek();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
            return item;
        }
        /// <summary>
        /// Trims the queue to a proper size
        /// </summary>
        public void TrimToSize()
        {
            mutex.WaitOne();
            try
            {
                queue.TrimToSize();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }
}
