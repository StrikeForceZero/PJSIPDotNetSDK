using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PJSIPDotNetSDK.Entity;

namespace PJSIPDotNetSDK
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
        public string Name { get; private set; }

        [JsonProperty]
        public string Number { get; private set; }

        [JsonProperty]
        public DateTime Start { get; private set; }

        [JsonProperty]
        public DateTime End { get; private set; }

        [JsonProperty]
        [JsonConverter(typeof (StringEnumConverter))]
        public Call.Type LogType { get; private set; }

        public TimeSpan Duration => End != null ? End.Subtract(Start) : DateTime.Now.Subtract(Start);

        public string DurationString
        {
            get
            {
                var seconds = Duration.Seconds;
                return $"{seconds / 3600:00}:{(seconds / 60) % 60:00}:{seconds % 60:00}";
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