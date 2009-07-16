using NUnit.Framework;
using TwitterLib.Utilities;

namespace WittyUnitTests
{
    [TestFixture]
    public class UserBehaviorManagerTests
    {
        [Test]
        public void can_serialize()
        {            
            var firstManager = new UserBehaviorManager();
            firstManager.AddBehavior("ignu", UserBehavior.NeverAlert);
            string info = firstManager.Serialize();
            var secondManager = new UserBehaviorManager(info);
            Assert.AreEqual(secondManager.GetBehavior("ignu"), UserBehavior.NeverAlert);
            Assert.AreEqual(secondManager.GetBehavior("not a user"), UserBehavior.Default);
        }
        
    }
}
