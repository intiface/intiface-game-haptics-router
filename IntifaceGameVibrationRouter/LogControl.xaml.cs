using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Buttplug.Core.Logging;
using Microsoft.Win32;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace IntifaceGameVibrationRouter
{
    /// <summary>
    /// Interaction logic for LogControl.xaml
    /// </summary>

    public class LogList : ObservableCollection<string>
    {
    }

    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class LogControl : UserControl
    {
        private readonly LogList _logs;
        private LoggingRule _outgoingLoggingRule;

        public LogControl()
        {
            var c = LogManager.Configuration ?? new LoggingConfiguration();
            _logs = new LogList();

            InitializeComponent();
            //LogLevelComboBox.SelectionChanged += LogLevelSelectionChangedHandler;
            LogListBox.ItemsSource = _logs;
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
                    // but it's pretty rare. Log it.
                }
            }
        }
    }
}
