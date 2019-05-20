using System;
using System.Windows;
using Buttplug.Logging;
using JetBrains.Annotations;

namespace IntifaceGameVibrationRouter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [NotNull]
        private readonly Logger _log;

        private double _vibrationMultiplier = 1;
        private double _vibrationBaseline = 0;

        public MainWindow()
        {
            InitializeComponent();
            if (Application.Current == null)
            {
                return;
            }

            _intifaceTab.LogMessageHandler += OnLogMessage;
            _modTab.GvrProtocolMessageHandler += OnGVRMessageReceived;
            //_graphTab.PassthruChanged += PassthruChanged;
        }

        protected void OnLogMessage(object aObj, string aMsg)
        {
            _logTab.AddLogMessage(aMsg);
        }

        protected async void OnGVRMessageReceived(object aObj, GVRProtocolMessage aMsg)
        {
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
            
        }
    }
}
