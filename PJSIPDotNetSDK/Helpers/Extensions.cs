using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PJSIPDotNetSDK.Helpers
{
    public static class Extensions
    {
        public static bool In<T>(this T obj, params T[] args) => args.Contains(obj);

        public static void Invoke<T>(this IDispatchable<T> _this, GenericHandlers.InvokableDelegate<T> func, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            //if we are the same thread as the context then just call the function directly
            if (_this.Dispatcher.CheckAccess())
            {
                func((T) _this);
                return;
            }
            
            _this.Dispatcher.Invoke((Action)delegate { func((T)_this); }, priority);
        }

        public static TR Invoke<T, TR>(this IDispatchable<T> _this, GenericHandlers.InvokableDelegate<T, TR> func, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            //if we are the same thread as the context then just call the function directly
            if (_this.Dispatcher.CheckAccess())
                return func((T)_this);

            var retval = default(TR);
            _this.Dispatcher.Invoke((Action)delegate { retval = func((T)_this); }, priority);
            return retval;
        }

        public static async void BeginInvoke<T>(this IDispatchable<T> _this, GenericHandlers.InvokableDelegate<T> func, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            //if we are the same thread as the context then just call the function directly
            if (_this.Dispatcher.CheckAccess())
            {
                func((T) _this);
                return;
            }

            await _this.Dispatcher.BeginInvoke(
                priority,
                (Action)delegate { func((T)_this); }
            );
        }

        public static async Task<TR> BeginInvoke<T, TR>(this IDispatchable<T> _this, GenericHandlers.InvokableDelegate<T, TR> func, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            //if we are the same thread as the context then just call the function directly
            if (_this.Dispatcher.CheckAccess())
                return func((T)_this);

            var retval = default(TR);
            await _this.Dispatcher.BeginInvoke(
                priority,
                (Action) delegate { retval = func((T)_this); }
            );
            return retval;
        }
    }
}
