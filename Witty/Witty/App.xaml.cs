using System.Net;
using System.Windows;
using System.Windows.Threading;
using log4net;
using log4net.Config;
using TwitterLib;
using System.Configuration;

namespace Witty
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>

    public partial class App : System.Windows.Application
    {
        public static readonly ILog Logger = LogManager.GetLogger("Witty.Logging");

        // Global variable for the user
        public static User LoggedInUser = null;
        public static UserCollection friends = new UserCollection();

        protected override void OnStartup(StartupEventArgs e)
        {
            DOMConfigurator.Configure();

            Logger.Info("Witty is starting.");

            CheckForCorruptedConfigFile();

            Properties.Settings appSettings = Witty.Properties.Settings.Default;
            if (appSettings.UpgradeSettings)
            {
                Witty.Properties.Settings.Default.Upgrade();
                appSettings.UpgradeSettings = false;
            }

            // Set the default proxy here once and for all (should then be used by
            // WPF controls, like images that fetch their source from the internet)
            if (appSettings.UseProxy)
            {
                HttpWebRequest.DefaultWebProxy = WebProxyHelper.GetConfiguredWebProxy();
            }


            if (!string.IsNullOrEmpty(appSettings.Skin))
            {
                try
                {
                    SkinsManager.ChangeSkin(appSettings.Skin);
                }
                catch
                {
                    Logger.Error("Selected skin " + appSettings.Skin + " + not found");
                    // REVIEW: Should witty do something smart here?
                }
            }

            base.OnStartup(e);
        }

        /// <summary>
        /// user.config can become corrupted due to crash, power loss, etc.
        /// This checks user.config and deletes it if it's corrupted.
        /// http://www.codeproject.com/KB/dotnet/CorruptUserConfig.aspx
        /// </summary>
        private static void CheckForCorruptedConfigFile()
        {
            try
            {
                Witty.Properties.Settings.Default.Reload();
            }
            catch (ConfigurationErrorsException ex)
            { //(requires System.Configuration)
                string filename = ((ConfigurationErrorsException)ex.InnerException).Filename;

                if (MessageBox.Show("Witty has detected that your" +
                                      " user settings file has become corrupted. " +
                                      "This may be due to a crash or improper exiting" +
                                      " of the program. Witty must reset your " +
                                      "user settings in order to continue.\n\nClick" +
                                      " Yes to reset your user settings and continue.\n\n" +
                                      "Click No if you wish to attempt manual repair" +
                                      " or to rescue information before proceeding.",
                                      "Corrupt user settings",
                                      MessageBoxButton.YesNo,
                                      MessageBoxImage.Error) == MessageBoxResult.Yes)
                {
                    System.IO.File.Delete(filename);
                    Witty.Properties.Settings.Default.Reload();
                    // you could optionally restart the app instead
                }
                else
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                // avoid the inevitable crash
            }
        }

        /// <summary>
        /// DispatcherUnhandledException is used to catch all unhandled exceptions.
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // log error for debugging.
            App.Logger.Error("Unhandled Exception", e.Exception);

            //REMARK: Should we handle the exception and do something more user-friendly here?
            MessageBox.Show("Witty has encountered an unexpected error. Please restart Witty");
        }
    }
}
