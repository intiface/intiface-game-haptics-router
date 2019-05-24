using System;
using System.Diagnostics;
using System.Timers;
using System.Windows;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace IntifaceGameHapticsRouter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly NLog.Logger _log;
        private Timer vrTimer = new Timer();

        public MainWindow()
        {
            InitializeComponent();
            vrTimer.Interval = 75;
            vrTimer.Elapsed += OnTimer;
            if (Application.Current == null)
            {
                return;
            }
            _log = LogManager.GetCurrentClassLogger();
            LogManager.Configuration = LogManager.Configuration ?? new LoggingConfiguration();
#if DEBUG
            // Debug Logger Setup
            var t = new DebuggerTarget();
            LogManager.Configuration.AddTarget("debugger", t);
            LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, t));
            LogManager.Configuration = LogManager.Configuration;
#endif

            _intifaceTab.LogMessageHandler += OnLogMessage;
            _modTab.MessageReceivedHandler += OnGVRMessageReceived;
            //_graphTab.PassthruChanged += PassthruChanged;
            _log.Info("Application started.");
        }

        protected void OnLogMessage(object aObj, string aMsg)
        {
            _log.Info(aMsg);
        }

        protected async void OnTimer(object aObj, ElapsedEventArgs aArgs)
        {
            vrTimer.Stop();
            await Dispatcher.Invoke(async () => { await _intifaceTab.Vibrate(0); });
        }
        
        protected async void OnGVRMessageReceived(object aObj, GHRProtocolMessageContainer aMsg)
        {
            // For now, treat Vive and Oculus clips the same. Assume that if we
            // get anything at all, we should be vibrating, and if our timer
            // runs out, we should stop. There's no real need to parse the
            // buffers yet, as there's no way our older motors can spin up/down
            // at the speed of HD rumble. Once we get Nintendo Joycon support,
            // this may change.
            if (aMsg.UnityXRViveHaptics != null || aMsg.UnityXROculusClipHaptics != null || aMsg.UnityXROculusInputHaptics != null)
            {
                var isEnabled = vrTimer.Enabled;
                vrTimer.Stop();
                vrTimer.Start();
                if (!isEnabled)
                {
                    await Dispatcher.Invoke(async () => { await _intifaceTab.Vibrate(1); });
                }
            }
            else if (aMsg.Log != null)
            {
                _log.Info(aMsg.Log.Message);
                Debug.WriteLine(aMsg.Log.Message);
            }
            /*
            switch (aMsg)
            {
                case Log l:
                    _logTab.AddLogMessage(l.Message);
                    break;
                case Ping p:
                    break;
                case XInputHaptics x:
                    break;
                case UnityXRViveHaptics x:
                    break;
                case UnityXROculusClipHaptics x:
                    break;
                case UnityXROculusInputHaptics x:
                    break;
            }
            */
        }
    }
}
