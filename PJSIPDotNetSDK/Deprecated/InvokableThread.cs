using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PJSIPDotNetSDK
{
    public class InvokableThread : IDisposable
    {
        public Thread Thread { get; }

        private readonly object _locker = new object();

        public enum Priority
        {
            Procastinate,
            Normal,
            Now
        }

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEventSlim _processQueueSignal = new ManualResetEventSlim();

        private readonly ConcurrentDictionary<Priority, ConcurrentQueue<Delegate>> _actionQueue =
            new ConcurrentDictionary<Priority, ConcurrentQueue<Delegate>>();

        public InvokableThread(string name = "", CancellationToken cancellationToken = new CancellationToken())
        {
            if (
                _actionQueue.TryAdd(Priority.Now, new ConcurrentQueue<Delegate>()) == false
                ||
                _actionQueue.TryAdd(Priority.Normal, new ConcurrentQueue<Delegate>()) == false
                ||
                _actionQueue.TryAdd(Priority.Procastinate, new ConcurrentQueue<Delegate>()) == false
                )
                throw new Exception("Failed to create queues");

            Thread = new Thread(() =>
            {
                while (_cancellationTokenSource.IsCancellationRequested == false &&
                       cancellationToken.IsCancellationRequested == false)
                {
                    if (_actionQueue.Values.Sum(x => x.Count) == 0)
                        _processQueueSignal.Wait(_cancellationTokenSource.Token);
                    if (Deque(Priority.Now) == false && Deque(Priority.Normal) == false)
                        Deque(Priority.Procastinate);
                    _processQueueSignal.Reset();
                }
                _cancellationTokenSource.Cancel();
            });
            Thread.Name = name;
            Thread.Start();
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }

        private bool Deque(Priority priority)
        {
            ConcurrentQueue<Delegate> queue;
            if (_actionQueue.TryGetValue(priority, out queue) == false)
                return false;
            Delegate action;
            if (queue.TryDequeue(out action) == false)
                return false;
            action.DynamicInvoke();
            return true;
        }

        public void Invoke(Action action, Priority priority = Priority.Normal)
        {
            ManualResetEventSlim actionCompleted;

            lock (_locker)
            {
                actionCompleted = new ManualResetEventSlim();

                Action wrappedAction = () =>
                {
                    action();
                    actionCompleted.Set();
                };

                ConcurrentQueue<Delegate> queue;
                if (_actionQueue.TryGetValue(priority, out queue) == false)
                    throw new ArgumentOutOfRangeException(nameof(priority));
                queue.Enqueue(wrappedAction);
            }

            _processQueueSignal.Set();

            actionCompleted.Wait(_cancellationTokenSource.Token);
        }

        public async void BeginInvoke(Action action, Priority priority = Priority.Normal)
        {
            ManualResetEventSlim actionCompleted;

            lock (_locker)
            {
                actionCompleted = new ManualResetEventSlim();

                Action wrappedAction = () =>
                {
                    action();
                    actionCompleted.Set();
                };

                ConcurrentQueue<Delegate> queue;
                if (_actionQueue.TryGetValue(priority, out queue) == false)
                    throw new ArgumentOutOfRangeException(nameof(priority));
                queue.Enqueue(wrappedAction);
            }

            _processQueueSignal.Set();

            await Task.Run(() => actionCompleted.Wait(_cancellationTokenSource.Token));
        }
    }
}
