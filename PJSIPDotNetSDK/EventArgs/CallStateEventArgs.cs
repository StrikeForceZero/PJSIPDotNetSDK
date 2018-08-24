using PJSIP;
using Call = PJSIPDotNetSDK.Entity.Call;

namespace PJSIPDotNetSDK.EventArgs
{
    public class CallStateEventArgs : System.EventArgs
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