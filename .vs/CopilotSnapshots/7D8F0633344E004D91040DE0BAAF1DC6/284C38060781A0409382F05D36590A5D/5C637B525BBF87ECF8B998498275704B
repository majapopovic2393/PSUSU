using System;
using System.Collections.Generic;

namespace DataConcentrator
{
    public enum TagType
    {
        DI, // Digital Input
        DO, // Digital Output
        AI, // Analog Input
        AO  // Analog Output
    }

    public abstract class Tag
    {
        public TagType Type { get; protected set; }
        public string Id { get; protected set; }
        public string Description { get; set; }
        public string IOAddress { get; protected set; }
        public Dictionary<string, object> Properties { get; } = new Dictionary<string, object>();

        protected Tag(TagType type, string id, string description, string ioAddress)
        {
            Type = type;
            Id = id;
            Description = description;
            IOAddress = ioAddress;
        }
    }

    public class DigitalTag : Tag
    {
        public int ScanTime { get; set; } // Only for input
        public bool OnOffScan { get; set; } // Only for input

        public DigitalTag(TagType type, string id, string description, string ioAddress, int scanTime = 0, bool onOffScan = false)
            : base(type, id, description, ioAddress)
        {
            if (type != TagType.DI && type != TagType.DO)
                throw new ArgumentException("DigitalTag must be DI or DO");
            if (type == TagType.DI)
            {
                ScanTime = scanTime;
                OnOffScan = onOffScan;
            }
        }
    }

    public class AnalogTag : Tag
    {
        public int ScanTime { get; set; } // Only for input
        public bool OnOffScan { get; set; } // Only for input
        public double LowLimit { get; set; }
        public double HighLimit { get; set; }
        public string Units { get; set; }
        public double InitialValue { get; set; } // Only for AO
        public List<Alarm> Alarms { get; set; } = new List<Alarm>();

        public AnalogTag(TagType type, string id, string description, string ioAddress, double lowLimit, double highLimit, string units, int scanTime = 0, bool onOffScan = false, double initialValue = 0)
            : base(type, id, description, ioAddress)
        {
            if (type != TagType.AI && type != TagType.AO)
                throw new ArgumentException("AnalogTag must be AI or AO");
            LowLimit = lowLimit;
            HighLimit = highLimit;
            Units = units;
            if (type == TagType.AI)
            {
                ScanTime = scanTime;
                OnOffScan = onOffScan;
            }
            if (type == TagType.AO)
            {
                InitialValue = initialValue;
            }
        }
    }
}
