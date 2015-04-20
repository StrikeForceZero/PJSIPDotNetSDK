using System;
using PJSIP;

namespace pjsipDotNetSDK
{
    public class CallStateEventArgs : EventArgs
    {
        public CallStateEventArgs(Call call, pjsip_inv_state state)
        {
            Call = call;
            State = state;
        }

        public Call Call { get; private set; }
        public pjsip_inv_state State { get; private set; }
    }
}