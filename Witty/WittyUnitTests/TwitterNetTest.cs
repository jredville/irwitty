using System.Security;
using NUnit.Framework;
using TwitterLib;

namespace WittyUnitTests
{
    [TestFixture]
    public class TwitterNetTest
    {
        protected string username;
        protected SecureString password;
        protected int userId;
        protected int friendsCount;

        protected int alanUserId;
        protected int alanFriendsCount;
        protected User testUser;

        [SetUp]
        public void Init()
        {
            username = "WittyTest";
            password = TwitterNet.ToSecureString("WittyTest");
            userId = 14203624; // WittyTest's Twitter id

            TwitterNet twitter = new TwitterNet(username, password);
            testUser = twitter.Login();
        }
        
        [Test]
        public void Login()
        {
            TwitterNet twitter = new TwitterNet(username, password);
            User user = twitter.Login();
            Assert.AreEqual(userId, user.Id);
        }

        [Test]
        public void GetFriends()
        {
            TwitterNet twitter = new TwitterNet(username, password);
            UserCollection uc = twitter.GetFriends();
            Assert.AreEqual(testUser.FollowingCount, uc.Count);
        }

        [Test]
        public void GetFriendsWithId()
        {
            TwitterNet twitter = new TwitterNet(username, password);
            UserCollection uc = twitter.GetFriends(14203624);
            Assert.AreEqual(testUser.FollowingCount, uc.Count);
        }

        //Alan: It sucks that twitter requires authentication to get followers
        //[Test]
        //public void GetFriendsUnauthenticated()
        //{
        //    TwitterNet twitter = new TwitterNet();
        //    UserCollection uc = twitter.GetFriends(userId);
        //    Assert.AreEqual(friendsCount, uc.Count);
        //}

        ///// <summary>
        ///// For testing GetFriends above 100.
        ///// </summary>
        //[Test]
        //public void GetAlanFriendsUnauthenticated()
        //{
        //    TwitterNet twitter = new TwitterNet();
        //    UserCollection uc = twitter.GetFriends(alanUserId);
        //    Assert.AreEqual(alanFriendsCount, uc.Count); 
        //}

        [Test]
        public void GetUser()
        {
            TwitterNet twitter = new TwitterNet(username,password);
            User user = twitter.GetUser(userId);
            Assert.AreEqual(username, user.ScreenName);
        }
    }
}
