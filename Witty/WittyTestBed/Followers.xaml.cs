using System.Windows;
using TwitterLib;

namespace WittyTestBed
{
    /// <summary>
    /// Interaction logic for Followers.xaml
    /// </summary>
    public partial class Followers : Window
    {
        public Followers()
        {
            InitializeComponent();

            TwitterNet twitter = new TwitterNet("WittyTest", TwitterNet.ToSecureString("WittyTest"));
            FriendsListBox.ItemsSource = twitter.GetFriends();
        }
    }
}
