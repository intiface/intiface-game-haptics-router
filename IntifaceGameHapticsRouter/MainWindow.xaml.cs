using System;
using System.Diagnostics;
using System.Threading.Tasks;
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
        private Timer xinputTimer = new Timer();
        private XInputHaptics _lastXInput = new XInputHaptics(0, 0);
        private bool _needXInputRecalc;
        private double _multiplier;
        private double _baseline;
        private Task _updateTask;

        public MainWindow()
        {
            InitializeComponent();
            vrTimer.Interval = 75;
            vrTimer.Elapsed += OnVRTimer;
            xinputTimer.Interval = 50;
            xinputTimer.Elapsed += OnXInputTimer;
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
            _modTab.ProcessAttached += OnProcessAttached;
            _modTab.ProcessDetached += OnProcessDetached;
            _graphTab.MultiplierChanged += OnMultiplierChanged;
            _graphTab.BaselineChanged += OnBaselineChanged;
            _multiplier = _graphTab.Multiplier;
            _baseline = _graphTab.Baseline;
            //_graphTab.PassthruChanged += PassthruChanged;
            _log.Info("Application started.");
            _updateTask = _aboutTab.CheckForUpdate();
        }

        protected void OnProcessAttached(object aObj, EventArgs aNull)
        {
            _graphTab.StartUpdates();
        }

        protected void OnProcessDetached(object aObj, EventArgs aNull)
        {
            _graphTab.StopUpdates();
        }

        protected void OnMultiplierChanged(object aObj, double aValue)
        {
            _needXInputRecalc = true;
            _multiplier = aValue;
        }

        protected void OnBaselineChanged(object aObj, double aValue)
        {
            _needXInputRecalc = true;
            _baseline = aValue;
            if (!xinputTimer.Enabled)
            {
                xinputTimer.Start();
            }
        }

        protected void OnLogMessage(object aObj, string aMsg)
        {
            _log.Info(aMsg);
        }

        protected async void OnVRTimer(object aObj, ElapsedEventArgs aArgs)
        {
            vrTimer.Stop();
            await Dispatcher.Invoke(async () => { await _intifaceTab.Vibrate(0); });
        }

        protected async void OnXInputTimer(object aObj, ElapsedEventArgs aArgs)
        {
            if (!_needXInputRecalc)
            {
                return;
            }
            
            // If we've received an off packet, just assume we won't be updating again until we get something new.
            if (_lastXInput.LeftMotor == 0 && _lastXInput.RightMotor == 0 && _baseline == 0)
            {
                xinputTimer.Stop();
            }

            _graphTab.UpdateVibrationValues(
                Math.Max((uint)(_lastXInput.LeftMotor * _multiplier), (uint)(_baseline * 65535.0)),
                Math.Max((uint)(_lastXInput.RightMotor * _multiplier), (uint)(_baseline * 65535.0)));

            var averageVibeSpeed = (_lastXInput.LeftMotor + _lastXInput.RightMotor) / (2.0 * 65535.0);

            // Calculate the vibe speed by first adding the multiplier to the averaged speed 
            // Then check if it's above the baseline, if not default to the baseline
            // If it is then make sure we don't go above 1.0 speed or things start breaking
            var vibeSpeed = Math.Min(Math.Max(averageVibeSpeed * _multiplier, _baseline), 1.0);
            Debug.WriteLine($"Updating XInput haptics to {vibeSpeed}");
            _needXInputRecalc = false;
            await Dispatcher.Invoke(async () => { await _intifaceTab.Vibrate(vibeSpeed); });
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
            else if (aMsg.XInputHaptics != null)
            {
                _lastXInput = aMsg.XInputHaptics;
                xinputTimer.Start();
                _needXInputRecalc = true;
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
