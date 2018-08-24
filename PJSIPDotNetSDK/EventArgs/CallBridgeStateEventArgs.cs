using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PJSIP;
using Call = PJSIPDotNetSDK.Entity.Call;

namespace PJSIPDotNetSDK.EventArgs
{
    public class CallBridgeStateEventArgs
    {
        public enum BridgeState
        {
            Bridged,
            Unbridged
        }

        public CallBridgeStateEventArgs(Call sourceCall, Call targetCall, BridgeState bridgeState)
        {
            Call = sourceCall;
            TargetCall = targetCall;
            State = bridgeState;
        }

        public Call Call { get; private set; }
        public Call TargetCall { get; private set; }
        public BridgeState State { get; private set; }
    }
}
