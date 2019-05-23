using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
