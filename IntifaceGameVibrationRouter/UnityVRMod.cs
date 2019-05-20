using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using EasyHook;
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
            finally
            {
                Native.CloseHandle(handle);
            }

            module = IntPtr.Zero;
            return false;
        }

        private Task _readerTask;

        public UnityVRMod()
        {

            _readerTask = new Task(async () => await StdInReader());
            _readerTask.Start();
        }

        private async Task StdInReader()
        {
            var pipeServer = new NamedPipeServerStream("GVRPipe", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await pipeServer.WaitForConnectionAsync().ConfigureAwait(false);
            string line;
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
                            msg += Encoding.UTF8.GetString(buffer, 0, len);
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
