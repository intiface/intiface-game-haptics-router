﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
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
                AttachButton.IsEnabled = _unityMod == null;
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
        private UnityVRMod _unityMod;
        private XInputMod _xinputMod;
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

                    var procInfo = new ProcessInfo
                    {
                        FileName = currentProc.ProcessName,
                        Id = currentProc.Id,
                    };

                    if (XInputMod.CanUseMod(handle))
                    {
                        procInfo.Owner = owner;
                    }

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
                    if (process.CanUseMono)
                    {
                        _unityMod = new UnityVRMod();
                        _unityMod.MessageReceivedHandler += OnMessageReceived;
                        _unityMod.Inject(process.Id, process.FrameworkVersion, process.MonoModule);
                        attached = true;
                    }

                    if (process.CanUseXInput)
                    {
                        _xinputMod = new XInputMod();
                        _xinputMod.Attach(process.Id);
                        _xinputMod.MessageReceivedHandler += OnMessageReceived;
                        attached = true;
                    }

                    if (attached)
                    {
                        Attached = true;
                        ProcessAttached?.Invoke(this, null);
                        ProcessStatus = $"Attached to {process.FileName} ({process.Id}) {(_unityMod != null ? " - Restart GHR to detach" : "")}";
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
            _xinputMod.Detach();
            _xinputMod = null;
            Attached = false;
        }
    }
}
