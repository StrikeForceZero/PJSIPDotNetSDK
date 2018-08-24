using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PJSIPDotNetSDK.Helpers
{
    public interface IDispatchable<T>
    {
        Dispatcher Dispatcher { get; }

        void Invoke(GenericHandlers.InvokableDelegate<T> action, DispatcherPriority priorty);
        TR Invoke<TR>(GenericHandlers.InvokableDelegate<T, TR> action, DispatcherPriority priorty);
        void BeginInvoke(GenericHandlers.InvokableDelegate<T> action, DispatcherPriority priorty);
        Task<TR> BeginInvoke<TR>(GenericHandlers.InvokableDelegate<T, TR> action, DispatcherPriority priorty);
    }
}
