using PJSIPDotNetSDK.Entity;

namespace PJSIPDotNetSDK.EventArgs
{
    public class IncomingCallEventArgs : System.EventArgs
    {
        public IncomingCallEventArgs(Call call)
        {
            Call = call;
        }

        public Call Call { get; private set; }
    }
}