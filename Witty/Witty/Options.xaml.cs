using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using log4net;
using TwitterLib;
using TwitterLib.Utilities;
using Screen = System.Windows.Forms.Screen;

namespace Witty
{
    public partial class Options
    {
        private static readonly ILog logger = LogManager.GetLogger("Witty.Logging");

        private readonly Properties.Settings AppSettings = Properties.Settings.Default;

        // bool to prevent endless recursion
        private bool isInitializing = false;
        private delegate void NoArgDelegate();

        private List<User> allFriends;
        private static string allFriendsFromUserName;  // needed to detect when a user has logged out.
        private readonly TwitterNet twitter;
        
        
        private List<User> GetFriends()
        {
            if (allFriends == null || allFriendsFromUserName != AppSettings.Username)
            {
                UserCollection friends = App.friends;
                var sortedFriends = new List<User>(friends);
                sortedFriends.Sort((u, u2) => u.ScreenName.CompareTo(u2.ScreenName));
                allFriends = sortedFriends;
            }
            return allFriends;
        }

        public Options()
        {
            this.InitializeComponent();
            AlertSelectedOnlyCheckBox.IsChecked = AppSettings.AlertSelectedOnly;
            twitter = new TwitterNet(AppSettings.Username, TwitterNet.DecryptString(AppSettings.Password));
            LayoutRoot.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new NoArgDelegate(BindFriendsDropDown));            

            #region Initialize Options

            UsernameTextBox.Text = AppSettings.Username;
            PasswordTextBox.Password = AppSettings.Password;
            TwitterHostTextBox.Text = AppSettings.TwitterHost;

            RefreshComboBox.Items.Add(1);
            RefreshComboBox.Items.Add(2);
            RefreshComboBox.Items.Add(5);
            RefreshComboBox.Items.Add(10);
            RefreshComboBox.Items.Add(15);
            RefreshComboBox.Items.Add(20);
            RefreshComboBox.Items.Add(30);
            RefreshComboBox.Items.Add(60);

            if (!string.IsNullOrEmpty(AppSettings.RefreshInterval))
            {
                Double refreshInterval = Double.Parse(AppSettings.RefreshInterval);
                if (refreshInterval < 1) refreshInterval = 1; //Previously the options screen allowed setting to 0
                RefreshComboBox.Text = refreshInterval.ToString();
            }

            RetweetComboBox.Items.Add("RT");
            RetweetComboBox.Items.Add("Retweet");
            RetweetComboBox.Items.Add("\u267A");
            RetweetComboBox.Items.Add("\u00BB");

            if (!string.IsNullOrEmpty(AppSettings.RetweetPrefix))
                RetweetComboBox.Text = AppSettings.RetweetPrefix;

            ReplyComboBox.Items.Add("r");
            ReplyComboBox.Items.Add("-");
            ReplyComboBox.Items.Add(".");
            ReplyComboBox.Items.Add(">");
            ReplyComboBox.Items.Add("\u00BB");

            if (!string.IsNullOrEmpty(AppSettings.ReplyPrefix))
                ReplyComboBox.Text = AppSettings.ReplyPrefix;

            isInitializing = true;
            SkinsComboBox.ItemsSource = SkinsManager.GetSkins();

            // select the current skin
            if (!string.IsNullOrEmpty(AppSettings.Skin))
            {
                SkinsComboBox.SelectedItem = AppSettings.Skin;
            }

            UrlServiceComboBox.ItemsSource = Enum.GetValues(typeof(TwitterLib.ShorteningService));
            if (!string.IsNullOrEmpty(AppSettings.UrlShorteningService))
                UrlServiceComboBox.Text = AppSettings.UrlShorteningService;
            else
                UrlServiceComboBox.Text = TwitterLib.ShorteningService.TinyUrl.ToString();

            // select number of tweets to keep
            KeepLatestComboBox.Text = AppSettings.KeepLatest.ToString();
            isInitializing = false;

            AlwaysOnTopCheckBox.IsChecked = AppSettings.AlwaysOnTop;

            PlaySounds = AppSettings.PlaySounds;
            MinimizeToTray = AppSettings.MinimizeToTray;
            MinimizeOnClose = AppSettings.MinimizeOnClose;
            PersistLogin = AppSettings.PersistLogin;
            SmoothScrollingCheckBox.IsChecked = AppSettings.SmoothScrolling;
            RunAtStartupCheckBox.IsChecked = AppSettings.RunAtStartup;
            
            UseProxyCheckBox.IsChecked = AppSettings.UseProxy;
            ProxyServerTextBox.Text = AppSettings.ProxyServer;
            ProxyPortTextBox.Text = AppSettings.ProxyPort.ToString();
            ProxyUsernameTextBox.Text = AppSettings.ProxyUsername;
            FilterRegex.Text = AppSettings.FilterRegex;
            HightlightRegex.Text = AppSettings.HighlightRegex;

            #endregion

            // JMF: Thinking about the user experience here, rolling out something that
            //      may very well invalidate their stored settings.  I'll just flag
            //      values that are now encrypted with a prefix when saved to the AppSetting.
            if (!AppSettings.ProxyPassword.StartsWith("WittyEncrypted:"))
                ProxyPasswordTextBox.Password = AppSettings.ProxyPassword;
            else
                ProxyPasswordTextBox.Password = DecryptString(AppSettings.ProxyPassword);

            ToggleProxyFieldsEnabled(AppSettings.UseProxy);

            if (!string.IsNullOrEmpty(AppSettings.MaximumIndividualAlerts))
            {
                MaxIndSlider.Value = Double.Parse(AppSettings.MaximumIndividualAlerts);
            }

            DisplayNotificationsCheckBox.IsChecked = AppSettings.DisplayNotifications;

            if (!string.IsNullOrEmpty(AppSettings.NotificationDisplayTime))
            {
                NotificationDisplayTimeSlider.Value = Double.Parse(AppSettings.NotificationDisplayTime);
            }

            //TODO:Figure out how to get auto-start working with ClickOnce and Vista
            //Until then, don't show the auto-start option
            if (Environment.OSVersion.Version.Major > 5 && ClickOnce.Utils.IsApplicationNetworkDeployed)
                RunAtStartupCheckBox.Visibility = Visibility.Collapsed;
        }

        #region PlaySounds

        public bool PlaySounds
        {
            get { return (bool)GetValue(PlaySoundsProperty); }
            set { SetValue(PlaySoundsProperty, value); }
        }

        public static readonly DependencyProperty PlaySoundsProperty =
            DependencyProperty.Register("PlaySounds", typeof(bool), typeof(Options),
            new FrameworkPropertyMetadata(false, new PropertyChangedCallback(OnPlaySoundsChanged)));

        private static void OnPlaySoundsChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            Properties.Settings.Default.PlaySounds = (bool)args.NewValue;
            Properties.Settings.Default.Save();
        }

        #endregion

        #region MinimizeToTray

        public bool MinimizeToTray
        {
            get { return (bool)GetValue(MinimizeToTrayProperty); }
            set { SetValue(MinimizeToTrayProperty, value); }
        }

        public static readonly DependencyProperty MinimizeToTrayProperty =
            DependencyProperty.Register("MinimizeToTray", typeof(bool), typeof(Options),
            new FrameworkPropertyMetadata(false, new PropertyChangedCallback(OnMinimizeToTrayChanged)));

        private static void OnMinimizeToTrayChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            Properties.Settings.Default.MinimizeToTray = (bool)args.NewValue;
            Properties.Settings.Default.Save();
        }

        public bool MinimizeOnClose
        {
            get
            {
                return (bool)GetValue(MinimizeOnCloseProperty); 
            }
            set
            {
                SetValue(MinimizeOnCloseProperty, value);
            }
        }

        public static readonly DependencyProperty MinimizeOnCloseProperty =
        DependencyProperty.Register("MinimizeOnClose", typeof(bool), typeof(Options),
        new FrameworkPropertyMetadata(false, new PropertyChangedCallback(OnMinimizeOnCloseChanged)));

        private static void OnMinimizeOnCloseChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            Properties.Settings.Default.MinimizeOnClose = (bool)args.NewValue;
            Properties.Settings.Default.Save();
        }

        #endregion

        #region PersistLogin

        public bool PersistLogin
        {
            get { return (bool)GetValue(PersistLoginProperty); }
            set { SetValue(PersistLoginProperty, value); }
        }

        public static readonly DependencyProperty PersistLoginProperty =
            DependencyProperty.Register("PersistLogin", typeof(bool), typeof(Options),
            new FrameworkPropertyMetadata(false, new PropertyChangedCallback(OnPersistLoginChanged)));

        private static void OnPersistLoginChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            Properties.Settings.Default.PersistLogin = (bool)args.NewValue;
            Properties.Settings.Default.Save();
        }

        #endregion

        #region Clear Event Handlers

        private void ClearTweetsButton_Click(object sender, RoutedEventArgs e)
        {
            // Since, this window does not have access to the main Tweets collection,
            // call the owner window methods to handle it
            ((MainWindow)this.Owner).ClearTweets();
        }

        private void ClearRepliesButton_Click(object sender, RoutedEventArgs e)
        {
            // Since, this window does not have access to the replies collection,
            // call the owner window methods to handle it
            ((MainWindow)this.Owner).ClearReplies();
        }

        #endregion

        #region Proxy Config

        private void UseProxyCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ToggleProxyFieldsEnabled((bool)UseProxyCheckBox.IsChecked);
        }

        private void ToggleProxyFieldsEnabled(bool enabled)
        {
            ProxyServerTextBox.IsEnabled = enabled;
            ProxyPortTextBox.IsEnabled = enabled;
            ProxyUsernameTextBox.IsEnabled = enabled;
            ProxyPasswordTextBox.IsEnabled = enabled;
        }

        bool _RestartOnProxyChange = false;

        private void NotifyIfRestartNeeded()
        {
            // JMF: The HttpWebRequest.DefaultWebProxy is now being set when AppSettings are
            //      saved, so a restart is probably not necessary just because the config'd
            //      proxy settings changed.
            if (_RestartOnProxyChange &&
                    (
                        AppSettings.UseProxy != (bool)UseProxyCheckBox.IsChecked ||
                        AppSettings.ProxyServer != ProxyServerTextBox.Text ||
                        AppSettings.ProxyPort != int.Parse(ProxyPortTextBox.Text) ||
                        AppSettings.ProxyUsername != ProxyUsernameTextBox.Text ||
                        AppSettings.ProxyPassword != "WittyEncrypted:" + EncryptString(ProxyPasswordTextBox.Password)
                    )
                )
            {
                MessageBox.Show("Witty will need to be restarted before settings will take effect.");
            }
        }

        private bool InputIsValid()
        {
            // Only checking for valid port number right now, but other validation could go here later

            bool result = true;
            if (ProxyPortTextBox.Text.Trim().Length > 0)
            {
                try
                {
                    int.Parse(ProxyPortTextBox.Text);
                }
                catch (Exception ex)
                {
                    result = false;
                    MessageBox.Show("Invalid port number.");
                }
            }

            return result;
        }

        #endregion

        #region "Notifications"

        public bool DisplayNotifications
        {
            get { return (bool)GetValue(DisplayNotificationsProperty); }
            set { SetValue(MinimizeToTrayProperty, value); }
        }

        public static readonly DependencyProperty DisplayNotificationsProperty =
            DependencyProperty.Register("DisplayNotifications", typeof(bool), typeof(Options),
            new FrameworkPropertyMetadata(false, new PropertyChangedCallback(OnDisplayNotificationsChanged)));

        private static void OnDisplayNotificationsChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            Properties.Settings.Default.DisplayNotifications = (bool)args.NewValue;
            Properties.Settings.Default.Save();
        }

        #endregion

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            if (InputIsValid())
            {
                AppSettings.RefreshInterval = RefreshComboBox.Text;

                NotifyIfRestartNeeded();

                AppSettings.TwitterHost = TwitterHostTextBox.Text;

                if (!AppSettings.TwitterHost.EndsWith("/"))
                    AppSettings.TwitterHost += "/";

                AppSettings.SmoothScrolling = (bool)SmoothScrollingCheckBox.IsChecked;

                if (AppSettings.RunAtStartup != (bool)RunAtStartupCheckBox.IsChecked)
                {
                    Shortcut.SetStartupGroupShortcut((bool)RunAtStartupCheckBox.IsChecked);
                    AppSettings.RunAtStartup = (bool) RunAtStartupCheckBox.IsChecked;
                }

                AppSettings.UseProxy = (bool)UseProxyCheckBox.IsChecked;
                AppSettings.ProxyServer = ProxyServerTextBox.Text;
                AppSettings.ProxyPort = int.Parse(ProxyPortTextBox.Text);
                AppSettings.ProxyUsername = ProxyUsernameTextBox.Text;
                AppSettings.ProxyPassword = "WittyEncrypted:" + EncryptString(ProxyPasswordTextBox.Password);

                AppSettings.MaximumIndividualAlerts = MaxIndTextBlock.Text;
                AppSettings.NotificationDisplayTime = NotificationDisplayTimeTextBlock.Text;
                AppSettings.AlertSelectedOnly = (bool) AlertSelectedOnlyCheckBox.IsChecked;
                AppSettings.FilterRegex = FilterRegex.Text;
                AppSettings.HighlightRegex = HightlightRegex.Text;
                
                if(!string.IsNullOrEmpty(RetweetComboBox.Text))
                    AppSettings.RetweetPrefix = RetweetComboBox.Text;

                if(!string.IsNullOrEmpty(ReplyComboBox.Text))
                    AppSettings.ReplyPrefix = ReplyComboBox.Text;

                int setting;
                if (int.TryParse(((ComboBox)KeepLatestComboBox).Text, out setting))
                    AppSettings.KeepLatest = setting;
                else
                    AppSettings.KeepLatest = 0;

                AppSettings.Save();

                // Set (unset?) the default proxy here once and for all (should then be 
                // used by WPF controls, like images that fetch their source from the 
                // internet)
                HttpWebRequest.DefaultWebProxy = WebProxyHelper.GetConfiguredWebProxy();

                DialogResult = true;
                this.Close();
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Username = string.Empty;
            AppSettings.Password = string.Empty;
            AppSettings.LastUpdated = string.Empty;

            AppSettings.Save();

            DialogResult = false;
            this.Close();
        }

        private void SkinsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((!(isInitializing)) && (e.AddedItems.Count >= 0))
            {
                string skin = e.AddedItems[0] as string;

                SkinsManager.ChangeSkin(skin);

                AppSettings.Skin = skin;
                AppSettings.Save();
            }
        }

        private void UrlServiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((!(isInitializing)) && (e.AddedItems.Count >= 0))
            {
                AppSettings.UrlShorteningService = UrlServiceComboBox.SelectedValue.ToString();
                AppSettings.Save();
            }
        }

        /// <summary>
        /// Handles the OnLoaded event of the Window control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void Window_OnLoaded(object sender, RoutedEventArgs e)
        {
            MoveWindowInFullView();
        }

        private void MoveWindowInFullView()
        {
            // If we are off the current screen, move back in view.
            Point location = new Point(Left, Top);
            Point referenceLocation = location;
            if (Owner != null)
            {
                referenceLocation = new Point(Owner.Left, Owner.Top);
            }

            // If any edge of the window location is outside the bounds of the screen,
            // move the window back in bounds entirely.
            Rect workingArea = GetScreenWorkingArea(referenceLocation);
            if (!workingArea.Contains(location.X, workingArea.Y))
            {
                Left = workingArea.Left;
            }

            if (!workingArea.Contains(workingArea.X, location.Y))
            {
                Top = workingArea.Top;
            }

            if (!workingArea.Contains(workingArea.X, location.Y + Height))
            {
                Top = workingArea.Bottom - Height;
            }

            if (!workingArea.Contains(location.X + Width, workingArea.Y))
            {
                Left = workingArea.Right - Width;
            }
        }

        private Rect GetScreenWorkingArea(Point p)
        {
            // Get the screen rect associated with the point
            Screen screen = Screen.FromPoint(ToDrawingPoint(p));
            return ToRect(screen.WorkingArea);
        }

        // This method can go in an extension method repository, but I don't see any
        // so I'm just leaving it inline here.
        private static System.Drawing.Point ToDrawingPoint(Point p)
        {
            return new System.Drawing.Point()
                       {
                           X = Convert.ToInt32(p.X),
                           Y = Convert.ToInt32(p.Y)
                       };
        }

        // This method can go in an extension method repository, but I don't see any
        // so I'm just leaving it inline here.
        private static Rect ToRect(System.Drawing.Rectangle rect)
        {
            return new Rect()
                       {
                           X = Convert.ToDouble(rect.X),
                           Y = Convert.ToDouble(rect.Y),
                           Height = Convert.ToDouble(rect.Height),
                           Width = Convert.ToDouble(rect.Width)
                       };
        }

        /// <summary>
        /// Checks for keyboard shortcuts
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">EventArgs</param>
        private void Window_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { this.Close(); };
        }

        private void AlwaysOnTopCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)((CheckBox)sender).IsChecked)
                this.Topmost = true;
            else
                this.Topmost = false;

            AppSettings.AlwaysOnTop = this.Topmost;
            AppSettings.Save();
        }


        // REMARK: This is same encryption scheme that is used in TwitterNet class.  Should it
        //         be abstracted into a utility class?
        static byte[] entropy = System.Text.Encoding.Unicode.GetBytes("WittyPasswordSalt");
        private static string DecryptString(string encryptedData)
        {
            if (encryptedData.StartsWith("WittyEncrypted:"))
                encryptedData = encryptedData.Substring(15);

            try
            {
                byte[] decryptedData = System.Security.Cryptography.ProtectedData.Unprotect(
                    Convert.FromBase64String(encryptedData),
                    entropy,
                    System.Security.Cryptography.DataProtectionScope.CurrentUser);
                return System.Text.Encoding.Unicode.GetString(decryptedData);
            }
            catch
            {
                return String.Empty;
            }
        }

        private static string EncryptString(string input)
        {
            byte[] encryptedData = System.Security.Cryptography.ProtectedData.Protect(
                System.Text.Encoding.Unicode.GetBytes(input),
                entropy,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedData);
        }

        private User SelectedFriend
        {
            get
            {
                return (User)FriendsOptionsComboBox.SelectedItem;   
            }            
        }



        private void FriendsOptionsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            
            UserBehaviorManager manager = AppSettings.UserBehaviorManager;            
            FriendIgnoreCheckBox.IsChecked = manager.HasBehavior(SelectedFriend.Name, UserBehavior.Ignore);
            FriendAlwaysAlertCheckbox.IsChecked = manager.HasBehavior(SelectedFriend.Name, UserBehavior.AlwaysAlert);
            FriendNeverAlertCheckbox.IsChecked = manager.HasBehavior(SelectedFriend.Name, UserBehavior.NeverAlert);
            
        }

        private void FriendAlwaysAlertCheckbox_Clicked(object sender, RoutedEventArgs e)
        {

            UpdateStatus(SelectedFriend, UserBehavior.AlwaysAlert);
        }

        private void FriendIgnoreCheckBox_Clicked(object sender, RoutedEventArgs e)
        {
            UpdateStatus(SelectedFriend, UserBehavior.Ignore);
        }

        private void FriendNeverAlertCheckbox_Clicked(object sender, RoutedEventArgs e)
        {
            UpdateStatus(SelectedFriend, UserBehavior.NeverAlert);
        }

        private void BindFriendsDropDown()
        {
            FriendsOptionsComboBox.ItemsSource = GetFriends();
            FriendsOptionsComboBox.DisplayMemberPath = "ScreenName";                
        }

        private void UpdateStatus(User user, UserBehavior behavior)
        {
            if (user == null) return;            
            AppSettings.UserBehaviorManager.AddBehavior(user.Name, behavior);
            UpdateUserBehaviorAppSetting();
            FriendIgnoreCheckBox.IsChecked = (behavior == UserBehavior.Ignore);
            FriendAlwaysAlertCheckbox.IsChecked = (behavior == UserBehavior.AlwaysAlert);
            FriendNeverAlertCheckbox.IsChecked = (behavior == UserBehavior.NeverAlert);
        }

        private void ClearBehaviors(object sender, RoutedEventArgs e)
        {
            if (SelectedFriend == null) return;
            AppSettings.UserBehaviorManager.RemoveBehavior(SelectedFriend.Name);
            UpdateUserBehaviorAppSetting();
        }

        private void UpdateUserBehaviorAppSetting()
        {
            AppSettings.SerializedUserBehaviors = AppSettings.UserBehaviorManager.Serialize();
            AppSettings.Save();
        }
    }
}
