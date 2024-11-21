using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using EasyHook;
using NLog;
using SharpMonoInjector;
using System.Runtime.InteropServices;
using System.Security.Principal;

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
        
        private static string GetProcessUser(Process process)
        {
            IntPtr processHandle = IntPtr.Zero;
            try
            {
                OpenProcessToken(process.Handle, 8, out processHandle);
                WindowsIdentity wi = new WindowsIdentity(processHandle);
                string user = wi.Name;
                return user.Contains(@"\") ? user.Substring(user.IndexOf(@"\") + 1) : user;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (processHandle != IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                }
            }
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        public class ProcessInfo
        {
            public string FileName;
            public int Id;
            public string Owner;
            public bool isUWP;
            public IntPtr MonoModule = IntPtr.Zero;
//            public UnityVRMod.NetFramework FrameworkVersion = UnityVRMod.NetFramework.UNKNOWN;

            public bool CanUseXInput => !string.IsNullOrEmpty(Owner) && !isUWP;

            public bool CanUseUWP => !string.IsNullOrEmpty(Owner) && isUWP;

            public bool CanUseMono => MonoModule != IntPtr.Zero;

            public override string ToString()
            {
                var f = System.IO.Path.GetFileNameWithoutExtension(FileName);
                return $"{f} ({Id}) ({(CanUseXInput ? "XInput" : "")}{(CanUseUWP ? "UWP" : "")})";
            }

            public bool IsLive => Process.GetProcessById(Id) != null;
        }

        private class ProcessInfoList : ObservableCollection<ProcessInfo>
        {
        }

        public bool Attached
        {
            get
            {
                return _attached;
            }
            set
            {
                _attached = value;
                ProcessListBox.IsEnabled = !value;
                RefreshButton.IsEnabled = !value;
                AttachButton.IsEnabled = true;
                AttachButton.Content = value ? "Detach From Process" : "Attach To Process";
            }
        }

        public string ProcessStatus
        {
            set { StatusLabel.Content = value; }
        }

        private ProcessInfoList _processList = new ProcessInfoList();

        public event EventHandler<EventArgs> ProcessAttached;
        public event EventHandler<EventArgs> ProcessDetached;

        private bool _attached = false;
        private readonly Logger _log;
        private Task _enumProcessTask;
//        private UnityVRMod _unityMod;
        private EasyHookMod _easyHookMod;
        private CancellationTokenSource _scanningTokenSource = null;
        private CancellationToken _scanningToken;

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
            if (_scanningTokenSource != null)
            {
                _scanningTokenSource.Cancel();
                _enumProcessTask.Wait();
            }
            _scanningTokenSource = new CancellationTokenSource();
            _scanningToken = _scanningTokenSource.Token;
            _enumProcessTask = new Task(() => EnumProcesses());
            _enumProcessTask.Start();
        }

        private void EnumProcesses()
        {
            Dispatcher.Invoke(() => { _processList.Clear(); });
            Dispatcher.Invoke(() => { ProcessStatus = "Scanning Processes..."; });
            var cp = Process.GetCurrentProcess().Id;
            const ProcessAccessRights flags = ProcessAccessRights.PROCESS_QUERY_INFORMATION | ProcessAccessRights.PROCESS_VM_READ;
            var procList = from proc in Process.GetProcesses() orderby proc.ProcessName select proc;
            Parallel.ForEach(procList, (currentProc) =>
            {
                if (_scanningToken.IsCancellationRequested)
                {
                    return;
                }
                var handle = IntPtr.Zero;

                try
                {
                    // This can sometimes happen between calling GetProcesses and getting here. Save ourselves the throw.
                    if (currentProc.HasExited || currentProc.Id == cp)
                    {
                        return;
                    }

                    // This is usually what throws, so do it before we invoke via dispatcher.
                    var owner = RemoteHooking.GetProcessIdentity(currentProc.Id).Name;

                    if ((handle = Native.OpenProcess(flags, false, currentProc.Id)) == IntPtr.Zero)
                    {
                        return;
                    }
                    
                    if (new XInputMod().CanUseMod(handle) || procInfo.FileName == "steam")
                    {
                        var procInfo = new ProcessInfo
                        {
                            FileName = currentProc.ProcessName,
                            Id = currentProc.id,
                            Owner = owner;
                        };
                        Dispatcher.Invoke(() =>
                        {
                            _log.Debug(procInfo);
                            _processList.Add(procInfo);
                        });
                    }

                    if (new UWPInputMod().CanUseMod(handle))
                    {
                        var procInfo = new ProcessInfo
                        {
                            FileName = currentProc.ProcessName,
                            Id = currentProc.id,
                            Owner = owner;
                            isUWP = true;
                        };
                        Dispatcher.Invoke(() =>
                        {
                            _log.Debug(procInfo);
                            _processList.Add(procInfo);
                        });
                    }

                    /*
                    if (UnityVRMod.CanUseMod(handle, currentProc.MainModule.FileName, out var module, out var frameworkVersion))
                    {
                        var procInfo = new ProcessInfo
                        {
                            FileName = currentProc.ProcessName,
                            Id = currentProc.id,
                            MonoModule = module;
                            FrameworkVersion = frameworkVersion;
                        };
                        Dispatcher.Invoke(() =>
                        {
                            _log.Debug(procInfo);
                            _processList.Add(procInfo);
                        });
                    }
                    */
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
                    _log.Error(aEx);
                }
                finally
                {
                    // Only close the 
                    if (handle != IntPtr.Zero)
                    {
                        Native.CloseHandle(handle);
                    }
                }
            });
            if (!_attached)
            {
                Dispatcher.Invoke(() => { ProcessStatus = "Select Process to Inject"; });
            }
            _scanningTokenSource = null;
            _enumProcessTask = null;
        }

        private void OnSelectionChanged(object aObj, EventArgs aEvent)
        {
            AttachButton.IsEnabled = ProcessListBox.SelectedItems.Count == 1;
        }

        private void OnMessageReceived(object aObj, GHRProtocolMessageContainer aMsg)
        {
          MessageReceivedHandler?.Invoke(this, aMsg);
        }

        private void AttachButton_Click(object aObj, System.Windows.RoutedEventArgs aEvent)
        {
            if (!Attached)
            {
                if (_scanningTokenSource != null && _scanningToken.CanBeCanceled)
                {
                    _scanningTokenSource.Cancel();
                }

                var process = ProcessListBox.SelectedItems.Cast<ProcessInfo>().ToList()[0];
                if (!process.IsLive)
                {
                    return;
                }

                AttachButton.IsEnabled = false;
                RefreshButton.IsEnabled = false;
                ProcessListBox.IsEnabled = false;

                var attached = false;
                try
                {
                    /*
                    if (process.CanUseMono)
                    {
                        _unityMod = new UnityVRMod();
                        _unityMod.MessageReceivedHandler += OnMessageReceived;
                        _unityMod.Inject(process.Id, process.FrameworkVersion, process.MonoModule);
                        attached = true;
                    }
                    */

                    if (process.CanUseUWP)
                    {
                        _easyHookMod = new UWPInputMod();
                        _easyHookMod.Attach(process.Id);
                        _easyHookMod.MessageReceivedHandler += OnMessageReceived;
                        attached = true;
                    }

                    if (process.CanUseXInput && _easyHookMod == null)
                    {
                        _easyHookMod = new XInputMod();
                        _easyHookMod.Attach(process.Id);
                        _easyHookMod.MessageReceivedHandler += OnMessageReceived;
                        attached = true;
                    }

                    if (attached)
                    {
                        Attached = true;
                        ProcessAttached?.Invoke(this, null);
                        ProcessStatus = $"Attached to {process.FileName} ({process.Id})";
                    }
                } 
                catch
                {
                    Attached = false;
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
            _easyHookMod.Detach();
            _easyHookMod = null;
            Attached = false;
        }
    }
}
