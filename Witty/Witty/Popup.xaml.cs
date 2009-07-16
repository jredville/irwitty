using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TwitterLib;

namespace Witty
{
    public delegate void FadeOutFinishedDelegate(Popup p);
    public delegate void PopupReplyClickedDelegate(string screenName);
    public delegate void PopupDirectMessageClickedDelegate(string screenName);
    public delegate void PopupCloseButtonClickedDelegate(Popup p);
    public delegate void PopupClickedDelegate(Tweet tweet);

    /// <summary>
    /// Interaction logic for Popup.xaml
    /// </summary>
    public partial class Popup : Window
    {
        private Tweet _tweet;
        private Storyboard sbFadeOut;
        private Storyboard ShowPopup;        
        private TimeSpan ts = new TimeSpan();
        public event FadeOutFinishedDelegate FadeOutFinished;
        public event PopupReplyClickedDelegate ReplyClicked;
        public event PopupDirectMessageClickedDelegate DirectMessageClicked;
        public event PopupCloseButtonClickedDelegate CloseButtonClicked;
        public event PopupClickedDelegate Clicked;

        public Popup(Tweet tweet, Int32 numPopups) : this(tweet.User.ScreenName, tweet.Text, tweet.User.ImageUrl, numPopups)
        {
            _tweet = tweet;             
        }

        public Popup(String heading, String body, String imageSource, Int32 numPopups)
        {
            InitializeComponent();

            this.Left = SystemParameters.WorkArea.Width - this.Width;
            this.Top = SystemParameters.WorkArea.Height - (this.Height * (numPopups + 1));

            tweetText.Text = body;
            userName.Text = heading;

            ImageSourceConverter conv = new ImageSourceConverter();
            avatarImage.Source = (ImageSource)conv.ConvertFromString(imageSource);

            this.Topmost = true;

            sbFadeOut = (Storyboard)FindResource("sbFadeOut");            
            sbFadeOut.Completed += new EventHandler(sbFadeOut_Completed);

            DoubleAnimationUsingKeyFrames animation = new DoubleAnimationUsingKeyFrames();
            Storyboard.SetTargetName(animation, "MainGrid");
            Storyboard.SetTargetProperty(animation, new PropertyPath(UIElement.OpacityProperty));            

            animation.KeyFrames.Add(new SplineDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(Double.Parse(Properties.Settings.Default.NotificationDisplayTime)))));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(Double.Parse(Properties.Settings.Default.NotificationDisplayTime) + 1))));

            sbFadeOut.Children.Add(animation);

            ShowPopup = (Storyboard)FindResource("ShowPopup");
            ShowPopup.Completed += new EventHandler(ShowPopup_Completed);
            ShowPopup.Begin(this, true);
        }

        void ShowPopup_Completed(object sender, EventArgs e)
        {
            sbFadeOut.Begin(this, true);
        }

        void sbFadeOut_Completed(object sender, EventArgs e)
        {
            if (FadeOutFinished != null)
                FadeOutFinished(this);
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                ts = (TimeSpan)sbFadeOut.GetCurrentTime(this);
                sbFadeOut.Stop(this);
                this.Opacity = 1;
            }
            catch (Exception ex)
            {
                App.Logger.Error("Popup SNARL Error", ex);
            }
        }

        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            sbFadeOut.Begin(this, true);
            sbFadeOut.Seek(this, ts, TimeSeekOrigin.BeginTime);
        }

        private void ContextMenuReply_Click(object sender, RoutedEventArgs e)
        {
            ReplyClicked(userName.Text);
        }

        private void ContextMenuDirectMessageClick_Click(object sender, RoutedEventArgs e)
        {
            DirectMessageClicked(userName.Text);
        }        

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            CloseButtonClicked(this);
        }

        private void MainGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Clicked(_tweet);
        }        
    }
}
