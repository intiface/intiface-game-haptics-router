using System;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Threading.Tasks;
using NLog;
using SharpMonoInjector;

namespace IntifaceGameHapticsRouter
{
    public class UnityVRMod
    {
        public static bool CanUseMod(IntPtr handle, string processPath, out IntPtr module, out NetFramework frameworkVersion)
        {
            frameworkVersion = NetFramework.UNKNOWN;
            try
            {
                if (!ProcessUtils.GetMonoModule(handle, out module))
                {
                    return false;
                }

                if (!GetNetFrameworkVersion(processPath, out frameworkVersion))
                {
                    return false;
                }

                return true;
            }
            catch (InjectorException ex)
            {
                // Noop and just return false.
                // TODO Maybe log here too.
            }

            module = IntPtr.Zero;
            return false;
        }

        public enum NetFramework
        {
            // Ok so this is a little weird. Older Unity games are actually Mono v2, and in GetNetFrameworkVersion
            // they'll report something like v2.0.50727. However, this is actually .Net Framework 3.5 COMPATIBLE, 
            // but it lists itself as .Net 2.0 because it's apparently missing some stuff or something. Anyways, we
            // can usually load our .Net 3.5 mod in these older games and not have a problem.
            NET35 = 2,
            NET45 = 4,
            UNKNOWN = 0,
        }

        protected static bool GetNetFrameworkVersion(string aProcessPath, out NetFramework frameworkVersion)
        {
            
            // If someone is asking us this, we can assume they've already checked they can use this mod.
            // We'll also assume it's Unity, and that there's an Assembly-CSharp.dll file somewhere in the tree below the process file.
            Path.GetDirectoryName(aProcessPath);
            var assemblyFiles = Directory.GetFiles(Path.GetDirectoryName(aProcessPath), "*Assembly-CSharp.dll",
                SearchOption.AllDirectories);
            if (assemblyFiles.Length != 1)
            {
                frameworkVersion = NetFramework.UNKNOWN;
                return false;
            }
            var netVersion = Assembly.LoadFrom(assemblyFiles[0]).ImageRuntimeVersion;
            if (netVersion.Contains("v4"))
            {
                frameworkVersion = NetFramework.NET45;
                return true;
            }
            if (netVersion.Contains("v2"))
            {
                frameworkVersion = NetFramework.NET35;
                return true;
            }
            frameworkVersion = NetFramework.UNKNOWN;
            return false;
        }

        private Logger _log;
        private Task _readerTask;
        private bool _isExecuting;
        public EventHandler<GHRProtocolMessageContainer> MessageReceivedHandler;

        public UnityVRMod()
        {
            _log = LogManager.GetCurrentClassLogger();

        }

        public void Inject(int aProcessId, NetFramework aFrameworkVersion, IntPtr aMonoModule)
        {
            _readerTask = new Task(async () => await NamedPipeReader());
            _readerTask.Start();

            var handle = Native.OpenProcess(ProcessAccessRights.PROCESS_ALL_ACCESS, false, aProcessId);

            if (handle == IntPtr.Zero)
            {
                _log.Error("Failed to open process");
                return;
            }

            byte[] file;

            try
            {
                if (aFrameworkVersion == NetFramework.NET45)
                {
                    file = File.ReadAllBytes("./GHRUnityVRModNet45.dll");
                }
                else if (aFrameworkVersion == NetFramework.NET35)
                {
                    file = File.ReadAllBytes("./GHRUnityVRModNet35.dll");
                }
                else
                {
                    throw new ArgumentException($"Passed unusable framework version {aFrameworkVersion} to Inject");
                }
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
                    IntPtr asm = injector.Inject(file, "GHRUnityVRMod", "GHRUnityVRModFuncs", "Load");
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

        private async Task NamedPipeReader()
        {
            var pipeServer = new NamedPipeServerStream("GHRPipe", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
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
                            var gvrMsg = GHRProtocolMessageContainer.Deserialize(new MemoryStream(buffer, 0, len));
                            MessageReceivedHandler?.Invoke(this, gvrMsg);
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
