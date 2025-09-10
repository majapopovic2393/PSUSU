using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DataConcentrator
{
    using DataConcentrator;

    public delegate void AlarmActivatedEventHandler(object sender, ActivatedAlarm alarmInfo);

    public class ContextClass
    {
        // Skladište svih tagova po ID
        private Dictionary<string, Tag> tags = new Dictionary<string, Tag>(); 
        // Skladište aktiviranih alarma
        private List<ActivatedAlarm> activatedAlarms = new List<ActivatedAlarm>();

        public IReadOnlyDictionary<string, Tag> Tags => tags;
        public IReadOnlyList<ActivatedAlarm> ActivatedAlarms => activatedAlarms;

        // Event za aktivaciju alarma
        public event AlarmActivatedEventHandler AlarmActivated;

        private ScadaDatabase db = new ScadaDatabase();

        public bool AddTag(Tag tag)
        {
            // Validacija: ID mora biti jedinstven
            if (tag == null || string.IsNullOrWhiteSpace(tag.Id) || tags.ContainsKey(tag.Id))
                return false;

            // Validacija: units samo za analogne tagove
            if ((tag is DigitalTag) && tag is AnalogTag analogTag && !string.IsNullOrWhiteSpace(analogTag.Units))
                return false;

            tags.Add(tag.Id, tag);
            return true;
        }

        public bool RemoveTag(string id)
        {
            return tags.Remove(id);
        }

        // Dodavanje alarma na AI tag
        public bool AddAlarmToAnalogInput(string tagId, AlarmType alarmType, double limit, string message)
        {
            if (!tags.TryGetValue(tagId, out Tag tag))
                return false;
            if (!(tag is AnalogTag analogTag) || analogTag.Type != TagType.AI)
                return false;
            // Ne dozvoljavamo duplirane alarme sa istim tipom i granicom
            if (analogTag.Alarms.Any(a => a.Type == alarmType && a.Limit == limit))
                return false;
            analogTag.Alarms.Add(new Alarm(alarmType, limit, message));
            return true;
        }

        // Uklanjanje alarma sa AI taga
        public bool RemoveAlarmFromAnalogInput(string tagId, AlarmType alarmType, double limit)
        {
            if (!tags.TryGetValue(tagId, out Tag tag))
                return false;
            if (!(tag is AnalogTag analogTag) || analogTag.Type != TagType.AI)
                return false;
            var alarm = analogTag.Alarms.FirstOrDefault(a => a.Type == alarmType && a.Limit == limit);
            if (alarm == null)
                return false;
            analogTag.Alarms.Remove(alarm);
            return true;
        }

        // PISANJE VREDNOSTI U IZLAZNE TAGOVE
        public bool SetOutputValue(string tagId, double value)
        {
            if (!tags.TryGetValue(tagId, out Tag tag))
                return false;
            if (tag.Type == TagType.DO && tag is DigitalTag)
            {
                tag.Properties["Value"] = value != 0 ? 1 : 0;
                return true;
            }
            if (tag.Type == TagType.AO && tag is AnalogTag analogTag)
            {
                analogTag.InitialValue = value;
                return true;
            }
            return false;
        }

        // UKLJUČIVANJE/ISKLJUČIVANJE SKENIRANJA ULAZNIH TAGOVA
        public bool SetInputScan(string tagId, bool onOff)
        {
            if (!tags.TryGetValue(tagId, out Tag tag))
                return false;
            if (tag.Type == TagType.DI && tag is DigitalTag digitalTag)
            {
                digitalTag.OnOffScan = onOff;
                return true;
            }
            if (tag.Type == TagType.AI && tag is AnalogTag analogTag)
            {
                analogTag.OnOffScan = onOff;
                return true;
            }
            return false;
        }

        // PROVERA I AKTIVACIJA ALARMA NAD AI TAGOM
        public void CheckAndActivateAlarms(string tagId, double currentValue)
        {
            if (!tags.TryGetValue(tagId, out Tag tag))
                return;
            if (!(tag is AnalogTag analogTag) || analogTag.Type != TagType.AI)
                return;
            // Ispis promena vrednosti za testiranje
            Console.WriteLine($"[SCADA] Tag: {analogTag.Id}, Value: {currentValue}");
            foreach (var alarm in analogTag.Alarms)
            {
                bool isActive = false;
                if (alarm.Type == AlarmType.HIGH && currentValue > alarm.Limit)
                    isActive = true;
                if (alarm.Type == AlarmType.LOW && currentValue < alarm.Limit)
                    isActive = true;
                if (isActive)
                {
                    var activated = new ActivatedAlarm(alarm.Limit.ToString(), analogTag.Id, alarm.Message, DateTime.Now);
                    activatedAlarms.Add(activated);
                    AlarmActivated?.Invoke(this, activated);
                }
            }
        }

        // --- ČUVANJE/ČITANJE KONFIGURACIJE (stubovi za povezivanje sa bazom) ---
        public void SaveConfigurationToDatabase()
        {
            using (var conn = db.GetConnection())
            {
                conn.Open();
                var cmd = conn.CreateCommand();

                // Očisti stare podatke
                cmd.CommandText = "DELETE FROM Tagovi; DELETE FROM Alarmi; DELETE FROM AktiviraniAlarmi;";
                cmd.ExecuteNonQuery();

                // Tagovi
                foreach (var tag in tags.Values)
                {
                    cmd.CommandText = @"INSERT INTO Tagovi (Id, Type, Description, IOAddress, LowLimit, HighLimit, Units, ScanTime, OnOffScan, InitialValue)
                                        VALUES (@Id, @Type, @Description, @IOAddress, @LowLimit, @HighLimit, @Units, @ScanTime, @OnOffScan, @InitialValue)";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@Id", tag.Id);
                    cmd.Parameters.AddWithValue("@Type", (int)tag.Type);
                    cmd.Parameters.AddWithValue("@Description", tag.Description);
                    cmd.Parameters.AddWithValue("@IOAddress", tag.IOAddress);
                    if (tag is AnalogTag at)
                    {
                        cmd.Parameters.AddWithValue("@LowLimit", at.LowLimit);
                        cmd.Parameters.AddWithValue("@HighLimit", at.HighLimit);
                        cmd.Parameters.AddWithValue("@Units", at.Units ?? "");
                        cmd.Parameters.AddWithValue("@ScanTime", at.ScanTime);
                        cmd.Parameters.AddWithValue("@OnOffScan", at.OnOffScan ? 1 : 0);
                        cmd.Parameters.AddWithValue("@InitialValue", at.InitialValue);
                    }
                    else if (tag is DigitalTag dt)
                    {
                        cmd.Parameters.AddWithValue("@LowLimit", 0);
                        cmd.Parameters.AddWithValue("@HighLimit", 0);
                        cmd.Parameters.AddWithValue("@Units", "");
                        cmd.Parameters.AddWithValue("@ScanTime", dt.ScanTime);
                        cmd.Parameters.AddWithValue("@OnOffScan", dt.OnOffScan ? 1 : 0);
                        cmd.Parameters.AddWithValue("@InitialValue", 0);
                    }
                    cmd.ExecuteNonQuery();

                    // Alarmi za AI tagove
                    if (tag is AnalogTag analogTag && analogTag.Type == TagType.AI)
                    {
                        foreach (var alarm in analogTag.Alarms)
                        {
                            cmd.CommandText = @"INSERT INTO Alarmi (TagId, Type, Limit, Message)
                                                VALUES (@TagId, @AlarmType, @Limit, @Message)";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@TagId", tag.Id);
                            cmd.Parameters.AddWithValue("@AlarmType", (int)alarm.Type);
                            cmd.Parameters.AddWithValue("@Limit", alarm.Limit);
                            cmd.Parameters.AddWithValue("@Message", alarm.Message);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                // Aktivirani alarmi
                foreach (var aa in activatedAlarms)
                {
                    cmd.CommandText = @"INSERT INTO AktiviraniAlarmi (AlarmId, TagName, Message, Time)
                                        VALUES (@AlarmId, @TagName, @Message, @Time)";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@AlarmId", aa.AlarmId);
                    cmd.Parameters.AddWithValue("@TagName", aa.TagName);
                    cmd.Parameters.AddWithValue("@Message", aa.Message);
                    cmd.Parameters.AddWithValue("@Time", aa.Time.ToString("o"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void LoadConfigurationFromDatabase()
        {
            tags.Clear();
            activatedAlarms.Clear();
            using (var conn = db.GetConnection())
            {
                conn.Open();
                var cmd = conn.CreateCommand();

                // Tagovi.
                cmd.CommandText = "SELECT * FROM Tagovi";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var type = (TagType)Convert.ToInt32(reader["Type"]);
                        Tag tag = null;
                        if (type == TagType.AI || type == TagType.AO)
                        {
                            tag = new AnalogTag(
                                type,
                                reader["Id"].ToString(),
                                reader["Description"].ToString(),
                                reader["IOAddress"].ToString(),
                                Convert.ToDouble(reader["LowLimit"]),
                                Convert.ToDouble(reader["HighLimit"]),
                                reader["Units"].ToString(),
                                Convert.ToInt32(reader["ScanTime"]),
                                Convert.ToInt32(reader["OnOffScan"]) == 1,
                                Convert.ToDouble(reader["InitialValue"])
                            );
                        }
                        else
                        {
                            tag = new DigitalTag(
                                type,
                                reader["Id"].ToString(),
                                reader["Description"].ToString(),
                                reader["IOAddress"].ToString(),
                                Convert.ToInt32(reader["ScanTime"]),
                                Convert.ToInt32(reader["OnOffScan"]) == 1
                            );
                        }
                        tags[tag.Id] = tag;
                    }
                }

                // Alarmi
                cmd.CommandText = "SELECT * FROM Alarmi";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var tagId = reader["TagId"].ToString();
                        if (tags.TryGetValue(tagId, out Tag tag) && tag is AnalogTag analogTag && analogTag.Type == TagType.AI)
                        {
                            analogTag.Alarms.Add(new Alarm(
                                (AlarmType)Convert.ToInt32(reader["Type"]),
                                Convert.ToDouble(reader["Limit"]),
                                reader["Message"].ToString()
                            ));
                        }
                    }
                }

                // Aktivirani alarmi
                cmd.CommandText = "SELECT * FROM AktiviraniAlarmi";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        activatedAlarms.Add(new ActivatedAlarm(
                            reader["AlarmId"].ToString(),
                            reader["TagName"].ToString(),
                            reader["Message"].ToString(),
                            DateTime.Parse(reader["Time"].ToString())
                        ));
                    }
                }
            }
        }
    }
}
