using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using PJSIPDotNetSDK.Entity;
using PJSIPDotNetSDK.Helpers;

namespace PJSIPDotNetSDK.Workers
{
    public class DtmfDialerWorker
    {
        public readonly ConcurrentQueue<string> DtmfQueue = new ConcurrentQueue<string>();
        public Call Call { get; }
        public int Interval { get; }
        public Task Task { get; private set; }

        public Boolean Running => Task != null && !Task.IsCanceled && !Task.IsCompleted && !Task.IsFaulted;

        internal DtmfDialerWorker(Call call, int interval = 350)
        {
            Call = call;
            Interval = interval;
        }

        internal async void Process(IProgress<int> progress, CancellationToken cancellationToken)
        {
            //if (Running)
            //    throw new Exception("DTMF Worker is already running!");

            Task = System.Threading.Tasks.Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested && DtmfQueue.Count > 0)
                {
                    string dtmfDigit;
                    if (DtmfQueue.TryDequeue(out dtmfDigit) == false)
                        break;

                    Call.Invoke(c => c.DialDtmf(dtmfDigit, false) );

                    await Task.Delay(Interval, cancellationToken);

                    progress.Report(DtmfQueue.Count);
                }
            }, cancellationToken);

            await Task;
        }
    }
}
