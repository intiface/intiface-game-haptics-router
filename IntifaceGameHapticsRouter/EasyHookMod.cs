using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels.Ipc;
using System.Text;
using EasyHook;
using GHRXInputModInterface;
using NLog;
using SharpMonoInjector;

namespace IntifaceGameHapticsRouter
{
    abstract class EasyHookMod
    {
        public EventHandler<GHRProtocolMessageContainer> MessageReceivedHandler;
        private IpcServerChannel _hookServer;
        private string _channelName;
        protected Logger _log;

        /// <summary>
        /// Denotes whether we can use XInput mods with this process.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        /// <remarks>This is basically a copy of SharpMonoInjector.ProcessUtils.GetMonoModule, just shuffled a bit for checking for xinput.</remarks>
        public abstract bool CanUseMod(IntPtr handle);

        protected bool CanUseMod(IntPtr handle, string requiredLibrary)
        {
            var size = ProcessUtils.Is64BitProcess(handle) ? 8 : 4;

            var ptrs = new IntPtr[0];

            if (!Native.EnumProcessModulesEx(
                handle, ptrs, 0, out int bytesNeeded, ModuleFilter.LIST_MODULES_ALL))
            {
                throw new InjectorException("Failed to enumerate process modules", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            var count = bytesNeeded / size;
            ptrs = new IntPtr[count];

            if (!Native.EnumProcessModulesEx(
                handle, ptrs, bytesNeeded, out bytesNeeded, ModuleFilter.LIST_MODULES_ALL))
            {
                throw new InjectorException("Failed to enumerate process modules", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            for (var i = 0; i < count; i++)
            {
                StringBuilder path = new StringBuilder(260);
                try
                {
                    Native.GetModuleFileNameEx(handle, ptrs[i], path, 260);
                }
                catch { continue; }
                //Debug.WriteLine(path.ToString());

                if (path.ToString().IndexOf(requiredLibrary, StringComparison.OrdinalIgnoreCase) > -1)
                {
                    return true;
                }
            }

            return false;
        }

        public EasyHookMod()
        {
            _log = LogManager.GetCurrentClassLogger();
        }

        public abstract void Attach(int aProcessId);

        protected void Attach<T>(int aProcessId, string payloadName)
        {
            try
            {
                _hookServer = RemoteHooking.IpcCreateServer<GHRXInputModInterface.GHRXInputModInterface>(
                    ref _channelName,
                    WellKnownObjectMode.Singleton);
                var dllFile = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(T).Assembly.Location),
                    payloadName);

                _log.Info($"Beginning process injection on {aProcessId}...");
                _log.Info($"Injecting DLL {dllFile}");

                RemoteHooking.Inject(
                    aProcessId,
                    InjectionOptions.Default,
                    dllFile,
                    dllFile,
                    // the optional parameter list...
                    _channelName);
                _log.Info($"Finished process injection on {aProcessId}...");
            }
            catch (Exception ex)
            {
                Detach();
                _log.Error(ex);
            }
        }

        public void Detach()
        {
            GHRXInputModInterface.GHRXInputModInterface.Detach();
            _channelName = null;
            _hookServer = null;
        }

        protected abstract void OnVibrationCommand(object aObj, Vibration aVibration);

        protected void OnVibrationException(object aObj, Exception aEx)
        {
            _log.Error($"Remote Exception: {aEx}");
            Detach();
        }

        protected void OnVibrationLogMessage(object aObj, string aMsg)
        {
            _log.Info($"EasyHookMod: {aMsg}");
        }

        protected void OnVibrationPingMessage(object aObj, EventArgs aIgnored)
        {

        }

        protected void OnVibrationExit(object aObj, EventArgs aIgnored)
        {
            Detach();
        }
    }
}
