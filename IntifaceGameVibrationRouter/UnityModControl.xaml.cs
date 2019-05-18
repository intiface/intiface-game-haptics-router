using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace IntifaceGameVibrationRouter
{
    /// <summary>
    /// Interaction logic for UnityModControl.xaml
    /// </summary>
    public partial class UnityModControl : UserControl
    {
        private Task _readerTask;

        public UnityModControl()
        {
            InitializeComponent();
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
                Dispatcher.Invoke(() => { _stdInLabel.Content = msg; });
            }
        }
    }
}
