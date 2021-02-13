using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using NLog;
using Octokit;

namespace IntifaceGameHapticsRouter
{
    /// <summary>
    /// Interaction logic for AboutControl.xaml
    /// </summary>
    public partial class AboutControl : UserControl
    {
        private string _currentVersion = "v14";

        public AboutControl()
        {
            InitializeComponent();
        }

        public async Task CheckForUpdate()
        {
            try
            {
                var client = new Octokit.GitHubClient(new ProductHeaderValue($"IntifaceGameHapticsRouter{_currentVersion}"));
                var release = await client.Repository.Release.GetLatest("intiface", "intiface-game-haptics-router");
                if (release.TagName != _currentVersion)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (MessageBox.Show("A new GHR update is available! Would you like to go to the update site?",
                                "GHR Update",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Asterisk) == MessageBoxResult.Yes)
                        {
                            var link = new Hyperlink(
                                new Run("https://github.com/intiface/intiface-game-haptics-router/releases"))
                            {
                                NavigateUri =
                                    new Uri("https://github.com/intiface/intiface-game-haptics-router/releases")
                            };
                            link.RequestNavigate += Hyperlink_RequestNavigate;
                            link.DoClick();
                        }
                    });
                }
                UpdateCheckStatus.Text = $"Last Check at {DateTime.Now.ToString()}";
            }
            catch (Exception ex)
            {
                var log = LogManager.GetCurrentClassLogger();
                log.Error($"Cannot run update check: {ex.Message}");
            }
        }

        private void TryUri(string aUri)
        {
            try
            {
                System.Diagnostics.Process.Start(aUri);
            }
            catch (Win32Exception)
            {
                MessageBox.Show($"No browser available to open link! Go to {aUri}.", "Browser open error", MessageBoxButton.OK);
            }
        }

        private void Hyperlink_RequestNavigate(object aSender, System.Windows.Navigation.RequestNavigateEventArgs aEvent)
        {
            TryUri(aEvent.Uri.AbsoluteUri);
        }

        private async void CheckForUpdates_OnClick(object sender, RoutedEventArgs e)
        {
            await CheckForUpdate();
        }
    }
}
