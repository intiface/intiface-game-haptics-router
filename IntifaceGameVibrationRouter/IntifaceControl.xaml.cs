using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Windows.Controls;
using Buttplug.Client;
using Buttplug.Client.Connectors;

namespace IntifaceGameVibrationRouter
{

    public class CheckedListItem
    {
        public CheckedListItem(ButtplugClientDevice dev, bool isChecked = false)
        {
            Device = dev;
            IsChecked = isChecked;
        }

        public string Name => Device.Name;

        public uint Id => Device.Index;

        public ButtplugClientDevice Device { get; set; }

        public bool IsChecked { get; set; }
    }

    /// <summary>
    /// Interaction logic for IntifaceControl.xaml
    /// </summary>
    public partial class IntifaceControl : UserControl
    {
        public ObservableCollection<CheckedListItem> DevicesList { get; set; } = new ObservableCollection<CheckedListItem>();

        private ButtplugClient _client;
        private List<ButtplugClientDevice> _devices;
        private Task _connectTask;
        private bool _quitting;

        public EventHandler Connected;
        public EventHandler Disconnected;
        public bool IsConnected => _client.Connected;

        public IntifaceControl()
        {
            InitializeComponent();
            DeviceListBox.ItemsSource = DevicesList;
            _connectTask = new Task(async () => await ConnectTask());
            _connectTask.Start();
        }

        public async Task ConnectTask()
        {
            Dispatcher.Invoke(() => { ConnectionStatus.Content = "Connecting"; });
            //var insecureWebsocketConnector = new ButtplugWebsocketConnector();
            //var secureWebsocketConnector = new ButtplugWebsocketConnector();
            var ipcConnector = new ButtplugClientIPCConnector("ButtplugPort");
            //var insecureWebsocketClient = new ButtplugClient("GVR - Insecure Websocket", insecureWebsocketConnector);
            //var secureWebsocketClient = new ButtplugClient("GVR - Secure Websocket", secureWebsocketConnector);
            var ipcClient = new ButtplugClient("GVR - IPC", ipcConnector);
            while (!_quitting)
            {
                try
                {
                    ipcClient.DeviceAdded += OnDeviceAdded;
                    ipcClient.DeviceRemoved += OnDeviceRemoved;
                    ipcClient.Log += OnLogMessage;
                    ipcClient.ServerDisconnect += OnDisconnect;
                    await ipcClient.ConnectAsync();
                    _client = ipcClient;
                    Dispatcher.Invoke(() => { Connected?.Invoke(this, new EventArgs()); });
                    Dispatcher.Invoke(() => { ConnectionStatus.Content = "Connected"; });
                    await _client.StartScanningAsync();
                    break;
                }
                catch (ButtplugClientConnectorException)
                {
                    Debug.WriteLine("Retrying");
                    // Just keep trying to connect.
                }
                catch (Exception)
                {
                    Debug.WriteLine("Did something else fail?");
                }
            }
        }

        public async Task Disconnect()
        {
            _quitting = true;
            await _client.DisconnectAsync();
        }

        public async Task StartScanning()
        {
            await _client.StartScanningAsync();
        }

        public async Task StopScanning()
        {
            await _client.StopScanningAsync();
        }

        public void OnDisconnect(object aObj, EventArgs aArgs)
        {
            _connectTask = new Task(async () => await ConnectTask());
            _connectTask.Start();
        }

        public void OnDeviceAdded(object aObj, DeviceAddedEventArgs aArgs)
        {
            Dispatcher.Invoke(() => { DevicesList.Add(new CheckedListItem(aArgs.Device)); }); 
        }

        public void OnDeviceRemoved(object aObj, DeviceRemovedEventArgs aArgs)
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var dev in DevicesList)
                {
                    if (dev.Id != aArgs.Device.Index)
                    {
                        continue;
                    }
                    DevicesList.Remove(dev);
                    return;
                }
            });
        }

        public void OnLogMessage(object aObj, LogEventArgs aArgs)
        {

        }
    }
}
