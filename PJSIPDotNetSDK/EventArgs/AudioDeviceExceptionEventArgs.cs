using PJSIP;
using PJSIPDotNetSDK.Exceptions;
using Call = PJSIPDotNetSDK.Entity.Call;

namespace PJSIPDotNetSDK.EventArgs
{
    public class AudioDeviceExceptionEventArgs : System.EventArgs
    {
        public AudioDeviceExceptionEventArgs(Call call, AudioDeviceException exception)
        {
            Call = call;
            Exception = exception;
        }

        public Call Call { get; private set; }
        public AudioDeviceException Exception { get; private set; }
    }
}