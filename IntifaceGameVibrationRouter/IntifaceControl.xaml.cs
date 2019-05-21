using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Controls;
using Buttplug.Client;
using Buttplug.Client.Connectors;
using Buttplug.Core.Logging;
using Buttplug.Core.Messages;
using GVRXInputModInterface;
using NLog;

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
        private Logger _log;

        public EventHandler ConnectedHandler;
        public EventHandler DisconnectedHandler;
        public EventHandler<string> LogMessageHandler;
        public bool IsConnected => _client.Connected;

        private Vibration _lastVibration = new Vibration();
        private Vibration _lastSentVibration = new Vibration();
        private bool _speedNeedsRecalc = false;
        private Timer commandTimer;

        public IntifaceControl()
        {
            InitializeComponent();
            _log = LogManager.GetCurrentClassLogger();
            DeviceListBox.ItemsSource = DevicesList;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12;
            _connectTask = new Task(async () => await ConnectTask());
            _connectTask.Start();
            commandTimer = new Timer { Interval = 50, AutoReset = true };
            commandTimer.Elapsed += OnVibrationTimer;
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
                    await ipcClient.RequestLogAsync(ButtplugLogLevel.Debug);
                    _client = ipcClient;
                    await Dispatcher.Invoke(async () =>
                    {
                        ConnectedHandler?.Invoke(this, new EventArgs());
                        ConnectionStatus.Content = "Connected";
                        await StartScanning();
                    });
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
            _devices.Clear();
            _client = null;
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
            Dispatcher.Invoke(() => { LogMessageHandler?.Invoke(this, aArgs.Message.LogMessage); });
        }

        private async void OnVibrationTimer(object aObj, ElapsedEventArgs e)
        {
            if (_lastVibration == _lastSentVibration && !_speedNeedsRecalc)
            {
                return;
            }

            await Dispatcher.Invoke(async () =>
            {
                foreach (var device in _devices)
                {
                    if (device.AllowedMessages.ContainsKey(typeof(VibrateCmd)))
                    {
                        try
                        {
                            var attrs = device.AllowedMessages[typeof(VibrateCmd)];
                            var vibeCount = attrs.FeatureCount ?? 0;
                            List<VibrateCmd.VibrateSubcommand> vibratorSettings = new List<VibrateCmd.VibrateSubcommand>();

                            var averageVibeSpeed = (_lastVibration.LeftMotorSpeed + _lastVibration.RightMotorSpeed) / (2.0 * 65535.0);

                            // Calculate the vibe speed by first adding the multiplier to the averaged speed 
                            // Then check if it's above the baseline, if not default to the baseline
                            // If it is then make sure we don't go above 1.0 speed or things start breaking
                            //var vibeSpeed = Math.Min(Math.Max(averageVibeSpeed * _vibrationMultiplier, _vibrationBaseline), 1.0);
                            var vibeSpeed = 0;
                            for (var i = 0; i < vibeCount; i++)
                            {
                                vibratorSettings.Add(new VibrateCmd.VibrateSubcommand((uint)i, vibeSpeed));
                            }

                            //await _bpServer.SendMessage(new VibrateCmd(device.Index, vibratorSettings));
                        }
                        catch (Exception ex)
                        {
                            //_log.Error(ex);
                        }
                    }
                }
            });

            _speedNeedsRecalc = false;
            _lastSentVibration = _lastVibration;
        }
    }
}
