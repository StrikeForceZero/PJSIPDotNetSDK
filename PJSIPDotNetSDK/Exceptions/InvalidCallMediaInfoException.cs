using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PJSIPDotNetSDK.Exceptions
{
    public class InvalidCallMediaInfoException : Exception
    {
        public InvalidCallMediaInfoException(string message) : base(message)
        {
            
        }
    }
}
