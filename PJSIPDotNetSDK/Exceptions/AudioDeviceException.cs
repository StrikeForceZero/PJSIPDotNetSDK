using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PJSIPDotNetSDK.Exceptions
{
    public class AudioDeviceException : Exception
    {
        public AudioDeviceException(string message, Exception innerException = null) : base(message, innerException)
        {
        }
    }
}
