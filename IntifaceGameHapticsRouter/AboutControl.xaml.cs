using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace IntifaceGameHapticsRouter
{
    /// <summary>
    /// Interaction logic for AboutControl.xaml
    /// </summary>
    public partial class AboutControl : UserControl
    {
        public AboutControl()
        {
            InitializeComponent();

            _buildDate.Content = Properties.Resources.BuildDate;
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
    }
}
