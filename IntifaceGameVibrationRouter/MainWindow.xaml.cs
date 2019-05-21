using System.Windows;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace IntifaceGameVibrationRouter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly NLog.Logger _log;

        public MainWindow()
        {
            InitializeComponent();
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
            _modTab.GvrProtocolMessageHandler += OnGVRMessageReceived;
            //_graphTab.PassthruChanged += PassthruChanged;
            _log.Info("Application started.");
        }

        protected void OnLogMessage(object aObj, string aMsg)
        {
            _logTab.AddLogMessage(aMsg);
        }
        
        protected async void OnGVRMessageReceived(object aObj, GVRProtocolMessageContainer aMsg)
        {
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
