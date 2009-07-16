using NUnit.Framework;
using TwitterLib;

namespace WittyUnitTests
{
    [TestFixture]
    public class TweetSplitterTest
    {
        [SetUp]
        public void Init()
        {

        }

        [Test]
        public void TweetLessThan140CharactersIsUnchanged()
        {
            string tweetText = "This is a tweet less than 140 characters.";

            string[] statuses = TweetSplitter.SplitTweet(tweetText);

            foreach (string status in statuses)
            {
                Assert.LessOrEqual(status.Length, 140, "Tweet length greater than 140 characters.");
            }

            Assert.AreEqual(statuses.Length, 1);
            Assert.AreEqual(statuses[0], tweetText);
        }

        [Test]
        public void TweetEqualTo140CharactersIsUnchanged()
        {
            string tweetText = "123456789 1234567890 23456 8901234567 9012 4567890 23456789012 456789012345678 0123 5678 0123456 8901234 67890123 567 90123 567890 234567890";

            string[] statuses = TweetSplitter.SplitTweet(tweetText);

            foreach (string status in statuses)
            {
                Assert.LessOrEqual(status.Length, 140, "Tweet length greater than 140 characters.");
            }

            Assert.AreEqual(1, statuses.Length);
            Assert.AreEqual(statuses[0], tweetText);
        }

        [Test]
        public void TweetGreaterThan140CharactersAndLessThan280CharactersIsSplitInTwo()
        {
            string tweetText = "This is a tweet more than 140 characters and less than 280 characters. This is a tweet more than 140 characters and less than 280 characters. This is a tweet more than 140 characters and less than 280 characters.";
            string tweet1 = "This is a tweet more than 140 characters and less than 280 characters. This is a tweet more than 140 characters and less than 280...";
            string tweet2 = "...characters. This is a tweet more than 140 characters and less than 280 characters.";
            
            string[] statuses = TweetSplitter.SplitTweet(tweetText);

            foreach (string status in statuses)
            {
                Assert.LessOrEqual(status.Length, 140, "Tweet length greater than 140 characters.");
            }
            
            Assert.AreEqual(statuses.Length, 2);
            Assert.AreEqual(statuses[0], tweet1);
            Assert.AreEqual(statuses[1], tweet2);
        }

        [Test]
        public void ReplyLessThan140CharactersIsUnchanged()
        {
            string prefix = "@bengriswold ";
            string tweetText = prefix + "This is a tweet less than 140 characters.";
            string[] statuses = TweetSplitter.SplitTweet(tweetText);

            foreach (string status in statuses)
            {
                Assert.LessOrEqual(status.Length, 140, "Tweet length greater than 140 characters.");
                Assert.IsTrue(status.StartsWith(prefix));
            }
            
            Assert.AreEqual(1, statuses.Length);
            Assert.AreEqual(tweetText, statuses[0]);
        }

        [Test]
        public void DoubleReplyLessThan140CharactersIsUnchanged()
        {
            string prefix = "@bengriswold @jongalloway ";
            string tweetText = prefix + "This is a tweet less than 140 characters.";
            string[] statuses = TweetSplitter.SplitTweet(tweetText);

            foreach (string status in statuses)
            {
                Assert.LessOrEqual(status.Length, 140, "Tweet length greater than 140 characters.");
                Assert.IsTrue(status.StartsWith(prefix));
            }

            Assert.AreEqual(1, statuses.Length);
            Assert.AreEqual(tweetText, statuses[0]);
        }
        
        [Test]
        public void ReplyGreaterThan140CharactersAndLessThan280CharactersIsSplitInTwo()
        {
            string prefix = "@bengriswold ";
            string tweetText = prefix + "This is a tweet more than 140 characters and less than 280 characters. This is a tweet more than 140 characters and less than 280 characters. This is a tweet more than 140 characters and less than 280 characters.";
            string tweet1 = prefix + "This is a tweet more than 140 characters and less than 280 characters. This is a tweet more than 140 characters and less...";
            string tweet2 = prefix + "...than 280 characters. This is a tweet more than 140 characters and less than 280 characters.";
            
            string[] statuses = TweetSplitter.SplitTweet(tweetText);

            foreach (string status in statuses)
            {
                Assert.LessOrEqual(status.Length, 140, "Tweet length greater than 140 characters.");
                Assert.IsTrue(status.StartsWith(prefix));
            }
            
            Assert.AreEqual(2, statuses.Length);
            Assert.AreEqual(tweet1, statuses[0]);
            Assert.AreEqual(tweet2, statuses[1]);
        }

        [Test]
        public void DoubleReplyGreaterThan140CharactersAndLessThan280CharactersIsSplitInTwo()
        {
            string prefix = "@bengriswold @jongalloway ";
            string tweetText = prefix + "This is a tweet more than 140 characters and less than 280 characters. This is a tweet more than 140 characters and less than 280 characters. This is a tweet more than 140 characters and less than 280 characters.";
            string tweet1 = prefix + "This is a tweet more than 140 characters and less than 280 characters. This is a tweet more than 140 characters...";
            string tweet2 = prefix + "...and less than 280 characters. This is a tweet more than 140 characters and less than 280 characters.";

            string[] statuses = TweetSplitter.SplitTweet(tweetText);

            foreach (string status in statuses)
            {
                Assert.LessOrEqual(status.Length, 140, "Tweet length greater than 140 characters.");
                Assert.IsTrue(status.StartsWith(prefix));
            }

            Assert.AreEqual(2, statuses.Length);
            Assert.AreEqual(tweet1, statuses[0]);
            Assert.AreEqual(tweet2, statuses[1]);
        }

        [Test]
        public void DirectMessageLessThan140CharactersIsUnchanged()
        {
            string prefix = "D bengriswold ";
            string tweetText = prefix + "This is a tweet less than 140 characters.";
            string[] statuses = TweetSplitter.SplitTweet(tweetText);

            foreach (string status in statuses)
            {
                Assert.LessOrEqual(status.Length, 140, "Tweet length greater than 140 characters.");
                Assert.IsTrue(status.StartsWith(prefix));
            }
            
            Assert.AreEqual(statuses.Length, 1);
            Assert.AreEqual(statuses[0], tweetText);
        }

        [Test]
        public void DirectMessageGreaterThan140CharactersAndLessThan280CharactersIsSplitInTwo()
        {
            string prefix = "D bengriswold ";
            string tweetText = prefix + "This is a tweet more than 140 characters and less than 280 characters. This is a tweet more than 140 characters and less than 280 characters. This is a tweet more than 140 characters and less than 280 characters.";
            string tweet1 = prefix + "This is a tweet more than 140 characters and less than 280 characters. This is a tweet more than 140 characters and less...";
            string tweet2 = prefix + "...than 280 characters. This is a tweet more than 140 characters and less than 280 characters.";
            
            string[] statuses = TweetSplitter.SplitTweet(tweetText);

            foreach (string status in statuses)
            {
                Assert.LessOrEqual(status.Length, 140, "Tweet length greater than 140 characters.");
                Assert.IsTrue(status.StartsWith(prefix));
            }

            Assert.AreEqual(2, statuses.Length, "Tweet not split into correct number of parts.");
            Assert.AreEqual(statuses[0], tweet1);
            Assert.AreEqual(statuses[1], tweet2);
        }
    }
}


