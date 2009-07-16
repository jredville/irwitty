using System.Windows;
using TwitterLib;
using TwitterLib.Utilities;

namespace WittyTestBed
{
    /// <summary>
    /// Interaction logic for tweetscan.xaml
    /// </summary>
    public partial class tweetscan : Window
    {
        TweetCollection tweets = new TweetCollection();

        public tweetscan()
        {
            InitializeComponent();

            TweetsListBox.ItemsSource = tweets;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            TweetScanHelper ts = new TweetScanHelper();
            TweetCollection searchREsults = ts.GetSearchResults(SearchTextBox.Text);

            foreach (Tweet t in searchREsults)
            {
                tweets.Add(t);
            }
        }
    }
}
