using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using NLog;
using NLog.Config;
using NLog.Targets;
using Buttplug;

namespace IntifaceGameHapticsRouter
{
    /// <summary>
    /// Interaction logic for LogControl.xaml
    /// </summary>

    public class LogList : ObservableCollection<string>
    {
    }

    [Target("IntifaceLogger")]
    public sealed class IntifaceNLogTarget : TargetWithLayoutHeaderAndFooter
    {
        private readonly LogList _logs;
        private readonly object _logLock = new object();
        public long MaxLogs;

        public IntifaceNLogTarget(LogList aList, long aMaxLogs = 1000)
        {
            _logs = aList;
            MaxLogs = aMaxLogs;
            BindingOperations.EnableCollectionSynchronization(_logs, _logLock);
        }

        protected override void Write(LogEventInfo aLogEvent)
        {
            _logs.Add(Layout.Render(aLogEvent));
            while (_logs.Count > MaxLogs)
            {
                _logs.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class LogControl : UserControl
    {
        private static LogList _logs;
        private readonly IntifaceNLogTarget _logTarget;
        private LoggingRule _outgoingLoggingRule;

        public long MaxLogs
        {
            get
            {
                return _logTarget.MaxLogs;
            }

            set
            {
                _logTarget.MaxLogs = value;
            }
        }

        public LogControl()
        {
            var c = LogManager.Configuration ?? new LoggingConfiguration();
            _logs = new LogList();

            InitializeComponent();

            // Null check Dispatcher, otherwise test bringup for GUI tests will fail.
            if (Dispatcher != null)
            {
                _logTarget = new IntifaceNLogTarget(_logs);
                c.AddTarget("IntifaceLogger", _logTarget);
                _outgoingLoggingRule = new LoggingRule("*", LogLevel.Debug, _logTarget);
                c.LoggingRules.Add(_outgoingLoggingRule);
                LogManager.Configuration = c;
            }

            //LogLevelComboBox.SelectionChanged += LogLevelSelectionChangedHandler;
            LogListBox.ItemsSource = _logs;
            ButtplugFFILog.StartLogHandler(ButtplugLogLevel.Info, false);
            Buttplug.ButtplugFFILog.LogMessage += (obj, msg) => _logs.Add(msg.Trim());
        }

        public void AddLogMessage(string aMsg)
        {
            _logs.Add(aMsg);
        }

        public string[] GetLogs()
        {
            return _logs.ToArray();
        }

        private void SaveLogFileButton_Click(object aSender, RoutedEventArgs aEvent)
        {
            var dialog = new SaveFileDialog
            {
                CheckFileExists = false,
                CheckPathExists = true,
                OverwritePrompt = true,
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var sw = new System.IO.StreamWriter(dialog.FileName, false);
            foreach (var line in _logs.ToList())
            {
                sw.WriteLine(line);
            }

            sw.Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _logs.Clear();
        }

        private void LogListBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender != LogListBox)
            {
                return;
            }

            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.C)
            {
                var builder = new StringBuilder();
                foreach (var item in LogListBox.SelectedItems)
                {
                    if (item is string)
                    {
                        builder.AppendLine(item as string);
                    }
                    else if (item is ListBoxItem)
                    {
                        builder.AppendLine((item as ListBoxItem).Content as string);
                    }
                }

                try
                {
                    Clipboard.SetText(builder.ToString());
                }
                catch (Exception ex)
                {
                    // We've seen weird instances of can't open clipboard
                    // but it's pretty rare.
                }
            }
        }
    }
}
