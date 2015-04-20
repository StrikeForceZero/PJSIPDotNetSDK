using System;

namespace pjsipDotNetSDK
{
    public class IncomingCallEventArgs : EventArgs
    {
        public IncomingCallEventArgs(Call call)
        {
            Call = call;
        }

        public Call Call { get; private set; }
    }
}