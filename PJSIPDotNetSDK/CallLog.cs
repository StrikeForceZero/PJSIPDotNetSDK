using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace pjsipDotNetSDK
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CallLog
    {
        public CallLog(Call call)
        {
            if (call == null) return; // Required for Json.NET Deserialization

            Name = call.CallingName;
            Number = call.CallingNumber;
            Start = DateTime.Now.Subtract(TimeSpan.FromSeconds(call.getInfo().totalDuration.sec));
            LogType = call.CallType;
        }

        [JsonProperty]
        public String Name { get; private set; }

        [JsonProperty]
        public String Number { get; private set; }

        [JsonProperty]
        public DateTime Start { get; private set; }

        [JsonProperty]
        public DateTime End { get; private set; }

        [JsonProperty]
        [JsonConverter(typeof (StringEnumConverter))]
        public Call.Type LogType { get; private set; }

        public TimeSpan Duration
        {
            get
            {
                if (End != null)
                    return End.Subtract(Start);
                return DateTime.Now.Subtract(Start);
            }
        }

        public String DurationString
        {
            get
            {
                var seconds = Duration.Seconds;
                return string.Format("{0:00}:{1:00}:{2:00}", seconds/3600, (seconds/60)%60, seconds%60);
            }
        }

        public CallLog EndLog()
        {
            End = DateTime.Now;
            if (Start > End)
                End = Start;
            return this;
        }
    }
}