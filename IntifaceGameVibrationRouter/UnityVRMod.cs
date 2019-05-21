using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using EasyHook;
using NLog;
using SharpMonoInjector;

namespace IntifaceGameVibrationRouter
{
    class UnityVRMod
    {
        public static bool CanUseMod(IntPtr handle, out IntPtr module)
        {
            try
            {
                return ProcessUtils.GetMonoModule(handle, out module);
            }
            catch (InjectorException ex)
            {
                // Noop and just return false.
                // TODO Maybe log here too.
            }

            module = IntPtr.Zero;
            return false;
        }

        private Logger _log;
        private Task _readerTask;
        private bool _isExecuting;

        public UnityVRMod()
        {
            _log = LogManager.GetCurrentClassLogger();

        }

        public void Inject(int aProcessId, IntPtr aMonoModule)
        {
            _readerTask = new Task(async () => await StdInReader());
            _readerTask.Start();

            IntPtr handle = Native.OpenProcess(ProcessAccessRights.PROCESS_ALL_ACCESS, false, aProcessId);

            if (handle == IntPtr.Zero)
            {
                _log.Error("Failed to open process");
                return;
            }

            byte[] file;

            try
            {
                file = File.ReadAllBytes("./GVRUnityVRModNet45.dll");
            }
            catch (IOException)
            {
                _log.Error($"Failed to read the mod file");
                return;
            }

            _isExecuting = true;
            _log.Info($"Injecting");

            using (Injector injector = new Injector(handle, aMonoModule))
            {
                try
                {
                    IntPtr asm = injector.Inject(file, "GVRUnityVRMod", "GVRUnityVRModFuncs", "Load");
                    /*
                    InjectedAssemblies.Add(new InjectedAssembly
                    {
                        ProcessId = SelectedProcess.Id,
                        Address = asm,
                        Name = Path.GetFileName(AssemblyPath),
                        Is64Bit = injector.Is64Bit
                    });
                    */
                    _log.Info("Injection successful");
                }
                catch (InjectorException ie)
                {
                    _log.Error("Injection failed: " + ie.Message);
                }
                catch (Exception e)
                {
                    _log.Error("Injection failed (unknown error): " + e.Message);
                }
            }

            _isExecuting = false;
        }

        private async Task StdInReader()
        {
            var pipeServer = new NamedPipeServerStream("GVRPipe", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await pipeServer.WaitForConnectionAsync().ConfigureAwait(false);
            while (true)
            {
                var buffer = new byte[4096];
                var msg = string.Empty;
                var len = -1;
                while (len < 0 || (len == buffer.Length && buffer[4095] != '\0'))
                {
                    try
                    {
                        
                        len = await pipeServer.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                        if (len > 0)
                        {
                            Debug.WriteLine(System.Text.Encoding.UTF8.GetString(buffer, 0, len));
                            var gvrMsg = GVRProtocolMessageContainer.Deserialize(new MemoryStream(buffer, 0, len));
                            if (gvrMsg.Log != null)
                            {
                                _log.Info(gvrMsg.Log.Message);
                            }
                        }
                    }
                    catch
                    {
                        // no-op?
                    }
                }
            }
        }
    }
}
