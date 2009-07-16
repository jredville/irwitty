using System.Deployment.Application;
using System.Windows.Controls;

namespace Witty.Controls.Options
{
    /// <summary>
    /// Interaction logic for AboutOptions.xaml
    /// </summary>
    public partial class AboutOptions : UserControl
    {
        public AboutOptions()
        {
            InitializeComponent();
            string version = string.Empty;
            if (ApplicationDeployment.IsNetworkDeployed)
                version = string.Format("{0} (ClickOnce)", ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString());
            else
                version = App.ResourceAssembly.GetName().Version.ToString();

            Version.Inlines.Clear();
            Version.Inlines.Add(string.Format("Version {0}", version));
        }
    }
}
