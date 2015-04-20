using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using log4net.Config;
using log4net.Core;

namespace pjsipDotNetSDK
{
    public static class Logger
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static bool _configured = false;

        public static log4net.ILog GetLogger(string context = null)
        {
            if (_configured == false)
            {
                _configured = true;
                XmlConfigurator.Configure();

                ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Debug;
                ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).RaiseConfigurationChanged(EventArgs.Empty);
            }
            if(context != null)
                log4net.NDC.Push(context);
            return Log;
        }
    }
}
