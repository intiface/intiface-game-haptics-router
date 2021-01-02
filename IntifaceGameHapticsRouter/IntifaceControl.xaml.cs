using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using Buttplug;
using NLog;

namespace IntifaceGameHapticsRouter
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
        private List<ButtplugClientDevice> _devices = new List<ButtplugClientDevice>();
        private Task _connectTask;
        private bool _quitting;
        private bool _useEmbeddedServer = true;
        private Logger _log;

        public EventHandler ConnectedHandler;
        public EventHandler DisconnectedHandler;
        public EventHandler<string> LogMessageHandler;
        //        public bool IsConnected => _client.Connected;

        public IntifaceControl()
        {
            InitializeComponent();
            _log = LogManager.GetCurrentClassLogger();
            DeviceListBox.ItemsSource = DevicesList;
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls | SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12;
            _connectTask = new Task(async () => await ConnectTask());
            _connectTask.Start();
        }

        public async Task ConnectTask()
        {
            Dispatcher.Invoke(() => { ConnectionStatus.Content = "Connecting"; });
            //if (_useEmbeddedServer)
            {
            }
            //else
            {
                //connector = new ButtplugWebsocketConnector(new Uri("ws://localhost:12345/buttplug"));
                //var secureWebsocketConnector = new ButtplugWebsocketConnector();
                //var ipcConnector = new ButtplugClientIPCConnector("ButtplugPort");
                //var insecureWebsocketClient = new ButtplugClient("GVR - Insecure Websocket", insecureWebsocketConnector);
                //var secureWebsocketClient = new ButtplugClient("GVR - Secure Websocket", secureWebsocketConnector);
            }

            var client = new ButtplugClient("GVR - IPC");
            while (!_quitting)
            {
                //try
                {
                    var connector = new ButtplugEmbeddedConnectorOptions();
                    //var connector = new ButtplugWebsocketConnectorOptions(new Uri("ws://localhost:12345"));
                    client.DeviceAdded += OnDeviceAdded;
                    //                    client.DeviceRemoved += OnDeviceRemoved;
                    //client.Log += OnLogMessage;
                    client.ServerDisconnect += OnDisconnect;
                    await client.ConnectAsync(connector);
                    //await client.RequestLogAsync(ButtplugLogLevel.Debug);
                    _client = client;
                    client.ScanningFinished += OnScanningFinished;
                    await Dispatcher.Invoke(async () =>
                    {
                        ConnectedHandler?.Invoke(this, new EventArgs());
                        ConnectionStatus.Content = "Connected to Intiface (Embedded)";
                        // await StartScanning();
                        _scanningButton.Visibility = Visibility.Visible;
                    });
                    break;
                }
                /*
                catch (ButtplugClientConnectorException)
                {
                    Debug.WriteLine("Retrying");
                    // Just keep trying to connect.
                    // If the exception was thrown after connect, make sure we disconnect.
                    if (_client != null && _client.Connected)
                    {
                        await _client.DisconnectAsync();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Did something else fail? {ex})");
                    // If the exception was thrown after connect, make sure we disconnect.
                    if (_client != null && _client.Connected)
                    {
                        await _client.DisconnectAsync();
                    }
                }
                */
            }
        }

        public void OnScanningFinished(object o, EventArgs e)
        {
            Dispatcher.Invoke(new Action(() => _scanningButton.Content = "Start Scanning"));
        }

        public async Task Disconnect()
        {
            _quitting = true;
            //await _client.DisconnectAsync();
        }

        public Task StartScanning()
        {
            return _client.StartScanningAsync();
        }

        public Task StopScanning()
        {
            return _client.StopScanningAsync();
        }

        public void OnScanningClick(object aObj, EventArgs aArgs)
        {
            _scanningButton.IsEnabled = false;
            // Dear god this is so jank. How is IsScanning not exposed on the Buttplug Client?
            if (_scanningButton.Content.ToString().Contains("Stop"))
            {
                try
                {
                    _scanningButton.Content = "Start Scanning";
                    Task.Run(async () => await StopScanning());
                }
                catch (ButtplugException e)
                {
                    // This will happen if scanning has already stopped. For now, ignore it.
                }
            }
            else
            {
                _scanningButton.Content = "Stop Scanning";
                Task.Run(async () => await StartScanning());
            }
            _scanningButton.IsEnabled = true;
        }

        public void OnDisconnect(object aObj, EventArgs aArgs)
        {
            _connectTask = new Task(async () => await ConnectTask());
            _connectTask.Start();
            _devices.Clear();
            _client = null;
        }

        public void OnDeviceAdded(object aObj, DeviceAddedEventArgs aArgs)
        {
            Dispatcher.Invoke(() => {
                try
                {
                    DevicesList.Add(new CheckedListItem(aArgs.Device));
                }
                catch (Exception ex)
                {
                    // Ignore already added devices
                }
            });
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
 
        public async Task Vibrate(double aSpeed)
        {
            foreach (var deviceItem in DevicesList)
            {
                if (deviceItem.IsChecked && deviceItem.Device.AllowedMessages.ContainsKey(ServerMessage.Types.MessageAttributeType.VibrateCmd))
                {
                    await deviceItem.Device.SendVibrateCmd(aSpeed);
                }
                if (deviceItem.IsChecked && deviceItem.Device.AllowedMessages.ContainsKey(ServerMessage.Types.MessageAttributeType.RotateCmd))
                {
                    await deviceItem.Device.SendRotateCmd(aSpeed, true);
                }
            }
        }

        public async Task StopVibration()
        {
            await Vibrate(0);
        }
    }
}
