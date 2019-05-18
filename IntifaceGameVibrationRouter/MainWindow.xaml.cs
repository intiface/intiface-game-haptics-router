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

            //_graphTab.PassthruChanged += PassthruChanged;
        }

        protected async void OnGVRMessageReceived(object aObj, EventHandler<GVRProtocolMessage> aMsg)
        {

        }
    }
}
