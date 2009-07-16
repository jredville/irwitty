using NUnit.Framework;
using SpecUnit;
using TwitterLib;
using System;
using NUnit.Framework.SyntaxHelpers;

namespace WittyUnitTests
{
    [TestFixture]
    public class TweetTest
    {
        [SetUp]
        public void Init()
        {
        }

        [Test]
        public void Truncate()
        {
            int count = 10;

            TweetCollection tweets = new TweetCollection();

            for (int i = 0; i < 20; i++)
            {
                Tweet tweet = new Tweet();
                tweet.Id = i;
                tweets.Insert(0, tweet);
            }

            tweets.TruncateAfter(count);

            Assert.AreEqual(count, tweets.Count);
            Assert.AreEqual(tweets[0].Id, 19);
        }

        [Test]
        public void Tweet_added_to_empty_collection_succeeds()
        {
            TweetCollection tweets = new TweetCollection();
            Tweet tweet = new Tweet() { DateCreated = DateTime.Now, Id = 1 };
            tweets.Add(tweet);
            Assert.That(tweets.Contains(tweet));
            Assert.That(tweets.Count == 1);
        }

        [Test]
        public void Tweet_added_to_collection_in_sorted_order_with_descending_sort()
        {
            TweetCollection tweets = new TweetCollection(SortOrder.Descending);
            Tweet tweet1 = new Tweet() { DateCreated = DateTime.Now, Id = 1 };
            Tweet tweet2 = new Tweet() { DateCreated = tweet1.DateCreated.Value.Subtract(TimeSpan.FromMinutes(5)), Id = 2 };
            Tweet tweet3 = new Tweet() { DateCreated = tweet1.DateCreated.Value.Subtract(TimeSpan.FromMinutes(2)), Id = 2 };

            tweets.Add(tweet1);
            tweets.Add(tweet2);
            tweets.Add(tweet3);
            Assert.That(tweets[0], Is.EqualTo(tweet1));
            Assert.That(tweets[1], Is.EqualTo(tweet3));
            Assert.That(tweets[2], Is.EqualTo(tweet2));
        }

        [Test]
        public void Tweet_added_to_collection_in_sorted_order_with_ascending_sort()
        {
            TweetCollection tweets = new TweetCollection(SortOrder.Ascending);
            Tweet tweet1 = new Tweet() { DateCreated = DateTime.Now, Id = 1 };
            Tweet tweet2 = new Tweet() { DateCreated = tweet1.DateCreated.Value.Subtract(TimeSpan.FromMinutes(5)), Id = 2 };
            Tweet tweet3 = new Tweet() { DateCreated = tweet1.DateCreated.Value.Subtract(TimeSpan.FromMinutes(2)), Id = 2 };

            tweets.Add(tweet1);
            tweets.Add(tweet2);
            tweets.Add(tweet3);
            Assert.That(tweets[0], Is.EqualTo(tweet2));
            Assert.That(tweets[1], Is.EqualTo(tweet3));
            Assert.That(tweets[2], Is.EqualTo(tweet1));
        }

        [Test]
        public void Tweet_added_to_collection_unsorted()
        {
            TweetCollection tweets = new TweetCollection(SortOrder.None);
            Tweet tweet1 = new Tweet() { DateCreated = DateTime.Now, Id = 1 };
            Tweet tweet2 = new Tweet() { DateCreated = tweet1.DateCreated.Value.Subtract(TimeSpan.FromMinutes(5)), Id = 2 };
            Tweet tweet3 = new Tweet() { DateCreated = tweet1.DateCreated.Value.Subtract(TimeSpan.FromMinutes(2)), Id = 2 };

            tweets.Add(tweet1);
            tweets.Add(tweet2);
            tweets.Add(tweet3);
            Assert.That(tweets[0], Is.EqualTo(tweet1));
            Assert.That(tweets[1], Is.EqualTo(tweet2));
            Assert.That(tweets[2], Is.EqualTo(tweet3));
        }

    }
    /*

 * NOTE: SpecUnit isn't working with with TestDriven.net nor ReSharper TestRunner right now,
 * so we'll just use the standard NUnit attributes until SpecUnit is fixed.
 */
    //    [Concern(typeof(TweetCollection))]
    [TestFixture]
    public class when_a_collection_of_tweets_is_truncated : ContextSpecification
    {
        private TweetCollection _tweets;
        private int _totalTweets, _keepCount;
        protected override void Context()
        {
            _tweets = new TweetCollection();
            _totalTweets = 20;
            _keepCount = 5;
            for (int i = 1; i <= _totalTweets; i++)
            {
                var tweet = new Tweet { Id = i };
                _tweets.Insert(0, tweet);
                _tweets.Insert(0, tweet);
            }
        }

        protected override void Because()
        {
            _tweets.TruncateAfter(_keepCount);
        }

        //        [Observation]
        [Test]
        public void then_should_remove_excess_tweets()
        {
            _tweets.Count.ShouldEqual(_keepCount);
        }

        //[Test]
        //public void then_should_remove_tweets_from_collection_tail()
        //{
        //    _tweets.First().Id.ShouldEqual(20);
        //    _tweets.Last().Id.ShouldEqual(16);
        //}
    }


    [TestFixture]
    public class when_direct_message_collections_is_converted_to_tweet_collection : ContextSpecification
    {
        private DirectMessageCollection _directMessages;
        private int _directMessageCount = 3;

        private TweetCollection _tweets;

        protected override void Context()
        {
            _directMessages = new DirectMessageCollection();

            for (int i = 1; i <= _directMessageCount; i++)
            {
                var directMessage = new DirectMessage { Id = i };
                _directMessages.Insert(0, directMessage);
            }
        }

        protected override void Because()
        {
            _tweets = _directMessages.ToTweetCollection();
        }

        //        [Observation]
        [Test]
        public void then_should_contain_tweets_for_direct_messages()
        {
            _tweets.Count.ShouldEqual(_directMessageCount);

            for (int i = 0; i < _tweets.Count; i++)
            {
                DirectMessage message = _directMessages[i];
                Tweet tweet = _tweets[i];

                tweet.ShouldEqual(message.ToTweet());
            }
        }
    }

    [TestFixture]
    public class when_a_tweet_is_added_to_collection : ContextSpecification
    {
        private TweetCollection _tweets;
        private int _totalTweets, _keepCount;
        protected override void Context()
        {
            _tweets = new TweetCollection();
        }

        protected override void Because()
        {
            //_tweets.Add
        }

        //        [Observation]
        [Test]
        public void then_()
        {
            
        }
    }


}


