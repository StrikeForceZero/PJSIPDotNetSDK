using System;
using PJSIP;

namespace pjsipDotNetSDK
{
    public class CallMediaStateEventArgs : EventArgs
    {
        public CallMediaStateEventArgs(Call call, pjsua_call_media_status state)
        {
            Call = call;
            State = state;
        }

        public Call Call { get; private set; }
        public pjsua_call_media_status State { get; private set; }
    }
}