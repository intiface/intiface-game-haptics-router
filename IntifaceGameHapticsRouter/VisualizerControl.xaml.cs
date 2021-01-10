using System;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace IntifaceGameHapticsRouter
{
    /// <summary>
    ///     Interaction logic for VisualizerControl.xaml
    /// </summary>
    public partial class VisualizerControl : UserControl
    {
        private readonly ChartValues<double> HighPowerValues;
        private readonly ChartValues<double> LowPowerValues;
        private uint CurrentLeftMotorSpeed;
        private uint CurrentRightMotorSpeed;
        private Timer runTimer;

        // Both of these members need to be public, otherwise
        // livecharts can't see them to chart them.
        // ReSharper disable once MemberCanBePrivate.Global
        public SeriesCollection LowPowerSeriesCollection { get; set; }

        // ReSharper disable once MemberCanBePrivate.Global
        public SeriesCollection HighPowerSeriesCollection { get; set; }
        public event EventHandler<double> MultiplierChanged;
        public event EventHandler<double> BaselineChanged;
        public event EventHandler<int> PacketGapChanged;
        public event EventHandler<bool> PassthruChanged;

        public double Multiplier => multiplierSlider.Value;
        public double Baseline => baselineSlider.Value;

        public VisualizerControl()
        {
            InitializeComponent();

            packetGapSlider.Value = IntifaceGameHapticsRouterProperties.Default.PacketTimingGapInMS;
            packetGapSlider.ValueChanged += packetGapSlider_ValueChanged;

            multiplierSlider.Value = IntifaceGameHapticsRouterProperties.Default.VibrationMultiplier;
            multiplierSlider.ValueChanged += (object o, RoutedPropertyChangedEventArgs<double> e) =>
            {
                IntifaceGameHapticsRouterProperties.Default.VibrationMultiplier = Multiplier;
                IntifaceGameHapticsRouterProperties.Default.Save();
            };
            LowPowerValues = new ChartValues<double>();
            HighPowerValues = new ChartValues<double>();
            for (var i = 0; i < 200; ++i)
            {
                LowPowerValues.Add(0);
                HighPowerValues.Add(0);
            }

            LowPowerSeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Low Power Vibrator",
                    Values = LowPowerValues,
                    PointGeometrySize = 0
                }
            };
            HighPowerSeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "High Power Vibrator",
                    Values = HighPowerValues,
                    PointGeometrySize = 0
                }
            };
            DataContext = this;
            // The automated updater doesn't seem to run correctly after it loses focus, so we'll 
            // manually run update steps ourselves.
            Dispatcher.Invoke(() =>
            {
                HighPowerChart.UpdaterState = UpdaterState.Paused;
                LowPowerChart.UpdaterState = UpdaterState.Paused;
            });
            runTimer = new Timer { Interval = 100, AutoReset = true };
            runTimer.Elapsed += AddPoint;
        }

        public void StartUpdates()
        {
            runTimer.Start();
        }

        public void StopUpdates()
        {
            runTimer.Stop();
        }

        public void AddPoint(object o, ElapsedEventArgs e)
        {
            AddVibrationValue(CurrentLeftMotorSpeed, CurrentRightMotorSpeed);
        }

        public void UpdateVibrationValues(uint aLeftMotor, uint aRightMotor)
        {
            CurrentLeftMotorSpeed = aLeftMotor;
            CurrentRightMotorSpeed = aRightMotor;
        }

        public void AddVibrationValue(double aLowPower, double aHighPower)
        {
            LowPowerValues.RemoveAt(0);
            LowPowerValues.Add(aLowPower);
            HighPowerValues.RemoveAt(0);
            HighPowerValues.Add(aHighPower);
            // Manually run chart update
            try
            {
                Dispatcher.Invoke(() =>
                {
                    LowPowerChart.Update(false, true);
                    HighPowerChart.Update(false, true);
                });
            }
            catch (TaskCanceledException)
            {
                // Usually means we're shutting down. noop.
            }
        }

        private void PassthruCheckBox_Changed(object aSender, RoutedEventArgs aArgs)
        {
            PassthruChanged?.Invoke(this, PassthruCheckBox.IsChecked.Value);
        }

        private void BaselineSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            BaselineChanged?.Invoke(this, Baseline);
        }


        private void MultiplierSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MultiplierChanged?.Invoke(this, Multiplier);
        }

        private void packetGapSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            IntifaceGameHapticsRouterProperties.Default.PacketTimingGapInMS = (int)packetGapSlider.Value;
            IntifaceGameHapticsRouterProperties.Default.Save();
            PacketGapChanged?.Invoke(this, IntifaceGameHapticsRouterProperties.Default.PacketTimingGapInMS);
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            IntifaceGameHapticsRouterProperties.Default.Reset();
            IntifaceGameHapticsRouterProperties.Default.Save();
            MultiplierChanged?.Invoke(this, IntifaceGameHapticsRouterProperties.Default.VibrationMultiplier);
            PacketGapChanged?.Invoke(this, IntifaceGameHapticsRouterProperties.Default.PacketTimingGapInMS);
        }
    }
}