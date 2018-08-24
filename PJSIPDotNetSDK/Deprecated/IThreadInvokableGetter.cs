using System.Threading;

namespace PJSIPDotNetSDK
{
    public interface IThreadInvokableGetter
    {
        InvokableThread GetInvokableThread();
    }
}