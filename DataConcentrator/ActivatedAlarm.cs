using System;

namespace DataConcentrator
{
    public class ActivatedAlarm
    {
        public string AlarmId { get; set; } // string, identifikacija alarma, vrednost granice na kojoj je alarm aktivan
        public string TagName { get; set; } // string, ime taga na kojem je alarm aktivan 
        public string Message { get; set; } // string, poruka koja opisuje alarm
        public DateTime Time { get; set; }  // datetime, vreme kada je alarm aktiviran

        public ActivatedAlarm(string alarmId, string tagName, string message, DateTime time)
        {
            AlarmId = alarmId;
            TagName = tagName;
            Message = message;
            Time = time;
        }
    }
}
