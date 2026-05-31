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
        private XInputHaptics _lastXInput = new XInputHaptics(0, 0, 0);
        private bool _needXInputRecalc;
        private double _multiplier;
        private double _baseline;
        private int _fadeMs;
        private double _currentOutput;
        // Anchored at the most recent attack peak. Held constant across all fade ticks so the
        // release ramp stays linear over _fadeMs regardless of intermediate target oscillations.
        private double _fadeStartLevel;
        private Task _updateTask;
        private readonly DualSenseRumble _dualSense = new DualSenseRumble();
        // Read on the xinputTimer's ThreadPool thread, written on the WPF dispatcher thread.
        private volatile bool _directDualSenseRumbleEnabled;

        public MainWindow()
        {
            InitializeComponent();
            vrTimer.Elapsed += OnVRTimer;
            xinputTimer.Elapsed += OnXInputTimer;
            vrTimer.Interval = IntifaceGameHapticsRouterProperties.Default.PacketTimingGapInMS;
            xinputTimer.Interval = IntifaceGameHapticsRouterProperties.Default.PacketTimingGapInMS;
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
            _graphTab.PacketGapChanged += OnPacketTimingChanged;
            _graphTab.FadeMsChanged += OnFadeMsChanged;
            _graphTab.DirectDualSenseRumbleChanged += OnDirectDualSenseRumbleChanged;
            _multiplier = _graphTab.Multiplier;
            _baseline = _graphTab.Baseline;
            _fadeMs = _graphTab.FadeMs;
            _directDualSenseRumbleEnabled = _graphTab.DirectDualSenseRumble;
            if (_directDualSenseRumbleEnabled)
            {
                TryOpenDualSense();
            }
            else
            {
                _graphTab.SetDualSenseStatus("disabled", false);
            }
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
            if (_directDualSenseRumbleEnabled)
            {
                _dualSense.SetRumble(0, 0);
            }
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

        protected void OnPacketTimingChanged(object o, int a)
        {
            vrTimer.Interval = IntifaceGameHapticsRouterProperties.Default.PacketTimingGapInMS;
            xinputTimer.Interval = IntifaceGameHapticsRouterProperties.Default.PacketTimingGapInMS;
        }

        protected void OnFadeMsChanged(object o, int aValue)
        {
            _fadeMs = aValue;
        }

        protected void OnDirectDualSenseRumbleChanged(object o, bool enabled)
        {
            _directDualSenseRumbleEnabled = enabled;
            if (enabled)
            {
                TryOpenDualSense();
            }
            else
            {
                _dualSense.SetRumble(0, 0);
                _dualSense.Close();
                _graphTab.SetDualSenseStatus("disabled", false);
            }
        }

        private void TryOpenDualSense()
        {
            if (_dualSense.TryOpen())
            {
                _graphTab.SetDualSenseStatus("connected (USB)", true);
            }
            else
            {
                _graphTab.SetDualSenseStatus("not detected (USB-only prototype)", false);
            }
        }

        protected void OnLogMessage(object aObj, string aMsg)
        {
            _log.Info(aMsg);
        }

        protected async void OnVRTimer(object aObj, ElapsedEventArgs aArgs)
        {
            vrTimer.Stop();
            await Dispatcher.Invoke(async () => { await _intifaceTab.Vibrate(0xF, 0); });
        }

        protected async void OnXInputTimer(object aObj, ElapsedEventArgs aArgs)
        {
            if (!_needXInputRecalc)
            {
                return;
            }

            var averageVibeSpeed = (_lastXInput.LeftMotor + _lastXInput.RightMotor) / (2.0 * 65535.0);

            // Target is the steady-state value the game/baseline is asking for.
            // Floor at baseline so a fade naturally lands on the baseline level.
            var target = Math.Min(Math.Max(averageVibeSpeed * _multiplier, _baseline), 1.0);

            if (target >= _currentOutput)
            {
                // Snap up on attack — fade only applies on release.
                _currentOutput = target;
                _fadeStartLevel = target;
            }
            else if (_fadeMs <= 0)
            {
                _currentOutput = target;
                _fadeStartLevel = target;
            }
            else
            {
                // Linear release from _fadeStartLevel to target over _fadeMs.
                var tickMs = Math.Max(1, IntifaceGameHapticsRouterProperties.Default.PacketTimingGapInMS);
                var span = _fadeStartLevel - target;
                if (span > 0)
                {
                    var step = span * ((double)tickMs / _fadeMs);
                    _currentOutput = Math.Max(target, _currentOutput - step);
                }
                else
                {
                    _currentOutput = target;
                }
            }

            var motorVal = (uint)(_currentOutput * 65535.0);
            _graphTab.UpdateVibrationValues(motorVal, motorVal);

            if (_directDualSenseRumbleEnabled && _dualSense.IsOpen)
            {
                var motor255 = (byte)Math.Min(255, (int)(_currentOutput * 255.0));
                if (!_dualSense.SetRumble(motor255, motor255))
                {
                    _graphTab.SetDualSenseStatus("disconnected", false);
                }
            }

            // Keep ticking while we're still descending toward target.
            var stillFading = _currentOutput > target + 1e-6;
            _needXInputRecalc = stillFading;

            // Stop the timer only once we've fully settled with no input and no baseline.
            if (!stillFading
                && _lastXInput.LeftMotor == 0
                && _lastXInput.RightMotor == 0
                && _baseline == 0
                && _currentOutput <= 0.0)
            {
                xinputTimer.Stop();
            }

            await Dispatcher.Invoke(async () => { await _intifaceTab.Vibrate((uint)_lastXInput.ControllerIndex, _currentOutput); });
        }

        protected void OnGVRMessageReceived(object aObj, GHRProtocolMessageContainer aMsg)
        {
            if (aMsg.XInputHaptics != null)
            {
                Console.WriteLine($"{aMsg.XInputHaptics.ControllerIndex} {aMsg.XInputHaptics.LeftMotor} {aMsg.XInputHaptics.RightMotor}");
                _lastXInput = aMsg.XInputHaptics;
                xinputTimer.Start();
                _needXInputRecalc = true;
            }
            else if (aMsg.Log != null)
            {
                _log.Info(aMsg.Log.Message);
                Debug.WriteLine(aMsg.Log.Message);
            }
        }

    }
}
