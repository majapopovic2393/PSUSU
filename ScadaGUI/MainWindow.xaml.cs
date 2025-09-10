﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using DataConcentrator;
using System.Globalization;
using System.Windows.Controls;
using PLCSimulator;
using System.Windows.Threading;

namespace ScadaGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ContextClass context;
        private PLCSimulatorManager plcSimulator;
        private DispatcherTimer scanTimer;

        public MainWindow()
        {
            InitializeComponent();
            context = new ContextClass();
            context.LoadConfigurationFromDatabase();
            plcSimulator = new PLCSimulatorManager();
            plcSimulator.StartPLCSimulator();
            context.AlarmActivated += Context_AlarmActivated;
            InitScanTimer();
            LoadData();
        }

        private void InitScanTimer()
        {
            scanTimer = new DispatcherTimer();
            scanTimer.Interval = TimeSpan.FromMilliseconds(500); // Provera svakih 0.5s
            scanTimer.Tick += ScanTimer_Tick;
            scanTimer.Start();
        }

        private void ScanTimer_Tick(object sender, EventArgs e)
        {
            bool dataChanged = false;
            foreach (var tag in context.Tags.Values)
            {
                if (tag is DigitalTag dig && (dig.Type == TagType.DI) && dig.OnOffScan && dig.ScanTime > 0)
                {
                    if (!ShouldScan(dig)) continue;
                    double value = plcSimulator.GetAnalogValue(dig.IOAddress); // koristi GetAnalogValue za DI
                    dig.Properties["Value"] = value;
                    dataChanged = true;
                }
                else if (tag is AnalogTag ai && ai.Type == TagType.AI && ai.OnOffScan && ai.ScanTime > 0)
                {
                    if (!ShouldScan(ai)) continue;
                    double value = plcSimulator.GetAnalogValue(ai.IOAddress);
                    ai.InitialValue = value;
                    context.CheckAndActivateAlarms(ai.Id, value);
                    dataChanged = true;
                }
            }
            if (dataChanged)
            {
                TagsDataGrid.ItemsSource = null;
                TagsDataGrid.ItemsSource = context.Tags.Values.ToList();
            }
        }

        private Dictionary<string, DateTime> lastScanTimes = new Dictionary<string, DateTime>();
        private bool ShouldScan(Tag tag)
        {
            if (!lastScanTimes.ContainsKey(tag.Id))
            {
                lastScanTimes[tag.Id] = DateTime.Now;
                return true;
            }
            var elapsed = DateTime.Now - lastScanTimes[tag.Id];
            int scanTimeMs = 1000 * ((tag is DigitalTag d) ? d.ScanTime : (tag is AnalogTag a ? a.ScanTime : 1));
            if (elapsed.TotalMilliseconds >= scanTimeMs)
            {
                lastScanTimes[tag.Id] = DateTime.Now;
                return true;
            }
            return false;
        }

        private void Context_AlarmActivated(object sender, ActivatedAlarm alarmInfo)
        {
            Dispatcher.Invoke(() =>
            {
                AlarmMessagesListBox.Items.Add($"[{alarmInfo.Time:HH:mm:ss}] Alarm na tagu {alarmInfo.TagName} ({alarmInfo.AlarmId}): {alarmInfo.Message}");
            });
        }

        private void LoadData()
        {
            // Učitaj tagove
            TagsDataGrid.ItemsSource = context.Tags.Values.ToList();
            // Prikaz svih alarma sa svih AI tagova
            var allAlarms = context.Tags.Values
                .OfType<AnalogTag>()
                .Where(t => t.Type == TagType.AI)
                .SelectMany(t => t.Alarms.Select(a => new { TagId = t.Id, Alarm = a }))
                .ToList();
            AlarmsDataGrid.ItemsSource = allAlarms;
            if (OutputsDataGrid != null)
            {
                var outputs = context.Tags.Values
                    .Where(t => t.Type == TagType.DO || t.Type == TagType.AO)
                    .Select(t => new OutputTagViewModel
                    {
                        Id = t.Id,
                        Description = t.Description,
                        IOAddress = t.IOAddress,
                        Type = t.Type.ToString(),
                        Value = t is DigitalTag ? Convert.ToDouble(((DigitalTag)t).Properties.ContainsKey("Value") ? ((DigitalTag)t).Properties["Value"] : 0) :
                                t is AnalogTag ? ((AnalogTag)t).InitialValue : 0
                    })
                    .ToList();
                OutputsDataGrid.ItemsSource = outputs;
            }
        }

        private void ReportButton_Click(object sender, RoutedEventArgs e)
        {
            // Generiši .txt izveštaj za analogne ulaze u opsegu (high+low)/2 ± 5
            var analogInputs = context.Tags.Values.OfType<AnalogTag>().Where(t => t.Type == TagType.AI).ToList();
            var lines = new List<string>();
            foreach (var tag in analogInputs)
            {
                double sredina = (tag.HighLimit + tag.LowLimit) / 2.0;
                double min = sredina - 5;
                double max = sredina + 5;
                // Pretpostavljamo da postoji lista vrednosti (stub)
                // Ovdje bi trebalo čitati iz baze ili memorije sve vrednosti tog taga
                // Primer: lines.Add($"{tag.Id}: vrednosti u opsegu {min} - {max}");
                lines.Add($"{tag.Id} ({tag.Description}): vrednosti u opsegu {min} - {max}");
            }
            string path = $"SCADA_REPORT_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            File.WriteAllLines(path, lines);
            MessageBox.Show($"Izveštaj je sačuvan u fajl: {path}", "REPORT");
        }

        private void AddTagButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var typeStr = (TagTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (!Enum.TryParse(typeStr, out TagType tagType))
                {
                    MessageBox.Show("Izaberi tip taga.");
                    return;
                }
                string id = TagIdTextBox.Text.Trim();
                string desc = TagDescTextBox.Text.Trim();
                string addr = TagAddrTextBox.Text.Trim();
                int scanTime = 0;
                int.TryParse(TagScanTimeTextBox.Text, out scanTime);
                bool onOffScan = TagOnOffScanCheckBox.IsChecked == true;
                double low = 0, high = 0, initVal = 0;
                double.TryParse(TagLowLimitTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out low);
                double.TryParse(TagHighLimitTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out high);
                double.TryParse(TagInitValueTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out initVal);
                string units = TagUnitsTextBox.Text.Trim();

                // Validacija po tipu taga
                if ((tagType == TagType.DI || tagType == TagType.DO) && !string.IsNullOrWhiteSpace(units))
                {
                    MessageBox.Show("Ne možeš uneti units za digitalne tagove.");
                    return;
                }
                if ((tagType == TagType.DI || tagType == TagType.DO) && (low != 0 || high != 0))
                {
                    MessageBox.Show("Ne možeš uneti low/high limit za digitalne tagove.");
                    return;
                }
                if ((tagType == TagType.DI || tagType == TagType.AI) && !string.IsNullOrWhiteSpace(TagInitValueTextBox.Text) && initVal != 0)
                {
                    MessageBox.Show("Initial value možeš uneti samo za AO tagove.");
                    return;
                }
                if ((tagType == TagType.DO || tagType == TagType.AO) && (!string.IsNullOrWhiteSpace(TagScanTimeTextBox.Text) && scanTime != 0 || TagOnOffScanCheckBox.IsChecked == true))
                {
                    MessageBox.Show("Scan time i On/Off Scan možeš uneti samo za input tagove.");
                    return;
                }

                Tag tag = null;
                if (tagType == TagType.DI || tagType == TagType.DO)
                {
                    tag = new DigitalTag(tagType, id, desc, addr, scanTime, onOffScan);
                }
                else if (tagType == TagType.AI || tagType == TagType.AO)
                {
                    tag = new AnalogTag(tagType, id, desc, addr, low, high, units, scanTime, onOffScan, initVal);
                }
                if (tag == null)
                {
                    MessageBox.Show("Greška pri unosu taga.");
                    return;
                }
                if (!context.AddTag(tag))
                {
                    MessageBox.Show("Tag nije dodat. Proveri ID i podatke.");
                    return;
                }
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška: {ex.Message}");
            }
        }

        private void AddAlarmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(TagsDataGrid.SelectedItem is AnalogTag selectedTag) || selectedTag.Type != TagType.AI)
                {
                    MessageBox.Show("Selektuj AI tag za alarm.");
                    return;
                }
                var alarmTypeStr = (AlarmTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (!Enum.TryParse(alarmTypeStr, out AlarmType alarmType))
                {
                    MessageBox.Show("Izaberi tip alarma.");
                    return;
                }
                double limit = 0;
                double.TryParse(AlarmLimitTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out limit);
                string msg = AlarmMsgTextBox.Text.Trim();
                if (!context.AddAlarmToAnalogInput(selectedTag.Id, alarmType, limit, msg))
                {
                    MessageBox.Show("Alarm nije dodat. Proveri podatke ili duplikat.");
                    return;
                }
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška: {ex.Message}");
            }
        }

        private void DeleteTagButton_Click(object sender, RoutedEventArgs e)
        {
            if (TagsDataGrid.SelectedItem is Tag selectedTag)
            {
                if (MessageBox.Show($"Da li ste sigurni da želite da obrišete tag '{selectedTag.Id}'?", "Potvrda", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    context.RemoveTag(selectedTag.Id);
                    LoadData();
                }
            }
            else
            {
                MessageBox.Show("Selektujte tag za brisanje.");
            }
        }

        private void DeleteAlarmButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = AlarmsDataGrid.SelectedItem;
            if (selected != null)
            {
                var tagIdProp = selected.GetType().GetProperty("TagId");
                var alarmProp = selected.GetType().GetProperty("Alarm");
                if (tagIdProp != null && alarmProp != null)
                {
                    string tagId = tagIdProp.GetValue(selected)?.ToString();
                    var alarm = alarmProp.GetValue(selected) as DataConcentrator.Alarm;
                    if (!string.IsNullOrEmpty(tagId) && alarm != null)
                    {
                        if (MessageBox.Show($"Da li ste sigurni da želite da obrišete alarm ({alarm.Type}, {alarm.Limit}) sa taga '{tagId}'?", "Potvrda", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            context.RemoveAlarmFromAnalogInput(tagId, alarm.Type, alarm.Limit);
                            LoadData();
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Selektujte alarm za brisanje.");
            }
        }

        private void TagTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var typeStr = (TagTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (!Enum.TryParse(typeStr, out TagType tagType))
                return;

            // ScanTime i OnOffScan samo za input tagove
            if (TagScanTimeTextBox != null)
                TagScanTimeTextBox.Visibility = (tagType == TagType.DI || tagType == TagType.AI) ? Visibility.Visible : Visibility.Collapsed;
            if (TagOnOffScanCheckBox != null)
                TagOnOffScanCheckBox.Visibility = (tagType == TagType.DI || tagType == TagType.AI) ? Visibility.Visible : Visibility.Collapsed;

            // Low/High limit i Units samo za analogne tagove
            if (TagLowLimitTextBox != null)
                TagLowLimitTextBox.Visibility = (tagType == TagType.AI || tagType == TagType.AO) ? Visibility.Visible : Visibility.Collapsed;
            if (TagHighLimitTextBox != null)
                TagHighLimitTextBox.Visibility = (tagType == TagType.AI || tagType == TagType.AO) ? Visibility.Visible : Visibility.Collapsed;
            if (TagUnitsTextBox != null)
                TagUnitsTextBox.Visibility = (tagType == TagType.AI || tagType == TagType.AO) ? Visibility.Visible : Visibility.Collapsed;

            // InitialValue samo za AO
            if (TagInitValueTextBox != null)
                TagInitValueTextBox.Visibility = (tagType == TagType.AO) ? Visibility.Visible : Visibility.Collapsed;
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            context.SaveConfigurationToDatabase();
            base.OnClosing(e);
        }
        private void SetOutputButton_Click(object sender, RoutedEventArgs e)
        {
            if (OutputsDataGrid == null) return;
            var selected = OutputsDataGrid.SelectedItem;
            if (selected == null)
            {
                MessageBox.Show("Selektujte izlazni tag.");
                return;
            }
            var idProp = selected.GetType().GetProperty("Id");
            var valueProp = selected.GetType().GetProperty("Value");
            if (idProp == null || valueProp == null)
                return;
            string id = idProp.GetValue(selected)?.ToString();
            double value = 0;
            double.TryParse(valueProp.GetValue(selected)?.ToString(), out value);
            if (!string.IsNullOrEmpty(id))
            {
                if (context.SetOutputValue(id, value))
                {
                    MessageBox.Show("Vrednost izlaza postavljena.");
                    LoadData();
                }
                else
                {
                    MessageBox.Show("Greška pri postavljanju izlaza.");
                }
            }
        }
    } // Kraj klase MainWindow
} // Kraj namespace-a
