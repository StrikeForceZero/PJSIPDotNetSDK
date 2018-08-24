namespace PJSIPDotNetSDK.Helpers
{
    public class GenericHandlers
    {
        public delegate void ObjectDisposingHandler(object sender, object obj);

        public delegate void InvokableDelegate<in T>(T _this);

        public delegate TR InvokableDelegate<in T, out TR>(T _this);
    }
}
