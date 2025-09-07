using System;

namespace DataConcentrator
{
    public class ActivatedAlarm
    {
        public string AlarmId { get; set; }
        public string TagName { get; set; }
        public string Message { get; set; }
        public DateTime Time { get; set; }

        public ActivatedAlarm(string alarmId, string tagName, string message, DateTime time)
        {
            AlarmId = alarmId;
            TagName = tagName;
            Message = message;
            Time = time;
        }
    }
}
