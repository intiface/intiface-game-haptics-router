using System.Windows.Controls;
using System.IO;
using System.Linq;
using System.Reflection;

namespace IntifaceGameHapticsRouter
{
    /// <summary>
    /// Interaction logic for HelpControl.xaml
    /// </summary>
    public partial class HelpControl : UserControl
    {
        public HelpControl()
        {
            InitializeComponent();
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames()
                .Single(str => str.EndsWith("help.md"));
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                Markdownview.Markdown = reader.ReadToEnd();
            }

        }
    }
}
