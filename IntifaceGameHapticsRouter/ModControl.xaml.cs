using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EasyHook;
using NLog;
using SharpMonoInjector;

namespace IntifaceGameHapticsRouter
{
    /// <summary>
    /// Interaction logic for ProcessControl.xaml
    /// </summary>
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>

    public partial class ModControl
    {
        public EventHandler<GHRProtocolMessageContainer> MessageReceivedHandler;

        public class ProcessInfo
        {
            public string FileName;
            public int Id;
            public string Owner;
            public IntPtr MonoModule = IntPtr.Zero;
            public UnityVRMod.NetFramework FrameworkVersion = UnityVRMod.NetFramework.UNKNOWN;

            public bool CanUseXInput => !string.IsNullOrEmpty(Owner);

            public bool CanUseMono => MonoModule != IntPtr.Zero;

            public override string ToString()
            {
                var f = System.IO.Path.GetFileNameWithoutExtension(FileName);
                return $"{f} ({Id}) ({(CanUseMono ? $"Mono/{FrameworkVersion}" : "")}{(CanUseXInput && CanUseMono ? " | " : "")}{(CanUseXInput ? "XInput" : "")})";
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
        private UnityVRMod _unityMod;

        public ModControl()
        {
            InitializeComponent();
            _log = LogManager.GetCurrentClassLogger();
            ProcessListBox.ItemsSource = _processList;
            ProcessListBox.SelectionChanged += OnSelectionChanged;

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
            const ProcessAccessRights flags = ProcessAccessRights.PROCESS_QUERY_INFORMATION | ProcessAccessRights.PROCESS_VM_READ;
            foreach (var currentProc in from proc in Process.GetProcesses() orderby proc.ProcessName select proc)
            {
                var handle = IntPtr.Zero;

                try
                {
                    // This can sometimes happen between calling GetProcesses and getting here. Save ourselves the throw.
                    if (currentProc.HasExited || currentProc.Id == cp)
                    {
                        continue;
                    }

                    // This is usually what throws, so do it before we invoke via dispatcher.
                    // var owner = RemoteHooking.GetProcessIdentity(currentProc.Id).Name;

                    if ((handle = Native.OpenProcess(flags, false, currentProc.Id)) == IntPtr.Zero)
                    {
                        continue;
                    }

                    var procInfo = new ProcessInfo
                    {
                        FileName = currentProc.ProcessName,
                        Id = currentProc.Id,
                    };
                    /*
                    if (XInputMod.CanUseMod(handle))
                    {
                        procInfo.Owner = owner;
                    }
                    */
                    if (UnityVRMod.CanUseMod(handle, currentProc.MainModule.FileName, out var module, out var frameworkVersion))
                    {
                        procInfo.MonoModule = module;
                        procInfo.FrameworkVersion = frameworkVersion;
                    }

                    if (procInfo.CanUseXInput || procInfo.CanUseMono)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _log.Debug(procInfo);
                            _processList.Add(procInfo);
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
                    // Only close the 
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

        private void OnMessageReceived(object aObj, GHRProtocolMessageContainer aMsg)
        {
            Dispatcher.Invoke(() =>
            {
                MessageReceivedHandler?.Invoke(this, aMsg);
            });
        }

        private void AttachButton_Click(object aObj, System.Windows.RoutedEventArgs aEvent)
        {
            AttachButton.IsEnabled = false;
            RefreshButton.IsEnabled = false;
            if (!_attached)
            {
                var process = ProcessListBox.SelectedItems.Cast<ProcessInfo>().ToList();
                if (process[0].CanUseMono)
                {
                    _unityMod = new UnityVRMod();
                    _unityMod.Inject(process[0].Id, process[0].FrameworkVersion, process[0].MonoModule);
                    _unityMod.MessageReceivedHandler += OnMessageReceived;
                }
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

        private void Detach()
        {
            Attached = false;
        }
    }
}
