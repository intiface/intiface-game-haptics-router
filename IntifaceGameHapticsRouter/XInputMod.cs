using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels.Ipc;
using System.Text;
using System.Threading.Tasks;
using EasyHook;
using GHRXInputModInterface;
using NLog;
using SharpMonoInjector;

namespace IntifaceGameHapticsRouter
{
    class XInputMod
    {
        private IpcServerChannel _xinputHookServer;
        private string _channelName;
        private Logger _log;

        /// <summary>
        /// Denotes whether we can use XInput mods with this process.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        /// <remarks>This is basically a copy of SharpMonoInjector.ProcessUtils.GetMonoModule, just shuffled a bit for checking for xinput.</remarks>
        public static bool CanUseMod(IntPtr handle)
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
                Native.GetModuleFileNameEx(handle, ptrs[i], path, 260);

                if (path.ToString().IndexOf("xinput", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    return true;
                }
            }

            return false;
        }

        public XInputMod()
        {
            _log = LogManager.GetCurrentClassLogger();
            GHRXInputModInterface.GHRXInputModInterface.VibrationCommandReceived += OnVibrationCommand;
            GHRXInputModInterface.GHRXInputModInterface.VibrationPingMessageReceived += OnVibrationPingMessage;
            GHRXInputModInterface.GHRXInputModInterface.VibrationExceptionReceived += OnVibrationException;
            GHRXInputModInterface.GHRXInputModInterface.VibrationExitReceived += OnVibrationExit;
        }

        private void Attach(int aProcessId)
        {
            try
            {
                _xinputHookServer = RemoteHooking.IpcCreateServer<GHRXInputModInterface.GHRXInputModInterface>(
                    ref _channelName,
                    WellKnownObjectMode.Singleton);
                var dllFile = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(GHRXInputModPayload.GHRXInputModPayload).Assembly.Location),
                    "GVRXInputModPayload.dll");

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

        private void Detach()
        {
            GHRXInputModInterface.GHRXInputModInterface.Detach();
            _channelName = null;
            _xinputHookServer = null;
        }

        private void OnVibrationCommand(object aObj, Vibration aVibration)
        {

        }

        private void OnVibrationException(object aObj, Exception aEx)
        {
            _log.Error($"Remote Exception: {aEx}");
            Detach();
        }

        private void OnVibrationPingMessage(object aObj, string aMsg)
        {
            _log.Info($"Remote Ping Message: {aMsg}");
        }

        private void OnVibrationExit(object aObj, bool aTrue)
        {
            Detach();
        }
    }
}
