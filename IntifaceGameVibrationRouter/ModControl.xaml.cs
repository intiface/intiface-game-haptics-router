using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels.Ipc;
using System.Text;
using System.Threading.Tasks;
using Buttplug.Logging;
using EasyHook;
using SharpMonoInjector;

namespace IntifaceGameVibrationRouter
{
    /// <summary>
    /// Interaction logic for ProcessControl.xaml
    /// </summary>
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>

    public partial class ModControl
    {
        public EventHandler<GVRProtocolMessage> GvrProtocolMessageHandler;

        public class ProcessInfo
        {
            public string FileName;
            public int Id;
            public string Owner;
            public IntPtr MonoModule = IntPtr.Zero;

            public override string ToString()
            {
                var f = System.IO.Path.GetFileNameWithoutExtension(FileName);
                return $"{f} ({Id}) ({(MonoModule != IntPtr.Zero ? "Mono" : "XInput")})";
            }
        }

        private class ProcessInfoList : ObservableCollection<ProcessInfo>
        {
        }

        public bool Attached
        {
            set
            {
                _attached = value;
                ProcessListBox.IsEnabled = !value;
                RefreshButton.IsEnabled = !value;
                AttachButton.IsEnabled = true;
                AttachButton.Content = value ? "Detach From Process" : "Attach To Process";
            }
        }

        public string ProcessError
        {
            set { ErrorLabel.Content = value; }
        }

        private ProcessInfoList _processList = new ProcessInfoList();

        public event EventHandler<int> ProcessAttachRequested;

        public event EventHandler<bool> ProcessDetachRequested;

        private bool _attached;
        private readonly Logger _log;
        private Task _enumProcessTask;

        public ModControl()
        {
            InitializeComponent();
            ProcessListBox.ItemsSource = _processList;
            ProcessListBox.SelectionChanged += OnSelectionChanged;

            //GameVibrationRouterInterface.VibrationCommandReceived += OnVibrationCommand;
            GVRXInputModInterface.GVRXInputModInterface.VibrationPingMessageReceived += OnVibrationPingMessage;
            GVRXInputModInterface.GVRXInputModInterface.VibrationExceptionReceived += OnVibrationException;
            GVRXInputModInterface.GVRXInputModInterface.VibrationExitReceived += OnVibrationExit;
            RunEnumProcessUpdate();
        }

        private void RunEnumProcessUpdate()
        {
            if (_enumProcessTask != null)
            {
                return;
            }
            _enumProcessTask = new Task(() => EnumProcesses());
            _enumProcessTask.Start();
        }

        private void EnumProcesses()
        {
            Dispatcher.Invoke(() => { _processList.Clear(); });
            Dispatcher.Invoke(() => { ProcessError = "Scanning Processes..."; });
            var cp = Process.GetCurrentProcess().Id;
            foreach (var currentProc in from proc in Process.GetProcesses() orderby proc.ProcessName select proc)
            {
                var handle = IntPtr.Zero;
                // This can sometimes happen between calling GetProcesses and getting here. Save ourselves the throw.
                if (currentProc.HasExited || currentProc.Id == cp)
                {
                    continue;
                }
                const ProcessAccessRights flags = ProcessAccessRights.PROCESS_QUERY_INFORMATION | ProcessAccessRights.PROCESS_VM_READ;

                try
                {
                    // This is usually what throws, so do it before we invoke via dispatcher.
                    var owner = RemoteHooking.GetProcessIdentity(currentProc.Id).Name;

                    if ((handle = Native.OpenProcess(flags, false, currentProc.Id)) == IntPtr.Zero)
                    {
                        continue;
                    }
                    if (XInputMod.CanUseMod(handle))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _processList.Add(new ProcessInfo
                            {
                                FileName = currentProc.ProcessName,
                                Id = currentProc.Id,
                                Owner = owner
                            });
                        });
                    }
                    if (UnityVRMod.CanUseMod(handle, out var module))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _processList.Add(new ProcessInfo
                            {
                                FileName = currentProc.ProcessName,
                                Id = currentProc.Id,
                                Owner = owner,
                                MonoModule = module,
                            });
                        });
                    }
                }
                catch (AccessViolationException)
                {
                    // noop, there's a lot of system processes we can't see.
                }
                catch (Win32Exception)
                {
                    // noop, there's a lot of system processes we can't see.
                }
                catch (Exception aEx)
                {
                    // _log.Error(aEx);
                }
                finally
                {
                    if (handle != IntPtr.Zero)
                    {
                        Native.CloseHandle(handle);
                    }
                }
            }
            Dispatcher.Invoke(() => { ProcessError = "Select Process to Inject"; });
            _enumProcessTask = null;
        }

        private void OnSelectionChanged(object aObj, EventArgs aEvent)
        {
            AttachButton.IsEnabled = ProcessListBox.SelectedItems.Count == 1;
        }

        private void AttachButton_Click(object aObj, System.Windows.RoutedEventArgs aEvent)
        {
            AttachButton.IsEnabled = false;
            RefreshButton.IsEnabled = false;
            if (!_attached)
            {
                var process = ProcessListBox.SelectedItems.Cast<ProcessInfo>().ToList();
                //Attach(process[0].Id);
            }
            else
            {
                Detach();
            }
        }

        private void RefreshButton_Click(object aObj, System.Windows.RoutedEventArgs aEvent)
        {
            RunEnumProcessUpdate();
        }

        private void OnVibrationException(object aObj, Exception aEx)
        {
            //_log.Error($"Remote Exception: {aEx}");
            Dispatcher.Invoke(() =>
            {
                Detach();
                ProcessError = "Error attaching, see logs for details.";
            });
        }

        private void OnVibrationPingMessage(object aObj, string aMsg)
        {
            //_log.Info($"Remote Ping Message: {aMsg}");
        }

        private void OnVibrationExit(object aObj, bool aTrue)
        {
            Dispatcher.Invoke(() =>
            {
                Detach();
                ProcessError = "Attached process detached or exited";
            });
        }

        private void Detach()
        {
            GVRXInputModInterface.GVRXInputModInterface.Detach();
            Attached = false;
        }
    }
}
