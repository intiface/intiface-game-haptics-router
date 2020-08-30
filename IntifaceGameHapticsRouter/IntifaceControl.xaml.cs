using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using Buttplug.Client;
using Buttplug.Core.Logging;
using Buttplug.Core.Messages;
using Buttplug.Server;
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
        private DeviceManager _deviceManager;
        private List<ButtplugClientDevice> _devices = new List<ButtplugClientDevice>();
        private Task _connectTask;
        private bool _quitting;
        private bool _useEmbeddedServer = true;
        private Logger _log;

        public EventHandler ConnectedHandler;
        public EventHandler DisconnectedHandler;
        public EventHandler<string> LogMessageHandler;
        public bool IsConnected => _client.Connected;

        private XInputHaptics _lastVibration = new XInputHaptics();
        private XInputHaptics _lastSentVibration = new XInputHaptics();
        private bool _speedNeedsRecalc = false;
        private Timer commandTimer;

        public IntifaceControl()
        {
            InitializeComponent();
            _log = LogManager.GetCurrentClassLogger();
            DeviceListBox.ItemsSource = DevicesList;
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls | SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12;
            _connectTask = new Task(async () => await ConnectTask());
            _connectTask.Start();
            commandTimer = new Timer {Interval = 50, AutoReset = true};
            //commandTimer.Elapsed += OnVibrationTimer;
        }

        public async Task ConnectTask()
        {
            Dispatcher.Invoke(() => { ConnectionStatus.Content = "Connecting"; });
            IButtplugClientConnector connector;
            //if (_useEmbeddedServer)
            {
                var embeddedConnector = new ButtplugEmbeddedConnector("GVR Embedded Server", 0, _deviceManager);
                if (_deviceManager == null)
                {
                    _deviceManager = embeddedConnector.Server.DeviceManager;
                }
                connector = embeddedConnector;
            }
            //else
            {
                //connector = new ButtplugWebsocketConnector(new Uri("ws://localhost:12345/buttplug"));
                //var secureWebsocketConnector = new ButtplugWebsocketConnector();
                //var ipcConnector = new ButtplugClientIPCConnector("ButtplugPort");
                //var insecureWebsocketClient = new ButtplugClient("GVR - Insecure Websocket", insecureWebsocketConnector);
                //var secureWebsocketClient = new ButtplugClient("GVR - Secure Websocket", secureWebsocketConnector);
            }

            var client = new ButtplugClient("GVR - IPC", connector);
            while (!_quitting)
            {
                try
                {
                    client.DeviceAdded += OnDeviceAdded;
                    client.DeviceRemoved += OnDeviceRemoved;
                    client.Log += OnLogMessage;
                    client.ServerDisconnect += OnDisconnect;
                    await client.ConnectAsync();
                    await client.RequestLogAsync(ButtplugLogLevel.Debug);
                    _client = client;
                    await Dispatcher.Invoke(async () =>
                    {
                        ConnectedHandler?.Invoke(this, new EventArgs());
                        ConnectionStatus.Content = "Connected to Intiface (Embedded)";
                        await StartScanning();
                        _scanningButton.Visibility = Visibility.Visible;
                    });
                    break;
                }
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
            _scanningButton.Content = "Stop Scanning";
        }

        public async Task StopScanning()
        {
            await _client.StopScanningAsync();
            _scanningButton.Content = "Start Scanning";
        }

        public async void OnScanningClick(object aObj, EventArgs aArgs)
        {
            _scanningButton.IsEnabled = false;
            // Dear god this is so jank. How is IsScanning not exposed on the Buttplug Client?
            if (_scanningButton.Content.ToString().Contains("Stop"))
            {
                await StopScanning();
            }
            else
            {
                await StartScanning();
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

        public void OnDeviceAdded(object aObj, Buttplug.Client.DeviceAddedEventArgs aArgs)
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
            try
            {
                Dispatcher.Invoke(() =>
                {
                    LogMessageHandler?.Invoke(this, aArgs.Message.LogMessage);
                });
            }
            catch (TaskCanceledException)
            {
                // noop, we're shutting down.
            }
        }

        public async Task Vibrate(double aSpeed)
        {
            foreach (var deviceItem in DevicesList)
            {
                if (deviceItem.IsChecked && deviceItem.Device.AllowedMessages.ContainsKey(typeof(VibrateCmd)))
                {
                    await deviceItem.Device.SendVibrateCmd(aSpeed);
                }
                if (deviceItem.IsChecked && deviceItem.Device.AllowedMessages.ContainsKey(typeof(RotateCmd)))
                {
                    await deviceItem.Device.SendRotateCmd(aSpeed, true);
                }
            }
        }

        public async Task StopVibration()
        {
            await Vibrate(0);
        }

        /*
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
        */
    }
}
