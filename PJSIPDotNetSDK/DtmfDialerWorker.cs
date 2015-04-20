using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace pjsipDotNetSDK
{
    public class DtmfDialerWorker : BackgroundWorker
    {
        public readonly Queue<string> DtmfQueue = new Queue<string>(10, 1.5f);
        private readonly Call _call;
        private readonly int _interval;

        public DtmfDialerWorker(Call call, int interval = 350)
        {
            _call = call;
            _interval = interval;
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            base.OnDoWork(e);

            while (DtmfQueue.Count > 0)
            {
                if (WorkerSupportsCancellation && CancellationPending)
                    break;
                if (_call != null) _call.dialDtmf(DtmfQueue.Dequeue(), false);
                System.Threading.Thread.Sleep(_interval);
                if(WorkerReportsProgress)
                    ReportProgress(DtmfQueue.Count);
            }
        }

        protected override void OnProgressChanged(ProgressChangedEventArgs e)
        {
            base.OnProgressChanged(e);
        }

        protected override void OnRunWorkerCompleted(RunWorkerCompletedEventArgs e)
        {
            base.OnRunWorkerCompleted(e);
        }
    }
}
