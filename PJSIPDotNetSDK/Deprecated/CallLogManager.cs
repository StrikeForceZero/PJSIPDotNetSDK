using System.IO;
using Newtonsoft.Json;

namespace PJSIPDotNetSDK
{
    public class CallLogManager
    {
        public CallLogManager()
        {
            CallLogs = new ExtendedCollection<CallLog>();
        }

        private ExtendedCollection<CallLog> _callLogs { get; set; }

        public ExtendedCollection<CallLog> CallLogs
        {
            get
            {
                while (_callLogs.Count > 100)
                {
                    _callLogs.RemoveAt(0);
                }
                return _callLogs;
            }
            private set
            {
                while (value.Count > 100)
                {
                    value.RemoveAt(0);
                }
                _callLogs = value;
            }
        }

        public CallLogManager SaveToFile(string filename)
        {
            try
            {
                File.WriteAllText(filename, JsonConvert.SerializeObject(CallLogs));
            }
            catch (IOException ex)
            {
                //eat it
            }
            return this;
        }

        public CallLogManager LoadFromFile(string filename)
        {
            CallLogs = JsonConvert.DeserializeObject<ExtendedCollection<CallLog>>(File.ReadAllText(filename));
            return this;
        }
    }
}