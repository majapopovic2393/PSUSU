using System;

namespace DataConcentrator
{
    public enum AlarmType
    {
        LOW,
        HIGH
    }

    public class Alarm
    {
        public AlarmType Type { get; set; }
        public double Limit { get; set; }
        public string Message { get; set; }

        public Alarm(AlarmType type, double limit, string message)
        {
            Type = type;
            Limit = limit;
            Message = message;
        }
    }
}
