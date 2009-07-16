using System.Windows;

namespace WittyTestBed
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void TweetScanButton_Click(object sender, RoutedEventArgs e)
        {
            tweetscan ts = new tweetscan();
            ts.Show();
        }

        private void CustomWindowButton_Click(object sender, RoutedEventArgs e)
        {
            CustomWindow cw = new CustomWindow();
            cw.Show();
        }

        private void FollowersButton_Click(object sender, RoutedEventArgs e)
        {
            Followers f = new Followers();
            f.Show();
        }
    }
}
