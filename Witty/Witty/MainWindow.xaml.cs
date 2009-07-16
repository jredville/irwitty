using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Deployment.Application;
using System.Media;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Snarl;
using Witty.Misc.Growl;
using TwitterLib;
using TwitterLib.Utilities;
using Witty.ClickOnce;
using Witty.Properties;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace Witty
{
    public partial class MainWindow
    {

        #region Fields and Properties

        private IntPtr SnarlConfighWnd;
        private NativeWindowApplication.WittySnarlMsgWnd snarlComWindow;
        private bool reallyexit = false;

        //for conversation
        private double conversationStartId;
    
        // Main collection of tweets
        private TweetCollection tweets = new TweetCollection();

        // Main collection of replies
        private TweetCollection replies = new TweetCollection();

        private DateTime repliesLastUpdated;

        // Main collection of user Tweets
        private TweetCollection userTweets = new TweetCollection();

        // Main collection of "more" Tweets
        private TweetCollection moreTweets = new TweetCollection();

        // Main collection of direct messages
        private DirectMessageCollection messages = new DirectMessageCollection();

        private Dictionary<string, DateTime> ignoredUsers = new Dictionary<string, DateTime>();

        private DateTime messagesLastUpdated;

        // Main TwitterNet object used to make Twitter API calls
        private IServiceApi twitter;

        // Timer used for automatic tweet updates
        private DispatcherTimer refreshTimer = new DispatcherTimer();

        // How often the automatic tweet updates occur.  TODO: Make this configurable
        private TimeSpan refreshInterval;
        private TimeSpan friendsRefreshInterval = new TimeSpan(0, 45, 0);

        private DispatcherTimer friendsRefreshTimer = new DispatcherTimer();

        private DateTime lastFriendsUpdate = DateTime.MinValue;

        // Delegates for placing jobs onto the thread dispatcher.  
        // Used for making asynchronous calls to Twitter so that the UI does not lock up.
        private delegate void NoArgDelegate();
        private delegate void OneArgDelegate(TweetCollection arg);
        private delegate void OneStringArgDelegate(string arg);
        private delegate void OneDoubleArgDelegate(double id);
        private delegate void AddTweetsUpdateDelegate(TweetCollection arg);
        private delegate void MessagesDelegate(DirectMessageCollection arg);
        private delegate void SendMessageDelegate(string user, string text);
        private delegate void LoginDelegate(User arg);
        private delegate void DeleteTweetDelegate(double id);

        // Settings used by the application
        private Properties.Settings AppSettings = Properties.Settings.Default;

        // booleans to keep track of state
        private bool isExpanded;
        private bool isLoggedIn;
        private bool isReplyMessage;
        private bool isMessageExpanded;
        private bool ignoreKey;
        private bool tweetFormattingMayBeRequired;
        private bool isInAutocompleteMode;

        private DateTime? lastTruncatedTweetTime;

        private enum CurrentView
        {
            Recent, Replies, User, Messages, More
        }

        private UserCollection friends
        {
            get { return App.friends; }
            set { App.friends = value; }
        }

        private CurrentView currentView
        {
            get
            {
                switch (Tabs.SelectedIndex)
                {
                    case 0:
                        return CurrentView.Recent;
                    case 1:
                        return CurrentView.Replies;
                    case 2:
                        return CurrentView.User;
                    case 3:
                        return CurrentView.Messages;
                    case 4:
                        return CurrentView.More;
                    default:
                        return CurrentView.Recent;
                }
            }
        }

        private MoreResults moreResults
        {
            get { return MoreResults.Conversation; }
        }

        private string displayUser;

        private Deployment _clickOnce;
        private System.Windows.Threading.DispatcherTimer _clickOnceUpdateTimer;

        private int popupCount = 0;

        internal Tweet SelectedTweet
        {
            get
            {
                Tweet selectedTweet = null;
                if (this.currentView == CurrentView.Replies)
                {
                    if (null != RepliesListBox.SelectedItem) selectedTweet = (Tweet)RepliesListBox.SelectedItem;
                }
                else if (this.currentView == CurrentView.Messages)
                {
                    if (null != MessagesListBox.SelectedItem) selectedTweet = ((DirectMessage)MessagesListBox.SelectedItem).ToTweet();
                }
                else if (this.currentView == CurrentView.User)
                {
                    if (null != UserTimelineListBox.SelectedItem) selectedTweet = (Tweet)UserTimelineListBox.SelectedItem;
                }
                else
                {
                    if (null != TweetsListBox.SelectedItem) selectedTweet = (Tweet)TweetsListBox.SelectedItem;
                }
                return selectedTweet;
            }
        }

        SoundPlayer _player;
        #endregion

        #region Constructor
        public MainWindow()
        {
            this.InitializeComponent();            

            TrapUnhandledExceptions();

            SetupNotifyIcon();

            SetupSingleInstance();

            SetDataContextForAllOfTabs();

            SetHowOftenToGetUpdatesFromTwitter();

            InitializeClickOnceTimer();

            InitializeSoundPlayer();

            InitializeMiscSettings();

            RegisterWithSnarlIfAvailable();

            RegisterWithGrowlIfAvailable();

            DisplayLoginIfUserNotLoggedIn();
            
            SetTweetSorting();
        }

        private void SetTweetSorting()
        {
            ICollectionView collectionView = CollectionViewSource.GetDefaultView(tweets);
            collectionView.SortDescriptions.Add(new SortDescription("DateCreated", ListSortDirection.Descending));
        }
        #endregion

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // If the user selected to minimize on close and the window state is normal
            // just minimize the app
            if (AppSettings.MinimizeOnClose && this.reallyexit == false)
            {
                e.Cancel = true;
                _storedWindowState = this.WindowState;
                this.WindowState = WindowState.Minimized;
                if (_notifyIcon != null)
                {
                    _notifyIcon.ShowBalloonTip(2000);
                }
            }
        }

        protected override Decorator GetWindowButtonsPlaceholder()
        {
            return WindowButtonsPlaceholder;
        }

        private void Header_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove();
        }

        private void Thumb_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            Point position = Mouse.GetPosition(Application.Current.MainWindow);
            if (position.X > 10 && position.Y > 10)
            {
                this.Width = position.X;
                this.Height = position.Y;
            }
        }
        
        #region Retrieve new tweets

        /// <summary>
        /// Encapsulated method to create dispatcher for fetching new tweets asynchronously
        /// </summary>
        private void DelegateRecentFetch()
        {
            // Let the user know what's going on
            StatusTextBlock.Text = "Retrieving tweets...";

            PlayStoryboard("Fetching");

            // Create a Dispatcher to fetching new tweets
            NoArgDelegate fetcher = new NoArgDelegate(
                this.GetTweets);

            fetcher.BeginInvoke(null, null);

        }

        private void InitializeMiscSettings()
        {
            AppSettings.UserBehaviorManager = new UserBehaviorManager(AppSettings.SerializedUserBehaviors);
            this.Topmost = AlwaysOnTopMenuItem.IsChecked = AppSettings.AlwaysOnTop;
            ScrollViewer.SetCanContentScroll(TweetsListBox, !AppSettings.SmoothScrolling);
            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (!ApplicationDeployment.IsNetworkDeployed)
            {
                Title = string.Format("Witty {0}.{1}", version.Major, version.Minor);
#if DEBUG
                Title = Title + string.Format("{0} Debug", version.Revision);
#endif
            }
            else
            {
                // update window with clickonce version number
                this.Title = AppSettings.ApplicationName + " " + ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
            }
        }

        private void Timer_Elapsed(object sender, EventArgs e)
        {
            DelegateRecentFetch();
        }

        private void GetTweets()
        {
            try
            {
                // Schedule the update functions in the UI thread.
                LayoutRoot.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new OneArgDelegate(UpdateUserInterface), twitter.GetFriendsTimeline());

                // Direct message and replies < 70 hours old will be displayed on the recent tab.
                // Once this somewhat arbitrary (Friday, 5pm - Monday, 9am + 6 hours) threshold is met, 
                // users will still be able to access there direct messages and replies via their
                // respective tabs.  
                //TODO: Make DM and Reply threshold configurable.  Rework this logic once concept of viewed tweets is introduced to Witty.
                string since = DateTime.Now.AddHours(-70).ToString();

                LayoutRoot.Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new OneArgDelegate(UpdateUserInterface), twitter.GetReplies(since));

                LayoutRoot.Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new OneArgDelegate(UpdateUserInterface), twitter.RetrieveMessages(since).ToTweetCollection());
            }
            catch (RateLimitException ex)
            {
                App.Logger.Debug(String.Format("There was a problem fetching new tweets from Twitter.com: {0}", ex.ToString()));
                LayoutRoot.Dispatcher.BeginInvoke(
                    DispatcherPriority.ApplicationIdle,
                    new OneStringArgDelegate(ShowStatus), ex.Message);
            }
            catch (WebException ex)
            {
                App.Logger.Debug(String.Format("There was a problem fetching new tweets from Twitter.com: {0}", ex.ToString()));
            }
            catch (ProxyAuthenticationRequiredException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show("Proxy server is configured incorrectly.  Please correct the settings on the Options menu.");
            }
            catch (ProxyNotFoundException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show(ex.Message);
            }

        }

        private void SetHowOftenToGetUpdatesFromTwitter()
        {
            // Set how often to get updates from Twitter
            refreshInterval = new TimeSpan(0, int.Parse(AppSettings.RefreshInterval), 0);
        }

        private void SetDataContextForAllOfTabs()
        {
            // Set the data context for all of the tabs
            LayoutRoot.DataContext = tweets;
            RepliesListBox.ItemsSource = replies;
            UserTab.DataContext = userTweets;
            MessagesListBox.ItemsSource = messages;
            MoreListBox.DataContext = moreTweets;
        }

        private void TrapUnhandledExceptions()
        {
            LayoutRoot.Dispatcher.UnhandledException += new DispatcherUnhandledExceptionEventHandler(Dispatcher_UnhandledException);
        }

        /// <summary>
        /// Enforce single instance for release mode
        /// </summary>
        private void SetupSingleInstance()
        {
#if !DEBUG
            Application.Current.Exit += new ExitEventHandler(Current_Exit);
            _instanceManager = new SingleInstanceManager(this, ShowApplication);
#endif
        }

        private void DisplayLoginIfUserNotLoggedIn()
        {
            // Does the user need to login?
            if (string.IsNullOrEmpty(AppSettings.Username))
            {
                PlayStoryboard("ShowLogin");
            }
            else
            {
                LoginControl.Visibility = Visibility.Hidden;

                System.Security.SecureString password = TwitterNet.DecryptString(AppSettings.Password);

                // Jason Follas: Reworked Web Proxy - don't need to explicitly pass into TwitterNet ctor
                //twitter = new TwitterNet(AppSettings.Username, password, WebProxyHelper.GetConfiguredWebProxy());
                twitter = new TwitterNet(AppSettings.Username, password);

                // Jason Follas: Twitter proxy servers, anyone?  (Network Nazis who block twitter.com annoy me)
                twitter.TwitterServerUrl = AppSettings.TwitterHost;

                // Let the user know what's going on
                StatusTextBlock.Text = Properties.Resources.TryLogin;
                PlayStoryboard("Fetching");

                // Create a Dispatcher to attempt login on new thread
                NoArgDelegate loginFetcher = new NoArgDelegate(this.TryLogin);
                loginFetcher.BeginInvoke(null, null);

            }
        }

        private void RegisterWithSnarlIfAvailable()
        {
            if (SnarlConnector.GetSnarlWindow().ToInt32() != 0)
            {
                CreateSnarlMessageWindowForCommunication();
            }
        }

        private void CreateSnarlMessageWindowForCommunication()
        {
            this.snarlComWindow = new NativeWindowApplication.WittySnarlMsgWnd();
            this.SnarlConfighWnd = snarlComWindow.Handle;
            
            SnarlConnector.RegisterConfig(this.SnarlConfighWnd, "Witty", Snarl.WindowsMessage.WM_USER + 55);

            SnarlConnector.RegisterAlert("Witty", "New tweet");
            SnarlConnector.RegisterAlert("Witty", "New tweets summarized");
            SnarlConnector.RegisterAlert("Witty", "New reply");
            SnarlConnector.RegisterAlert("Witty", "New direct message");
        }

        private void RegisterWithGrowlIfAvailable()
        {
            GrowlCommunicator.Register();
        }

        private void UpdateUserInterface(TweetCollection newTweets)
        {
            DateTime lastUpdated = DateTime.Now;
            StatusTextBlock.Text = "Last Updated: " + lastUpdated.ToLongTimeString();

            AppSettings.LastUpdated = lastUpdated.ToString();
            AppSettings.Save();

            FilterTweets(newTweets, true);
            HighlightTweets(newTweets);
            UpdateExistingTweets();

            TweetCollection addedTweets = new TweetCollection();

            //prevents huge number of notifications appearing on startup
            bool displayPopups = !(tweets.Count == 0);

            // Add the new tweets
            for (int i = newTweets.Count - 1; i >= 0; i--)
            {
                Tweet tweet = newTweets[i];

                if (tweets.Contains(tweet) || HasBehavior(tweet, UserBehavior.Ignore) || IsTruncatedTweet(tweet))
                    continue;

                tweets.Add(tweet);
                tweet.Index = tweets.Count;
                tweet.IsNew = true;
                addedTweets.Add(tweet);
            }

            // tweets listbox ScrollViewer.CanContentScroll is set to "False", which means it scrolls more smooth,
            // However it disables Virtualization
            // Remove tweets pass 100 should improve performance reasons.
            if (AppSettings.KeepLatest != 0)
            {
                if (tweets.Count > AppSettings.KeepLatest)
                    lastTruncatedTweetTime = tweets[AppSettings.KeepLatest - 1].DateCreated;

                tweets.TruncateAfter(AppSettings.KeepLatest);
            }

            if (addedTweets.Count > 0)
            {
                if (AppSettings.DisplayNotifications && !(bool)this.IsActive)
                    NotifyOnNewTweets(addedTweets, "tweet");

                if (AppSettings.PlaySounds)
                {
                    // Author: Keith Elder
                    // I wrapped a try catch around this and added logging.
                    // I found that the Popup screen and this were causing 
                    // a threading issue.  At least that is my theory.  When
                    // new items would come in, and play a sound as well as 
                    // pop a new message there was no need to recreate and load
                    // the wave file.  InitializeSoundPlayer() was added on load
                    // to do that just once.
                    try
                    {
                        // Play tweets found sound for new tweets
                        _player.Play();
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error("Error playing sound", ex);
                    }
                }
            }

            StopStoryboard("Fetching");
        }

       
       
        private bool IsTruncatedTweet(Tweet tweet)
        {
            if (tweet.DateCreated < lastTruncatedTweetTime)
                return true;

            return false;
        }

        private void FilterTweets(TweetCollection tweets, bool filterUsers)
        {
            bool usersToFilter = filterUsers && (ignoredUsers.Count > 0);
            if (string.IsNullOrEmpty(AppSettings.FilterRegex) && !usersToFilter)
                return;

            for (int i = tweets.Count - 1; i >= 0; i--)
            {
                Tweet tweet = tweets[i];
                if (!string.IsNullOrEmpty(AppSettings.FilterRegex) && Regex.IsMatch(tweet.Text, AppSettings.FilterRegex, RegexOptions.IgnoreCase))
                    tweets.Remove(tweet);
                else if (ignoredUsers.ContainsKey(tweet.User.ScreenName) && ignoredUsers[tweet.User.ScreenName] > DateTime.Now)
                    tweets.Remove(tweet);
            }
        }

        private void HighlightTweets(TweetCollection tweets)
        {
            if (string.IsNullOrEmpty(AppSettings.HighlightRegex))
                return;
            
            foreach (Tweet tweet in tweets)
            {
                if (Regex.IsMatch(tweet.Text, AppSettings.HighlightRegex, RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(tweet.User.ScreenName, AppSettings.HighlightRegex, RegexOptions.IgnoreCase))
                {
                    tweet.IsInteresting = true;
                }
            }
        }

        private void SetupNotifyIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.BalloonTipText = "Right-click for more options";
            _notifyIcon.BalloonTipTitle = "Witty";
            _notifyIcon.Text = "Witty - The WPF Twitter Client";
            _notifyIcon.Icon = Witty.Properties.Resources.AppIcon;
            _notifyIcon.DoubleClick += new EventHandler(m_notifyIcon_Click);

            System.Windows.Forms.ContextMenu notifyMenu = new System.Windows.Forms.ContextMenu();
            System.Windows.Forms.MenuItem openMenuItem = new System.Windows.Forms.MenuItem();
            System.Windows.Forms.MenuItem exitMenuItem = new System.Windows.Forms.MenuItem();

            notifyMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] { openMenuItem, exitMenuItem });
            openMenuItem.Index = 0;
            openMenuItem.Text = "Open";
            openMenuItem.Click += new EventHandler(openMenuItem_Click);
            exitMenuItem.Index = 1;
            exitMenuItem.Text = "Exit";
            exitMenuItem.Click += new EventHandler(exitMenuItem_Click);

            _notifyIcon.ContextMenu = notifyMenu;

            this.Closed += new EventHandler(OnClosed);
            this.StateChanged += new EventHandler(OnStateChanged);
            this.IsVisibleChanged += new DependencyPropertyChangedEventHandler(OnIsVisibleChanged);
            //this.Loaded += new RoutedEventHandler(OnLoaded);
            OverrideClosing();
        }

        //[DllImport("user32.dll")]
        //static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        //[DllImport("user32.dll")]
        //static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        //void OnLoaded(object sender, RoutedEventArgs e)
        //{
        //    WindowInteropHelper wndHelper = new WindowInteropHelper(this); 
        //    int exStyle = (int)GetWindowLong(wndHelper.Handle, (int)GetWindowLongFields.GWL_EXSTYLE); 
        //    exStyle |= (int)ExtendedWindowStyles.WS_EX_TOOLWINDOW; 
        //    SetWindowLong(wndHelper.Handle, (int)GetWindowLongFields.GWL_EXSTYLE, (IntPtr)exStyle);
        //}

        private void ParoleIgnoredUsers()
        {
            List<string> paroledUsers = new List<string>();
            foreach (KeyValuePair<string, DateTime> ignoredUser in ignoredUsers)
            {
                if (ignoredUser.Value < DateTime.Now.AddHours(6))
                    paroledUsers.Add(ignoredUser.Key);
            }
            paroledUsers.ForEach(userName => ignoredUsers.Remove(userName));
        }

        private void NotifyOnNewTweets(TweetCollection newTweets, string type)
        {
            if (GrowlCommunicator.IsRunning())
            {
                GrowlNotify(newTweets, type);
            }
            else if (SnarlConnector.GetSnarlWindow().ToInt32() != 0)
            {
                SnarlNotify(newTweets, type);
            }
            else
            {
                PopUpNotify(newTweets);
            }
        }

        private void SetupPopupProps(Popup p)
        {
            p.FadeOutFinished += new FadeOutFinishedDelegate(RemovePopup);
            p.ReplyClicked += new PopupReplyClickedDelegate(PopupReplyClicked);
            p.DirectMessageClicked += new PopupDirectMessageClickedDelegate(PopupDirectMessageClicked);
            p.Clicked += new PopupClickedDelegate(PopupClicked);
            p.CloseButtonClicked += new PopupCloseButtonClickedDelegate(RemovePopup);
        }

        private bool ShouldPopUp(Tweet tweet)
        {
            if (AppSettings.AlertSelectedOnly)            
                return HasBehavior(tweet, UserBehavior.AlwaysAlert);

            return (!HasBehavior(tweet, UserBehavior.NeverAlert) || HasBehavior(tweet, UserBehavior.Ignore));

        }

        private bool HasBehavior(Tweet tweet, UserBehavior behavior)
        {
            return AppSettings.UserBehaviorManager.HasBehavior(tweet.User.Name, behavior);
        }

        private void PopUpNotify(TweetCollection newTweets)
        {
            TweetCollection popUpTweets = new TweetCollection();
            
            foreach (var tweet in newTweets)
            {
                if (ShouldPopUp(tweet))
                    popUpTweets.Add(tweet);
            }

            if (popUpTweets.Count > Double.Parse(AppSettings.MaximumIndividualAlerts))
            {
                Popup p = new Popup("New Tweets", BuiltNewTweetMessage(popUpTweets), twitter.CurrentlyLoggedInUser.ImageUrl, 0);
                SetupPopupProps(p);
                p.Show();
            }
            else
            {
                int index = 0;
                foreach (Tweet tweet in popUpTweets)
                {
                    Popup p = new Popup(tweet, index++);
                    SetupPopupProps(p);
                    p.Show();
                }
            }
        }

        private static string BuiltNewTweetMessage(TweetCollection newTweets)
        {
            string message = string.Format("You have {0} new tweets!\n", newTweets.Count);
            foreach (Tweet tweet in newTweets)
            {
                message += " " + tweet.User.ScreenName;
            }
            if (message.Length > TwitterNet.CharacterLimit)
            {
                message = message.Substring(0, (TwitterNet.CharacterLimit - 5));
                int lastSpace = message.LastIndexOf(' ');
                message = message.Substring(0, lastSpace) + "...";
            }
            return TruncateMessage(message);
        }

        private static string TruncateMessage(string message)
        {
            if (message.Length > TwitterNet.CharacterLimit)
            {
                message = message.Substring(0, (TwitterNet.CharacterLimit - 5));
                int lastSpace = message.LastIndexOf(' ');
                message = message.Substring(0, lastSpace) + "...";
            }
            return message;
        }

        private void SnarlNotify(TweetCollection newTweets, string type)
        {
            string alertClass = "";
            if (type == "reply")
            {
                alertClass = "New reply";
            }
            else if (type == "directMessage")
            {
                alertClass = "New direct message";
            }
            else
            {
                alertClass = "New tweet";
            }

            if (newTweets.Count > Double.Parse(AppSettings.MaximumIndividualAlerts))
            {
                string defaultImage = "";
                string tempFile = System.IO.Path.GetTempFileName();
                WebClient client = new WebClient();
                try
                {
                    client.DownloadFile(twitter.CurrentlyLoggedInUser.ImageUrl, tempFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    tempFile = defaultImage;
                }


                WindowsMessage replyMsg = new WindowsMessage();

                SnarlConnector.ShowMessageEx("New tweets summarized", "You have new tweets!", BuiltNewTweetMessage(newTweets), int.Parse(Properties.Settings.Default.NotificationDisplayTime), tempFile, this.SnarlConfighWnd, replyMsg, "");
                if (tempFile != defaultImage)
                {
                    System.IO.File.Delete(tempFile);
                }
            }
            else
            {
                foreach (Tweet tweet in newTweets)
                {
                    string defaultImage = "";

                    string tempFile = System.IO.Path.GetTempFileName();
                    WebClient client = new WebClient();
                    try
                    {
                        client.DownloadFile(tweet.User.ImageUrl, tempFile);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        tempFile = defaultImage;
                    }

                    WindowsMessage replyMsg = new WindowsMessage();
                    SnarlConnector.ShowMessageEx(alertClass, tweet.User.ScreenName, string.Format("{0}\n\n{1}", tweet.Text, tweet.RelativeTime), int.Parse(Properties.Settings.Default.NotificationDisplayTime), tempFile, this.SnarlConfighWnd, replyMsg, "");
                    if (tempFile != defaultImage)
                    {
                        System.IO.File.Delete(tempFile);
                    }
                }
            }
        }

        private void GrowlNotify(TweetCollection newTweets, string type)
        {
            Growl.Connector.NotificationType notificationType = null;
            if (type == "reply")
            {
                notificationType = GrowlCommunicator.NewReply;
            }
            else if (type == "directMessage")
            {
                notificationType = GrowlCommunicator.NewDirectMessage;
            }
            else if (newTweets.Count > Double.Parse(AppSettings.MaximumIndividualAlerts))
            {
                notificationType = GrowlCommunicator.NewTweetsSummary;
            }
            else
            {
                notificationType = GrowlCommunicator.NewTweet;
            }

            if (newTweets.Count > Double.Parse(AppSettings.MaximumIndividualAlerts))
            {
                GrowlCommunicator.Notify(notificationType, "You have new tweets!", BuiltNewTweetMessage(newTweets), twitter.CurrentlyLoggedInUser.ImageUrl);
            }
            else
            {
                foreach (Tweet tweet in newTweets)
                {
                    GrowlCommunicator.Notify(notificationType, tweet.User.ScreenName, String.Format("{0}\n\n{1}", tweet.Text, tweet.RelativeTime), tweet.User.ImageUrl);
                }
            }
        }

        private void UpdateExistingTweets()
        {
            UpdateExistingTweets(tweets);
        }

        private static void UpdateExistingTweets(TweetCollection oldTweets)
        {
            // Update existing tweets
            foreach (Tweet tweet in oldTweets)
            {
                tweet.IsNew = false;
                tweet.UpdateRelativeTime();
            }
        }

  

        #endregion

        #region Add new tweet update

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TweetTextBox.Text))
            {
                // Schedule posting the tweet

                UpdateButton.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new OneStringArgDelegate(AddTweet), TweetTextBox.Text);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelTweet();
        }

        private void CancelTweet()
        {
            isInAutocompleteMode = false;
            TweetTextBox.Text = string.Empty;
            ToggleUpdate();
        }

        private void ParseTextHereAndTinyUpAnyURLsFound(ref string tweetText)
        {
            //parse the text here and tiny up any URLs found.
            ShorteningService service;
            if (!string.IsNullOrEmpty(AppSettings.UrlShorteningService))
                service = (ShorteningService)Enum.Parse(typeof(ShorteningService), AppSettings.UrlShorteningService);
            else
                service = ShorteningService.isgd;

            UrlShorteningService urlHelper = new UrlShorteningService(service);
            tweetText = urlHelper.ShrinkUrls(tweetText);
        }

        private void ScheduleUpdateFunctionInUIThread(TweetCollection addedTweets)
        {
            LayoutRoot.Dispatcher.BeginInvoke(
                            DispatcherPriority.Background,
                            new AddTweetsUpdateDelegate(UpdatePostUserInterface), addedTweets);
        }

        private void AddTweet(string tweetText)
        {
            try
            {
                //bgriswold: Keeping check in place in case final character string is URL and it wasn't reformatted on the fly.
                if (tweetText.Length > TwitterNet.CharacterLimit) 
                {
                    ParseTextHereAndTinyUpAnyURLsFound(ref tweetText);
                }

                var addedTweets = new TweetCollection();

                string[] statuses = TweetSplitter.SplitTweet(tweetText);
                foreach (string status in statuses)
                {
                    if (isReplyMessage)
                    {
                        addedTweets.Add(twitter.AddTweet(status, SelectedTweet.Id));
                    }
                    else 
                    {
                        addedTweets.Add(twitter.AddTweet(status));
                    }
                }

                if (statuses.Length > 0)
                {
                    ScheduleUpdateFunctionInUIThread(addedTweets);
                }

            }
            catch (WebException ex)
            {
                UpdateTextBlock.Text = "Update failed.";
                App.Logger.Debug(String.Format("There was a problem fetching new tweets from Twitter.com: {0}", ex.ToString()));
            }
            catch (ProxyAuthenticationRequiredException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show("Proxy server is configured incorrectly.  Please correct the settings on the Options menu.");
            }
            catch (ProxyNotFoundException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show(ex.Message);
            }

        }

        private void UpdatePostUserInterface(TweetCollection addedTweets)
        {
            if (addedTweets != null)
            {
                UpdateTextBlock.Text = "Update";
                StatusTextBlock.Text = "Status Updated!";
                PlayStoryboard("CollapseUpdate");
                isExpanded = false;
                isReplyMessage = false;
                TweetTextBox.Clear();

                foreach (Tweet tweet in addedTweets)
                {
                    if (tweet.IsDirectMessage)
                    {
                        string dmTarget = GetDMTarget(tweet);
                        StatusTextBlock.Text = string.Format("Direct message to {0} sent", dmTarget);
                    }
                    else
                    {
                    tweets.Insert(0, tweet);
                }
            }
            }
            else
            {
                App.Logger.Error("There was a problem posting your tweet to Twitter.com.");
                MessageBox.Show("There was a problem posting your tweet to Twitter.com.");
            }
        }

        private string GetDMTarget(Tweet tweet)
        {
            Match m = Regex.Match(tweet.Text, @"\b[Dd]\s+(?<dmTarget>\w+)\s+");
            return m.Groups["dmTarget"].Value;
        }

        private void Update_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ToggleUpdate();
        }

        private void ToggleUpdate()
        {
            if (isLoggedIn)
            {
                if (!isExpanded)
                {
                    if (null == friends)
                    {
                        DispatchFriendsList();
                    }

                    PlayStoryboard("ExpandUpdate");
                    Update.ToolTip = "Hide update panel";
                    TweetTextBox.Focus();
                    isExpanded = true;
                }
                else
                {
                    Keyboard.Focus(CancelButton); 
                    PlayStoryboard("CollapseUpdate");
                    Update.ToolTip = "Display update panel";
                    isExpanded = false;
                    isReplyMessage = false;
                }
            }
        }
        #endregion

        #region Replies

        private void DelegateRepliesFetch()
        {
            // Let the user know what's going on
            StatusTextBlock.Text = "Retrieving replies...";

            PlayStoryboard("Fetching");

            // Create a Dispatcher to fetching new tweets
            NoArgDelegate fetcher = new NoArgDelegate(
                this.GetReplies);

            fetcher.BeginInvoke(null, null);
        }

        private void GetReplies()
        {
            try
            {
                // Schedule the update function in the UI thread.
                LayoutRoot.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new OneArgDelegate(UpdateRepliesInterface), twitter.GetReplies());
            }
            catch (WebException ex)
            {
                App.Logger.Debug(String.Format("There was a problem fetching your replies from Twitter.com. ", ex.Message));
            }
            catch (ProxyAuthenticationRequiredException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show("Proxy server is configured incorrectly.  Please correct the settings on the Options menu.");
            }
            catch (ProxyNotFoundException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show(ex.Message);
            }

        }

        private void UpdateRepliesInterface(TweetCollection newReplies)
        {
            repliesLastUpdated = DateTime.Now;
            StatusTextBlock.Text = "Replies Updated: " + repliesLastUpdated.ToLongTimeString();

            UpdateExistingTweets(replies);
            TweetCollection addedReplies = new TweetCollection();

            for (int i = newReplies.Count - 1; i >= 0; i--)
            {
                Tweet reply = newReplies[i];
                if (!replies.Contains(reply))
                {
                    replies.Insert(0, reply);
                    reply.Index = replies.Count;
                    reply.IsNew = true;
                    addedReplies.Add(reply);
                }
            }

            if (addedReplies.Count > 0)
            {
                if (AppSettings.DisplayNotifications && !(bool)this.IsActive)
                    NotifyOnNewTweets(addedReplies, "reply");

                if (AppSettings.PlaySounds)
                {
                    // Author: Keith Elder
                    // I wrapped a try catch around this and added logging.
                    // I found that the Popup screen and this were causing 
                    // a threading issue.  At least that is my theory.  When
                    // new items would come in, and play a sound as well as 
                    // pop a new message there was no need to recreate and load
                    // the wave file.  InitializeSoundPlayer() was added on load
                    // to do that just once.
                    try
                    {
                        // Play tweets found sound for new tweets
                        _player.Play();
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Error("Error playing sound", ex);
                    }
                }
            }

            StopStoryboard("Fetching");
        }

        #endregion

        #region Messages

        private void DelegateMessagesFetch()
        {
            // Let the user know what's going on
            StatusTextBlock.Text = "Retrieving direct messages...";

            PlayStoryboard("Fetching");

            // Create a Dispatcher to fetching new tweets
            NoArgDelegate fetcher = new NoArgDelegate(
                this.GetMessages);

            fetcher.BeginInvoke(null, null);
        }

     
        private void GetMessages()
        {
            try
            {
                // Schedule the update function in the UI thread.
                LayoutRoot.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new MessagesDelegate(UpdateMessagesInterface), twitter.RetrieveMessages());
            }
            catch (WebException ex)
            {
                App.Logger.Debug(String.Format("There was a problem fetching your direct messages from Twitter.com: {0}", ex.ToString()));
            }
            catch (ProxyAuthenticationRequiredException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show("Proxy server is configured incorrectly.  Please correct the settings on the Options menu.");
            }
            catch (ProxyNotFoundException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show(ex.Message);
            }

        }

        private void UpdateMessagesInterface(DirectMessageCollection newMessages)
        {
            messagesLastUpdated = DateTime.Now;
            StatusTextBlock.Text = "Messages Updated: " + messagesLastUpdated.ToLongTimeString();

            for (int i = newMessages.Count - 1; i >= 0; i--)
            {
                DirectMessage message = newMessages[i];
                if (!messages.Contains(message))
                {
                    messages.Insert(0, message);
                    message.IsNew = true;

                }
                else
                {
                    // update the relativetime for existing messages
                    //messages[i].UpdateRelativeTime();
                }
            }

            StopStoryboard("Fetching");
        }

        #endregion

        #region More Tab (conversation, search...etc)
        private enum MoreResults
        {
            Conversation
        }

        private void ViewConversation()
        {
            Tweet selectedTweet = SelectedTweet as Tweet;
            conversationStartId = selectedTweet.Id;
            //this.Tabs.SelectedIndex = 4;
            DelegateConversationFetch(conversationStartId);


        }

        private void DelegateConversationFetch(double id)
        {
            MoreTab.IsSelected = true;
            MoreContextMenu.IsEnabled = true; 

            moreTweets.Clear();
            // Let the user know what's going on
            StatusTextBlock.Text = "Retrieving conversation...";

            PlayStoryboard("Fetching");

            // Create a Dispatcher to fetching new tweets
            LayoutRoot.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new OneDoubleArgDelegate(GetConversation), id);

        }

        private void GetConversation(double id)
        {
            try
            {


                // Schedule the update function in the UI thread.
                LayoutRoot.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new OneArgDelegate(UpdateMoreInterface), twitter.GetConversation(id));
            }
            catch (WebException ex)
            {
                App.Logger.Debug(String.Format("There was a problem fetching this conversation from Twitter.com. ", ex.Message));
            }
            catch (ProxyAuthenticationRequiredException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show("Proxy server is configured incorrectly.  Please correct the settings on the Options menu.");
            }
            catch (ProxyNotFoundException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show(ex.Message);
            }

        }
        private void UpdateMoreInterface(TweetCollection newTweets)
        {
            StatusTextBlock.Text = "Viewing " + moreResults;
           

            for (int i = newTweets.Count - 1; i >= 0; i--)
            {
                Tweet tweet = newTweets[i];
                

                if (!moreTweets.Contains(tweet))
                {
                    moreTweets.Insert(0, tweet);
                    tweet.IsNew = true;
                }
                else
                {
                    // update the relativetime for existing tweets
                    moreTweets[i].UpdateRelativeTime();
                }
            }

            if (moreTweets.Count > 0)
                MoreListBox.SelectedIndex = 0;


            StopStoryboard("Fetching");
        }
    


        #endregion 

        #region Send messages

        private void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(MessageUserTextBox.Text) && !string.IsNullOrEmpty(MessageTextBox.Text))
            {
                // Schedule posting the tweet
                UpdateButton.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new SendMessageDelegate(SendMessage), MessageUserTextBox.Text, MessageTextBox.Text);
            }
        }

        private void SendMessage(string user, string messageText)
        {
            try
            {
                twitter.SendMessage(user, messageText);

                // Schedule the update function in the UI thread.
                LayoutRoot.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new NoArgDelegate(UpdateMessageUserInterface));
            }
            catch (WebException ex)
            {
                UpdateTextBlock.Text = "Message failed.";
                App.Logger.Debug(String.Format("There was a problem sending your message: {0}", ex.ToString()));
            }
            catch (ProxyAuthenticationRequiredException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show("Proxy server is configured incorrectly.  Please correct the settings on the Options menu.");
            }
            catch (ProxyNotFoundException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show(ex.Message);
            }

        }

        private void UpdateMessageUserInterface()
        {
            UpdateTextBlock.Text = "Send Message";
            StatusTextBlock.Text = "Message Sent!";
            PlayStoryboard("CollapseMessage");
            isMessageExpanded = false;
            MessageTextBox.Clear();

            UpdateExistingTweets();
        }

        private void Message_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ToggleMessage();
        }

        private void ToggleMessage()
        {
            if (isLoggedIn)
            {
                if (!isMessageExpanded)
                {
                    PlayStoryboard("ExpandMessage");
                    MessageTextBox.Focus();
                    isMessageExpanded = true;
                }
                else
                {
                    PlayStoryboard("CollapseMessage");
                    isMessageExpanded = false;
                }
            }
        }

        #endregion

        #region User Timeline

        public void DelegateUserTimelineFetch(string userId)
        {
            displayUser = userId;

            UserTab.IsSelected = true;
            UserContextMenu.IsEnabled = true;  // JMF

            userTweets.Clear();

            // Let the user know what's going on
            StatusTextBlock.Text = "Retrieving user's tweets...";

            PlayStoryboard("Fetching");

            // Create a Dispatcher to fetching new tweets
            LayoutRoot.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new OneStringArgDelegate(GetUserTimeline), userId);
        }

        private void GetUserTimeline(string userId)
        {
            try
            {
                // Schedule the update function in the UI thread.
                LayoutRoot.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new OneArgDelegate(UpdateUsersTimelineInterface), twitter.GetUserTimeline(userId));
            }

            // Jason Follas: Added the following UI feedback behavior for when users weren't found.
            catch (UserNotFoundException userNotFoundEx)
            {

                TweetCollection fakeTweets = new TweetCollection();
                fakeTweets.Add(new Tweet());
                fakeTweets[0].Id = -1;
                fakeTweets[0].Text = userNotFoundEx.Message;
                fakeTweets[0].Source = "Witty Error Handler";
                fakeTweets[0].User = new User();
                fakeTweets[0].User.ScreenName = "@" + userNotFoundEx.UserId;
                fakeTweets[0].User.Description = userNotFoundEx.Message;


                StopStoryboard("Fetching");

                this.UserContextMenu.IsEnabled = false;

                UpdateUsersTimelineInterface(fakeTweets);

            }
            catch (System.Security.SecurityException ex)
            {
                App.Logger.DebugFormat("User not allowed to get protected tweets from {0}. Exception details: {1}", userId, ex.ToString());
                StatusTextBlock.Text = userId + "'s updates are protected.";
                StopStoryboard("Fetching");
            }
            catch (WebException ex)
            {
                App.Logger.DebugFormat("There was a problem fetching the user's timeline from Twitter.com: {0}", ex.ToString());
            }
            catch (ProxyAuthenticationRequiredException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show("Proxy server is configured incorrectly.  Please correct the settings on the Options menu.");
            }
            catch (ProxyNotFoundException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show(ex.Message);
            }

        }

        /// <summary>
        /// Used to override closings and minimize instead
        /// </summary>
        private void OverrideClosing()
        {
            this.Closing += new CancelEventHandler(MainWindow_Closing);
        }

        private void UpdateUsersTimelineInterface(TweetCollection newTweets)
        {
            StatusTextBlock.Text = displayUser + "'s Timeline Updated: " + repliesLastUpdated.ToLongTimeString();
            User u = null;

            for (int i = newTweets.Count - 1; i >= 0; i--)
            {
                Tweet tweet = newTweets[i];
                u = tweet.User;

                if (!userTweets.Contains(tweet))
                {
                    userTweets.Insert(0, tweet);
                    tweet.IsNew = true;
                }
                else
                {
                    // update the relativetime for existing tweets
                    userTweets[i].UpdateRelativeTime();
                }
            }

            if (userTweets.Count > 0)
                UserTimelineListBox.SelectedIndex = 0;



            // JMF: There's an issue with binding where the wrong user's header is sometimes 
            // being displayed.  I think it is related to the fact that these header elements
            // are bound to a property found in each list item, and are probably picking up
            // the object from a previous list before the current list is updated (i.e., a
            // binding race condition?).

            // Manually setting header here as a workaround....
            if (u != null)
            {
                UserImage.Source = new ImageSourceConverter().ConvertFromString(u.ImageUrl) as ImageSource;
                FullName.Text = u.FullName;
                Description.Text = u.Description;
                SiteUrl.Text = u.SiteUrl;
                Location.Text = u.Location;
            }

            StopStoryboard("Fetching");
        }

        #endregion

        #region Login

        private void TryLogin()
        {
            try
            {
                // Schedule the update function in the UI thread.
                LayoutRoot.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new LoginDelegate(UpdatePostLoginInterface), twitter.Login());
            }
            catch (WebException ex)
            {
                App.Logger.Debug(String.Format("There was a problem logging in to Twitter: {0}", ex.ToString()));
            }
            catch (ProxyAuthenticationRequiredException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show("Proxy server is configured incorrectly.  Please correct the settings on the Options menu.");
                LayoutRoot.Dispatcher.BeginInvoke(DispatcherPriority.Background, new NoArgDelegate(UpdateLoginFailedInterface));
            }
            catch (ProxyNotFoundException ex)
            {
                App.Logger.Error("Incorrect proxy configuration.");
                MessageBox.Show(ex.Message);
                LayoutRoot.Dispatcher.BeginInvoke(DispatcherPriority.Background, new NoArgDelegate(UpdateLoginFailedInterface));
            }
        }

        private void UpdatePostLoginInterface(User user)
        {
            App.LoggedInUser = user;
            if (App.LoggedInUser != null)
            {
                isLoggedIn = true;
                RefreshButton.IsEnabled = true;
                OptionsButton.IsEnabled = true;
                FilterToggleButton.IsEnabled = true;
                AppSettings.LastUpdated = string.Empty;
                Filter.IsEnabled = true;

                DelegateRecentFetch();

                // Setup refresh timer
                refreshTimer.Interval = refreshInterval;
                refreshTimer.Tick += new EventHandler(Timer_Elapsed);
                refreshTimer.Start();

                // Setup friendslist timer for AutoSuggestPattern matching support
                SetupFriendsListTimer();
            }
            else
            {
                // login info from user settings is not valid, re-display the login screen.
                PlayStoryboard("ShowLogin");
            }
        }

        private void UpdateLoginFailedInterface()
        {
            isLoggedIn = false;
            OptionsButton.IsEnabled = true;
            PlayStoryboard("ShowLogin");

        }

        private void LoginControl_Login(object sender, RoutedEventArgs e)
        {
            // Jason Follas: Reworked Web Proxy - don't need to explicitly pass into TwitterNet ctor
            //twitter = new TwitterNet(AppSettings.Username, TwitterNet.DecryptString(AppSettings.Password), WebProxyHelper.GetConfiguredWebProxy());
            twitter = new TwitterNet(AppSettings.Username, TwitterNet.DecryptString(AppSettings.Password));

            // Jason Follas: Twitter proxy servers, anyone?  (Network Nazis who block twitter.com annoy me)
            twitter.TwitterServerUrl = AppSettings.TwitterHost;

            // fetch new tweets
            DelegateRecentFetch();

            // Setup refresh timer to get subsequent tweets
            refreshTimer.Interval = refreshInterval;
            refreshTimer.Tick += new EventHandler(Timer_Elapsed);
            refreshTimer.Start();

            // Setup friendslist timer for AutoSuggestPattern matching support
            SetupFriendsListTimer();

            PlayStoryboard("HideLogin");

            isExpanded = false;
            isLoggedIn = true;
            OptionsButton.IsEnabled = true;
            FilterToggleButton.IsEnabled = true;
            Filter.IsEnabled = true;
        }

        #endregion

        #region Misc Methods and Event Handlers

        /// <summary>
        /// This event is VERY important since it traps errors that happen unexpectedly.  Witty has been unstable
        /// due to the fact that there are actions in the API that don't account for the business rules.  So when 
        /// an action occurs, witty crashes.  This handler traps those errors and logs them.  
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Dispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            //TODO: Figure out a better option to do with these unhandled exceptions.  Maybe email them or something?
            App.Logger.Error("Unhandled exception occurred.", e.Exception);
#if DEBUG
            string error = String.Empty;
            if (e.Exception.InnerException != null)
            {
                error = e.Exception.InnerException.Message;
            }
            else
            {
                error = e.Exception.Message;
            }
            MessageBox.Show("An unhandled error occurred. See the log for details.\nError: " + error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
#endif
            e.Handled = true;
        }

        private void InitializeClickOnceTimer()
        {
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                // Initialize clickonce deployment
                _clickOnce = new Deployment(StatusTextBlock);
                _clickOnce.UpdateStartedEvent += new Deployment.UpdateStartedDelegate(clickOnce_UpdateStartedEvent);
                _clickOnce.UpdateCompletedEvent += new Deployment.UpdateCompletedDelegate(clickOnce_UpdateCompletedEvent);

                // initialize timer for click once updates
                _clickOnceUpdateTimer = new DispatcherTimer();
                _clickOnceUpdateTimer.Interval = new TimeSpan(0, 0, AppSettings.ClickOnceUpdateInterval);
                _clickOnceUpdateTimer.IsEnabled = true;
                _clickOnceUpdateTimer.Start();
                _clickOnceUpdateTimer.Tick += new EventHandler(_clickOnceUpdateTimer_Tick);
            }
        }

        private void InitializeSoundPlayer()
        {
            _player = new SoundPlayer(Witty.Properties.Resources.alert);
            _player.LoadAsync();
        }

        void _clickOnceUpdateTimer_Tick(object sender, EventArgs e)
        {
            StatusTextBlock.Text = "Starting update...";
            _clickOnce.UpdateApplication();
        }

        void clickOnce_UpdateCompletedEvent(bool restartApplication)
        {
            // restart the timeer
            _clickOnceUpdateTimer.Start();

            if (restartApplication)
            {
                System.Windows.Forms.Application.Restart();
                Application.Current.Shutdown();
            }
            else
            {
                StatusTextBlock.Text = "Last updated: " + AppSettings.LastUpdated;
            }
        }

        void clickOnce_UpdateStartedEvent()
        {
            StatusTextBlock.Text = "Update started...";
            _clickOnceUpdateTimer.Stop();
        }

        /// <summary>
        /// Checks for keyboard shortcuts
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">EventArgs</param>
        private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (isLoggedIn)
                {
                    switch (e.Key)
                    {
                        case Key.D:
                            CreateDirectMessage();
                            break;
                        case Key.U:
                            ToggleUpdate();
                            break;
                        case Key.R:
                            CreateReply();
                            break;
                        case Key.F:
                            createRetweet();
                            break;
                        case Key.O:
                            ShowOptions();
                            break;
                        case Key.D1:
                            //show the "Recent" tab
                            Tabs.SelectedIndex = 0;
                            break;
                        case Key.D2:
                            //show the "Replies" tab
                            Tabs.SelectedIndex = 1;
                            break;
                        case Key.D3:
                            //show the "Users" tab
                            Tabs.SelectedIndex = 2;
                            break;
                        case Key.D4:
                            //show the "Messages" tab
                            Tabs.SelectedIndex = 3;
                            break;
                    }
                }
                else
                {
                    if (e.Key == Key.Q) { App.Current.Shutdown(); };
                }
            }
            else
            {
                if (e.Key == Key.F5) { this.Refresh(); };

                if (e.Key == Key.Escape)
                {
                    this.WindowState = WindowState.Minimized;
                }
            }
        }

        private void NameClickedInTweet(object sender, RoutedEventArgs reArgs)
        {
            if (reArgs.OriginalSource is System.Windows.Documents.Hyperlink)
            {
                Hyperlink h = reArgs.OriginalSource as Hyperlink;

                string userId = h.Tag.ToString();
                DelegateUserTimelineFetch(userId);

                reArgs.Handled = true;
            }
        }

        private void HashtagClickedInTweet(object sender, RoutedEventArgs reArgs)
        {
            if (reArgs.OriginalSource is System.Windows.Documents.Hyperlink)
            {
                Hyperlink h = reArgs.OriginalSource as Hyperlink;

                string hashtag = h.Tag.ToString();
                string hashtagUrl = String.Format(Settings.Default.HashtagUrl, Uri.EscapeDataString(hashtag));

                try
                {
                    System.Diagnostics.Process.Start(hashtagUrl);
                }
                catch
                {
                    // TODO: Warn the user? Log the error? Do nothing since Witty itself is not affected?
                }

                reArgs.Handled = true;
            }
        }

        private void ShowStatus(string status)
        {
            StatusTextBlock.Text = status;
        }



        private void CreateDirectMessage()
        {
            Tweet selectedTweet = SelectedTweet as Tweet;
            if (null != selectedTweet)
            {
                CreateDirectMessage(selectedTweet.User.ScreenName);
            }
        }

        private void CreateDirectMessage(string screenName)
        {
            //Direct message to user
            if (!isExpanded)
            {
                this.Tabs.SelectedIndex = 0;
                ToggleUpdate();
            }
            TweetTextBox.Text = string.Empty;
            TweetTextBox.Text = "D ";

            TweetTextBox.Text += screenName;
            TweetTextBox.Text += " ";
            MoveTweetTextBoxCursorToEnd();
        }

        private void MoveTweetTextBoxCursorToEnd()
        {
                    TweetTextBox.Select(TweetTextBox.Text.Length, 0);
        }

        private void CreateReply()
        {
            //reply to user
            if (null != SelectedTweet)
            {
                CreateReply(SelectedTweet.User.ScreenName);
            }
        }

        private void CreateReply(string screenName)
        {
            if (!isExpanded)
            {
                this.Tabs.SelectedIndex = 0;
                ToggleUpdate();
            }
            isReplyMessage = true;
            TweetTextBox.Text = string.Empty;
            if(!string.IsNullOrEmpty(AppSettings.ReplyPrefix)) 
                TweetTextBox.Text = AppSettings.ReplyPrefix + " ";
            TweetTextBox.Text += "@" + screenName + " ";
            TweetTextBox.Select(TweetTextBox.Text.Length, 0);
        }

        private void CreateReplyAll()
        {
            List<string> screenNames = new List<string>();

            //reply to users
            if (null != SelectedTweet)
            {                
                screenNames.Add(SelectedTweet.User.ScreenName);

                string[] words = SelectedTweet.Text.Split(' ');

                foreach (string word in words)
                {
                    if (word.StartsWith("@"))
                    {
                        screenNames.Add(word.Replace("@", ""));           
                    }
                }

                CreateReplyAll(screenNames);
            }            

        }

        private void CreateReplyAll(List<string> screenNames)
        {
            if (!isExpanded)
            {
                this.Tabs.SelectedIndex = 0;
                ToggleUpdate();
            }
            isReplyMessage = true;
            TweetTextBox.Text = string.Empty;
            if (!string.IsNullOrEmpty(AppSettings.ReplyPrefix))
                TweetTextBox.Text = AppSettings.ReplyPrefix + " ";

            foreach (string screenName in screenNames)
            {
                if (App.LoggedInUser.ScreenName == screenName)
                {
                    continue;
                }
                TweetTextBox.Text += "@" + screenName + " ";
            }

            TweetTextBox.Select(TweetTextBox.Text.Length, 0);
        }

        private void DeleteTweet()
        {
            if (null != SelectedTweet)
            {
                DeleteTweet(SelectedTweet.Id);
            }
        }

        private void DeleteTweet(double id)
        {

            /* By: Keith Elder
             * You can only destroy a tweet if you are the one that created it
             * or if it is a direct message to you.  This is causing exceptions.
             */
            if (SelectedTweet.User.ScreenName == Settings.Default.Username)
            {
                if (MessageBoxResult.Yes == MessageBox.Show("Are you sure you want to permanently delete your tweet?\nThis action is irreversible. Select No to only delete it from the application or Yes to delete permanently.", Settings.Default.ApplicationName, MessageBoxButton.YesNo, MessageBoxImage.Question))
                {
                    LayoutRoot.Dispatcher.BeginInvoke(
                                    DispatcherPriority.Background,
                                    new DeleteTweetDelegate(twitter.DestroyTweet), id);
                }
                if (tweets.Contains(SelectedTweet))
                    tweets.Remove(SelectedTweet);
                else if (replies.Contains(SelectedTweet))
                    replies.Remove(SelectedTweet);

                if (userTweets.Contains(SelectedTweet))
                    userTweets.Remove(SelectedTweet);
            }
        }

        private void FollowUser()
        {
            if (null != SelectedTweet)
            {
                FollowUser(SelectedTweet.User.ScreenName);
            }
        }

        private void FollowUser(string username)
        {
            LayoutRoot.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                new OneStringArgDelegate(twitter.FollowUser), username);
            DispatchFriendsList();
        }

        private void DeleteDirectMessage()
        {
            DirectMessage message = MessagesListBox.SelectedItem as DirectMessage;
            if (message != null)
            {
                DeleteDirectMessage(message.Id);
                if (messages.Contains(message))
                {
                    messages.Remove(message);
                }
            }
        }

        private void DeleteDirectMessage(double id)
        {
            twitter.DestroyDirectMessage(id);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            this.Refresh();
        }

        private void Refresh()
        {
            switch (currentView)
            {
                case CurrentView.Recent:
                    DelegateRecentFetch();

                    // After a manual refresh, reset the timer
                    refreshTimer.Stop();
                    refreshTimer.Start();

                    break;
                case CurrentView.Replies:
                    DelegateRepliesFetch();
                    break;
                case CurrentView.Messages:
                    DelegateMessagesFetch();
                    break;
                case CurrentView.User:
                    DelegateUserTimelineFetch(displayUser);
                    break;
                case CurrentView.More:
                    switch (moreResults){
                        case MoreResults.Conversation:
                            DelegateConversationFetch(conversationStartId);
                            break;
                    }
                    break;


            }
        }

        #region Clear Methods

        internal void ClearTweets()
        {
            tweets.Clear();
        }

        internal void ClearReplies()
        {
            replies.Clear();
        }

        private void Clear()
        {
            switch (currentView)
            {
                case CurrentView.Recent:
                    ClearTweets();
                    break;
                case CurrentView.Replies:
                    ClearReplies();
                    break;
            }
        }
        #endregion

        private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabControl tabs = (TabControl)sender;

            //Selection changed events bubble, which is rarely expected
            if (e.Source != Tabs)
                return;

            if (tabs.SelectedIndex == 0)
            {
                displayUser = string.Empty;
                ShowStatus(string.Empty);

                //Scroll to the top of recent list
                if (e.Source == Tabs && TweetsListBox.Items.Count > 0)
                {
                    //Scroll to top
                    TweetsListBox.SelectedIndex = 0;
                    UpdateLayout();
                    TweetsListBox.ScrollIntoView(TweetsListBox.SelectedItem);
                }
            }

            if (tabs.SelectedIndex == 1 && isLoggedIn)
            {
                // limit updating replies to no more than once a minute
                long ticks = DateTime.Now.Ticks - repliesLastUpdated.Ticks;
                TimeSpan ts = new TimeSpan(ticks);
                if (ts.TotalMinutes > 1)
                {
                    DelegateRepliesFetch();
                }

                displayUser = string.Empty;
            }

            if (tabs.SelectedIndex == 2 && string.IsNullOrEmpty(displayUser))
            {
                DelegateUserTimelineFetch(AppSettings.Username);
            }

            if (tabs.SelectedIndex == 3 && isLoggedIn)
            {
                // limit updating replies to no more than once a minute
                long ticks = DateTime.Now.Ticks - messagesLastUpdated.Ticks;
                TimeSpan ts = new TimeSpan(ticks);
                if (ts.TotalMinutes > 1)
                {
                    DelegateMessagesFetch();
                }

                displayUser = string.Empty;
            }
            if (tabs.SelectedIndex == 4 && isLoggedIn)
            {
                // limit updating replies to no more than once a minute
                long ticks = DateTime.Now.Ticks - repliesLastUpdated.Ticks;
                TimeSpan ts = new TimeSpan(ticks);
                if (ts.TotalMinutes > 1)
                {
                    DelegateConversationFetch(conversationStartId);
                }

                displayUser = string.Empty;
            }
            // clear the filter text since it isn't applied when switching tabs
            FilterTextBox.Text = string.Empty;
        }

        private void PlayStoryboard(string storyboardName)
        {
            Object o = TryFindResource(storyboardName);
            if (o != null)
            {
                Storyboard storyboard = (Storyboard)o;
                storyboard.Begin(this);
            }
        }

        private void StopStoryboard(string storyboardName)
        {
            Object o = TryFindResource(storyboardName);
            if (o != null)
            {
                Storyboard storyboard = (Storyboard)o;
                storyboard.Stop(this);
            }
        }

        private void TweetsListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.MouseDevice.DirectlyOver != null && e.MouseDevice.DirectlyOver.GetType() == typeof(TextBlock))
            {
                TextBlock textBlock = (TextBlock)e.MouseDevice.DirectlyOver;

                try
                {
                    ListBox listbox = (ListBox)sender;

                    if (textBlock.Name == "ScreenName")
                    {
                        if (listbox.SelectedItem != null && currentView != CurrentView.User)
                        {
                            Tweet tweet = (Tweet)listbox.SelectedItem;
                            //System.Diagnostics.Process.Start(tweet.User.TwitterUrl);
                            DelegateUserTimelineFetch(tweet.User.ScreenName);
                        }
                    }
                }
                catch (Win32Exception ex)
                {
                    App.Logger.Debug(String.Format("Exception: {0}", ex.ToString()));
                }
            }
        }




        private void MoreTab_LostFocus(object sender, RoutedEventArgs e)
        {
            TabItem tab = (TabItem)sender;
            tab.IsEnabled = false;
            tab.Header = "More..";
        }


        private void UserTimelineListBox_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            AddConversationMenuItem(sender);
        }

        private void RepliesListBox_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            AddConversationMenuItem(sender);
        }

        private void TweetsListBox_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
           AddConversationMenuItem(sender);
        }
        private void MessagesListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.MouseDevice.DirectlyOver.GetType() == typeof(TextBlock))
            {
                TextBlock textBlock = (TextBlock)e.MouseDevice.DirectlyOver;

                try
                {
                    ListBox listbox = (ListBox)sender;

                    if (textBlock.Name == "ScreenName")
                    {
                        if (listbox.SelectedItem != null && currentView != CurrentView.User)
                        {
                            DirectMessage tweet = (DirectMessage)listbox.SelectedItem;

                            ToggleMessage();
                            MessageUserTextBox.Text = textBlock.Text;
                            MessageTextBox.Focus();
                        }
                    }
                }
                catch (Win32Exception ex)
                {
                    App.Logger.Debug(String.Format("Exception: {0}", ex.ToString()));
                }
            }
        }

        void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            if (((MenuItem)sender).IsChecked)
                this.Topmost = true;
            else
                this.Topmost = false;

            AppSettings.AlwaysOnTop = this.Topmost;
            AppSettings.Save();
        }

        private void LaunchUrlIfValid(string candidateUrlString)
        {
            if (Uri.IsWellFormedUriString(candidateUrlString, UriKind.Absolute))
            {
                try
                {
                    System.Diagnostics.Process.Start(candidateUrlString);
                }
                catch (Win32Exception ex)
                {
                    App.Logger.Debug(String.Format("Exception: {0}", ex.ToString()));
                }
            }
        }
        private void Url_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TextBlock textBlock = (TextBlock)sender;
            LaunchUrlIfValid(textBlock.Text);
        }

        private void ScreenName_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TextBlock textBlock = (TextBlock)sender;
            LaunchUrlIfValid(textBlock.Tag.ToString());
        }

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowOptions();
        }

        private void ConsoleButton_Click(object sender, RoutedEventArgs e)
        {
            var console = new ConsoleWindow();
            console.MainRepl.AddExternalObject("T", twitter);
            console.MainRepl.AddExternalObject("U", this);
            console.Show();
        }

        private void ShowOptions()
        {
            Options options = new Options();

            Binding binding = new Binding();
            binding.Path = new PropertyPath("Topmost");
            binding.Source = this;
            binding.Mode = BindingMode.TwoWay;
            options.SetBinding(Options.TopmostProperty, binding);
            options.Owner = this;

            bool? dialogResult = options.ShowDialog();
            switch (dialogResult)
            {
                case true:
                    // User wants to save options

                    // update the refresh interval
                    int minutes = int.Parse(AppSettings.RefreshInterval);

                    refreshTimer.Stop();
                    if (minutes > 0)
                    {
                        refreshInterval = new TimeSpan(0, minutes, 0);
                        refreshTimer.Interval = refreshInterval;
                        refreshTimer.Start();
                    }

                    // Set (unset?) the web proxy after options have been saved
                    if (twitter != null)
                        twitter.WebProxy = WebProxyHelper.GetConfiguredWebProxy();


                    StatusTextBlock.Text = "Options Updated";

                    break;
                case false:
                    break;
                default:
                    // Indeterminate, do nothing
                    break;
            }
            if (string.IsNullOrEmpty(AppSettings.Username))
            {
                // User wants to logout
                isLoggedIn = false;
                tweets.Clear();
                StatusTextBlock.Text = "Login";
                Filter.IsEnabled = false;

                PlayStoryboard("ShowLogin");
            }
        }

        #region Context menu event handlers

        private void ContextMenuReply_Click(object sender, RoutedEventArgs e)
        {
            CreateReply();
        }

        private void ContextMenuReplyAll_Click(object sender, RoutedEventArgs e)
        {
            CreateReplyAll();
        }

        private void ContextMenuRetweet_Click(object sender, RoutedEventArgs e)
        {
            createRetweet();
        }

        private void ContextMenuIgnore_Click(object sender, RoutedEventArgs e)
        {
            IgnoreUser(60);
            UpdateUserInterface(tweets);
        }
        private void ContextMenuConversation_Click(object sender, RoutedEventArgs e)
        {
            ViewConversation();
        }
        private void IgnoreUser(int ignoreTime)
        {
            Tweet selectedTweet = SelectedTweet as Tweet;
            if (selectedTweet != null)
            {
                ignoredUsers[selectedTweet.User.ScreenName] = DateTime.Now.AddMinutes(ignoreTime);
            }
        }

        private void createRetweet()
        {
            Tweet selectedTweet = SelectedTweet as Tweet;
            if (selectedTweet != null)
            {
                if (!isExpanded)
                {
                    this.Tabs.SelectedIndex = 0;
                    ToggleUpdate();
                }
                string message = string.Format("{0} @{1}: {2}", AppSettings.RetweetPrefix, selectedTweet.User.ScreenName, selectedTweet.Text);
                message = TruncateMessage(message);
                TweetTextBox.Text = message;
                TweetTextBox.Select(TweetTextBox.Text.Length, 0);
            }

        }

        private void AddConversationMenuItem(object sender)
        {
               try
            {
                ListBox listbox = (ListBox)sender;
                if (listbox.SelectedItem != null && currentView == CurrentView.Recent)
                {
                    Tweet tweet = (Tweet)listbox.SelectedItem;
                    MenuItem menuItem = (MenuItem)listbox.ContextMenu.Items.GetItemAt(5);

                    if (tweet.InReplyTo != null)
                    {
                        menuItem.Visibility = Visibility.Visible;
                       
                     }
                    else
                    {
                        menuItem.Visibility = Visibility.Collapsed;
                        
                    }
                    
                }

            }
            catch (Win32Exception ex)
            {
                App.Logger.Debug(String.Format("Exception: {0}", ex.ToString()));
            }
        }

        private void ContextMenuDeleteMessage_Click(object sender, RoutedEventArgs e)
        {
            DeleteDirectMessage();
        }


        private void ContextMenuDirectMessage_Click(object sender, RoutedEventArgs e)
        {
            CreateDirectMessage();
        }

        private void ContextMenuFollow_Click(object sender, RoutedEventArgs e)
        {
            FollowUser();
        }

        private void ContextMenuDelete_Click(object sender, RoutedEventArgs e)
        {
            DeleteTweet();
        }

        private void ContextMenuClear_Click(object sender, RoutedEventArgs e)
        {
            Clear();
        }

        #endregion

        #region Popup Event Handlers

        private void RemovePopup(Popup popup)
        {
            popupCount--;
            popup.Close();
            popup = null;
        }

        private void PopupReplyClicked(string screenName)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                Show();
                WindowState = _storedWindowState;
            }
            CreateReply(screenName);
        }

        private void PopupDirectMessageClicked(string screenName)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                Show();
                WindowState = _storedWindowState;
            }
            CreateDirectMessage(screenName);
        }

        void PopupClicked(Tweet tweet)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                Show();
                WindowState = _storedWindowState;
            }

            if (tweet != null)
            {
                TweetsListBox.ScrollIntoView(tweet);
            }
        }

        #endregion

        #endregion

        #region Filter

        // Delegate for performing filter in background thread for performance improvements
        private delegate void FilterDelegate();

        /// <summary>
        /// Handles the filtering
        /// </summary>
        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Start an async operation that filters the list.
            this.Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new FilterDelegate(FilterWorker));
        }

        /// <summary>
        /// Worker method that filters the list.
        /// </summary>
        private void FilterWorker()
        {
            //Use collection view to filter the listbox
            ICollectionView collectionView = CollectionViewSource.GetDefaultView(tweets);

            switch (currentView)
            {
                case CurrentView.Recent:
                    collectionView = CollectionViewSource.GetDefaultView(tweets);
                    break;
                case CurrentView.Replies:
                    collectionView = CollectionViewSource.GetDefaultView(replies);
                    break;
                case CurrentView.Messages:
                    collectionView = CollectionViewSource.GetDefaultView(messages);
                    break;
                case CurrentView.User:
                    collectionView = CollectionViewSource.GetDefaultView(userTweets);
                    break;
                default:
                    collectionView = CollectionViewSource.GetDefaultView(tweets);
                    break;
            }

            if (currentView == CurrentView.Messages)
                // messages aren't tweets
                collectionView.Filter = new Predicate<object>(MessageFilter);
            else
                collectionView.Filter = new Predicate<object>(TweetFilter);
        }

        /// <summary>
        /// Delegate to filter the tweet text and by the tweet user's screenname.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool TweetFilter(object item)
        {
            Tweet tweet = item as Tweet;

            // this will prevent the fade animation from starting when the tweet is filtered
            tweet.IsNew = false;

            return (tweet.Text.ToLower().Contains(FilterTextBox.Text.ToLower()))
                   || (tweet.User.ScreenName.ToLower().Contains(FilterTextBox.Text.ToLower()));
        }

        /// <summary>
        /// Delegate to filter the tweet text and by the tweet user's screenname.
        /// </summary>
        public bool MessageFilter(object item)
        {
            DirectMessage message = item as DirectMessage;
            return (message.Text.ToLower().Contains(FilterTextBox.Text.ToLower()))
                   || (message.Sender.ScreenName.ToLower().Contains(FilterTextBox.Text.ToLower()));
        }

        #endregion

        #region Minimize to Tray

        private System.Windows.Forms.NotifyIcon _notifyIcon;

        void OnClosed(object sender, EventArgs e)
        {
            if (!AppSettings.PersistLogin)
            {
                AppSettings.Username = string.Empty;
                AppSettings.Password = string.Empty;
                AppSettings.Save();
            }

            _notifyIcon.Dispose();
            _notifyIcon = null;

            if (SnarlConnector.GetSnarlWindow().ToInt32() != 0 && this.SnarlConfighWnd != null)
            {
                SnarlConnector.RevokeConfig(this.SnarlConfighWnd);
            }
            if (this.SnarlConfighWnd != null && snarlComWindow != null)
            {
                snarlComWindow.DestroyHandle();
            }
        }

        private WindowState _storedWindowState = WindowState.Normal;

        DispatcherTimer hideTimer = new DispatcherTimer();

        void OnStateChanged(object sender, EventArgs args)
        {
            if (AppSettings.MinimizeToTray)
            {
                if (WindowState == WindowState.Minimized)
                {
                    hideTimer.Interval = new TimeSpan(500);
                    hideTimer.Tick += new EventHandler(HideTimer_Elapsed);
                    hideTimer.Start();
                }
                else
                {
                    _storedWindowState = WindowState;
                }
            }
        }

        private void HideTimer_Elapsed(object sender, EventArgs e)
        {
            this.Hide();
            hideTimer.Stop();
        }

        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            CheckTrayIcon();
        }

        void m_notifyIcon_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = _storedWindowState;
        }

        void CheckTrayIcon()
        {
            ShowTrayIcon(!IsVisible);
        }

        void ShowTrayIcon(bool show)
        {
            if (_notifyIcon != null)
                _notifyIcon.Visible = show;
        }

        void openMenuItem_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = _storedWindowState;
        }

        void exitMenuItem_Click(object sender, EventArgs e)
        {
            this.reallyexit = true;
            this.Close();
        }

        #endregion

        #region Single Instance
        SingleInstanceManager _instanceManager;

        public void ShowApplication()
        {
            if (this.Visibility == Visibility.Hidden)
            {
                this.Visibility = Visibility.Visible;
            }
        }

        void Current_Exit(object sender, ExitEventArgs e)
        {
            Environment.Exit(0);
        }

        #endregion

        #region Filter and Search

        private void TweetScanButton_Click(object sender, RoutedEventArgs e)
        {
            TweetScanHelper ts = new TweetScanHelper();
            TweetCollection searchResults = ts.GetSearchResults(FilterTextBox.Text);

            // TODO: this should be displayed somewhere else instead of the main tweets listbox.
            for (int i = searchResults.Count - 1; i >= 0; i--)
            {
                Tweet tweet = searchResults[i];
                if (!tweets.Contains(tweet))
                {
                    tweets.Insert(0, tweet);
                    tweet.Index = tweets.Count;
                    tweet.IsNew = true;
                    tweet.IsSearchResult = true;
                }
            }
        }

        private void FilterToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            //TODO: fix this
            //TODO: need to remove the search tweets from the main list.
            //foreach (Tweet t in tweets)
            //{
            //    if (t.IsSearchResult)
            //    {
            //        tweets.Remove(t);
            //    }
            //}
        }

        #endregion

        #region Autocomplete Friends
        private void UpdateFriendsList()
        {
            if (isLoggedIn)
            {
                friends = twitter.GetFriends() ?? friends;
                lastFriendsUpdate = DateTime.Now;
            }
        }

        private void SetupFriendsListTimer()
        {
            friendsRefreshTimer.Interval = new TimeSpan(0,0,5);
            friendsRefreshTimer.IsEnabled = true;
            friendsRefreshTimer.Start();
            friendsRefreshTimer.Tick += new EventHandler(friendsRefreshTimer_Tick);
        }

        private void DispatchFriendsList()
        {
            new NoArgDelegate(this.UpdateFriendsList).BeginInvoke(null, null);
        }

        void friendsRefreshTimer_Tick(object sender, EventArgs e)
        {
            friendsRefreshTimer.Interval = friendsRefreshInterval;
            DispatchFriendsList();
        }

        private void TweetTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(Key.Escape == e.Key)
            {
                CancelTweet();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && Key.Space == e.Key)
            {
                isInAutocompleteMode = true;
                Suggest(TweetTextBox, AutoSuggestPattern, 0);
                e.Handled = true;
            }

            if (!isInAutocompleteMode) return;

            ShowStatus(string.Empty);

            if (Key.Tab == e.Key)
            {
                ignoreKey = true;
                TweetTextBox.Select(TweetTextBox.Text.Length, 0);
                TweetTextBox.Text += " ";
                e.Handled = true;
                TweetTextBox.Select(TweetTextBox.Text.Length, 0);
            } 
            //HACK: need to ignore the keypress for backspace
            else if (Key.Back == e.Key)
            {
                ignoreKey = true;
            }
            else if (Key.Up == e.Key)
            {
                Suggest(TweetTextBox, AutoSuggestPattern, -1);
                e.Handled = true;
            }
            else if (Key.Down == e.Key)
            {
                Suggest(TweetTextBox, AutoSuggestPattern, 1);
                e.Handled = true;
            }
            else
            {
                ignoreKey = false;
            }
        }

        private void TweetTextBox_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(typeof(String))) return;
            string pastedText = ((String)e.DataObject.GetData(typeof(String)));

            // If a URL was pasted and we're over the limit, shorten it
            if (UrlShorteningService.IsUrl(pastedText) && (TweetTextBox.Text.Length + pastedText.Length) > TwitterNet.CharacterLimit)
            {
                if(AttemptTinyURLFormatting(TweetTextBox, TweetTextBox.Text + pastedText))
                    e.CancelCommand();
            }
        }

        static Regex AutoSuggestPattern = new Regex(@"(^.*@|^d )(\w*)$");

        private void TweetTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Suggest(TweetTextBox, AutoSuggestPattern, 0);

            if (TweetTextBox.Text.EndsWith(" ") && TweetTextBox.Text.Length > TwitterNet.CharacterLimit)
            {
                // bgriswold: This routine is executed after a trailing space is entered or pasted. 
                // This implies that a URL which is entered as the final character string will not be reformatted. 
                // It can be assumed, however, that users will quickly come to realized that adding a space 
                // after URLs will trigger reformatting and they will begin doing it intuitively.  
                // Enough rambling about a minor point...
                AttemptTinyURLFormatting(TweetTextBox, null);
            }
        }
        
        private void Suggest(TextBox textBox, Regex matchAndReplace, int offset)
        {
            if (ignoreKey || friends == null) return;

            List<String> currentFriends = new List<string>();
            string selectedText = string.Empty;
            Match matchedText = Match.Empty;
            if (isInAutocompleteMode)
            {
                ignoreKey = true;
                selectedText = textBox.SelectedText;
                textBox.SelectedText = string.Empty;
                ignoreKey = false;
            }

            int length = textBox.Text.Length;
            isInAutocompleteMode = matchAndReplace.IsMatch(textBox.Text);
            if (!isInAutocompleteMode)
            {
                ShowStatus(string.Empty);
                return;
            }

            matchedText = matchAndReplace.Match(textBox.Text);
            int selectedIndex = 0;
            if (matchedText.Success)
            {
                string userEnteredText = matchedText.Groups[2].Value;
                {
                    foreach (User friend in friends)
                    {
                        if (friend.ScreenName.StartsWith(userEnteredText, StringComparison.CurrentCultureIgnoreCase) || userEnteredText.Length == 0)
                            currentFriends.Add(friend.ScreenName);
                    }
                }
                if (currentFriends.Count != 0)
                {
                    currentFriends.Sort();

                    selectedIndex = currentFriends.IndexOf(userEnteredText + selectedText);
                    if (selectedIndex < 0) selectedIndex = 0;
                    selectedIndex += offset;
                    if (selectedIndex < 0) selectedIndex = currentFriends.Count - 1;
                    else if (selectedIndex > (currentFriends.Count - 1)) selectedIndex = 0;

                    ignoreKey = true;
                    textBox.Text = matchAndReplace.Replace(textBox.Text, String.Format("${{1}}{0}", currentFriends[selectedIndex]));
                    textBox.Select(length, textBox.Text.Length - length);
                    ignoreKey = false;

                    ShowStatus(string.Format("{0} matching - [Up/Down] to cycle, [Tab] to select",currentFriends.Count));
                }
            }
        }

        public bool AttemptTinyURLFormatting(TextBox textBox, string tweetText)
        {
            if(string.IsNullOrEmpty(tweetText))
                tweetText = textBox.Text;

            if (tweetText.Length < TwitterNet.CharacterLimit) return false;

            ParseTextHereAndTinyUpAnyURLsFound(ref tweetText);

            if (tweetText == textBox.Text) return false;
            
                textBox.Text = tweetText;
                textBox.SelectionStart = textBox.Text.Length;
            
                return true;
            }
        #endregion

        private void Recent_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFilePaths =
                e.Data.GetData(DataFormats.FileDrop, true) as string[];

                foreach (string droppedFilePath in droppedFilePaths)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Threading.ThreadStart(() =>
                    {
                        twitter.PostPhoto(new System.IO.FileInfo(droppedFilePath), TweetTextBox.Text);
                    }));
                    
                    ShowStatus(System.IO.Path.GetFileNameWithoutExtension(droppedFilePath));
                }
            }
        }

        private void Recent_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.All;
            e.Handled = true;
        }
    }
}
