using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace pjsipDotNetSDK
{
    public static class Utils
    {
        public static string UserAppDataPath
        {
            get { return GetUserAppDataPath(); }
        }

        public static string GetUserAppDataPath()
        {
            var path = string.Empty;
            Assembly assm;
            Type at;
            object[] r;

            // Get the .EXE assembly
            assm = Assembly.GetEntryAssembly();
            // Get a 'Type' of the AssemblyCompanyAttribute
            at = typeof (AssemblyCompanyAttribute);
            // Get a collection of custom attributes from the .EXE assembly
            r = assm.GetCustomAttributes(at, false);
            // Get the Company Attribute
            var ct =
                ((AssemblyCompanyAttribute) (r[0]));
            // Build the User App Data Path
            path = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            path += @"\" + ct.Company;
            path += @"\" + assm.GetName().Name;
            path += @"\" + assm.GetName().Version;

            Directory.CreateDirectory(path);

            return path + Path.DirectorySeparatorChar;
        }

        public static bool GetFalseOrValue(this bool? b)
        {
            if (b.HasValue)
                return b.Value;
            return false;
        }

        public static bool In<T>(this T obj, params T[] args)
        {
            return args.Contains(obj);
        }
    }
}