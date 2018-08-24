using log4net;
using log4net.Config;
using log4net.Core;

namespace PJSIPDotNetSDK.Utils
{
    public static class Logger
    {
        private static log4net.ILog Log => log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool Configured { get; private set; } = false;

        public static log4net.ILog GetLogger(string context = null)
        {
            if (Configured == false)
            {
                Configured = true;
                XmlConfigurator.Configure();

                ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Debug;
                ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).RaiseConfigurationChanged(System.EventArgs.Empty);
            }
            if (context != null)
            {
                if (log4net.NDC.Depth == 1)
                    log4net.NDC.Pop();
                log4net.NDC.Push(context);
            }
            return Log;
        }

        public static void LogCallingThread(string tag = "", [System.Runtime.CompilerServices.CallerMemberName]string memberName = "", [System.Runtime.CompilerServices.CallerFilePath]string filePath = "")
        {
            if (System.Threading.Thread.CurrentThread.Name == null)
            {
                System.Threading.Thread.CurrentThread.Name = $"GeneratedName_{(new System.Random()).Next(1, 10000)}";
            }

            Logger.GetLogger(filePath.Substring(filePath.LastIndexOf(@"\") + 1)).Debug($"[{tag}] {memberName} Thread: {System.Threading.Thread.CurrentThread.Name}");
        }
    }
}