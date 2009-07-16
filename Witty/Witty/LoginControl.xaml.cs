using System.Net;
using System.Windows;
using log4net;
using TwitterLib;

namespace Witty
{
    public partial class LoginControl
    {
        private static readonly ILog logger = LogManager.GetLogger("Witty.Logging");

        private readonly Properties.Settings AppSettings = Properties.Settings.Default;

        private delegate void LoginDelegate(TwitterNet arg);
        private delegate void PostLoginDelegate(User arg);

        public LoginControl()
        {
            this.InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Jason Follas: Reworked Web Proxy - don't need to explicitly pass into TwitterNet ctor
            //TwitterNet twitter = new TwitterNet(UsernameTextBox.Text, TwitterNet.ToSecureString(PasswordTextBox.Password), WebProxyHelper.GetConfiguredWebProxy());
            TwitterNet twitter = new TwitterNet(UsernameTextBox.Text, TwitterNet.ToSecureString(PasswordTextBox.Password)); //, WebProxyHelper.GetConfiguredWebProxy());

            // Jason Follas: Twitter proxy servers, anyone?  (Network Nazis who block twitter.com annoy me)
            twitter.TwitterServerUrl = AppSettings.TwitterHost;

            // Attempt login in a new thread
            LoginButton.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                new LoginDelegate(TryLogin), twitter);
        }

        private void TryLogin(TwitterNet twitter)
        {
            try
            {
                // Schedule the update function in the UI thread.
                LayoutRoot.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    new PostLoginDelegate(UpdatePostLoginInterface), twitter.Login());
            }
            catch (WebException ex)
            {
                logger.Error("There was a problem logging in Twitter.");
                MessageBox.Show("There was a problem logging in to Twitter. " + ex.Message);
            }
            catch (RateLimitException ex)
            {
                logger.Error("There was a rate limit exception.");
                MessageBox.Show(ex.Message);
            }
            catch (ProxyAuthenticationRequiredException ex)
            {
                logger.Error("Incorrect proxy configuration.");
                MessageBox.Show("Proxy server is configured incorrectly.  Please correct the settings on the Options menu.");
            }
        }

        private void UpdatePostLoginInterface(User user)
        {
            App.LoggedInUser = user;
            if (App.LoggedInUser != null)
            {
                AppSettings.Username = UsernameTextBox.Text;
                AppSettings.Password = TwitterNet.EncryptString(TwitterNet.ToSecureString(PasswordTextBox.Password));
                AppSettings.LastUpdated = string.Empty;

                AppSettings.Save();

                UsernameTextBox.Text = string.Empty;
                PasswordTextBox.Password = string.Empty;

                RaiseEvent(new RoutedEventArgs(LoginEvent));
            }
            else
            {
                MessageBox.Show("Incorrect username or password. Please try again");
            }
        }

        public static readonly RoutedEvent LoginEvent =
            EventManager.RegisterRoutedEvent("Login", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(LoginControl));

        public event RoutedEventHandler Login
        {
            add { AddHandler(LoginEvent, value); }
            remove { RemoveHandler(LoginEvent, value); }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Set Username textbox as default focus
            UsernameTextBox.Focus();
        }
    }
}
