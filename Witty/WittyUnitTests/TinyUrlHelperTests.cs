using NUnit.Framework;
using TwitterLib;

namespace WittyUnitTests
{
    /// <summary>
    /// These these were originally written by cbilson for xUnit.
    /// http://code.google.com/p/wittytwitter/issues/detail?id=82
    /// </summary>
    [TestFixture]
    public class TinyUrlHelperTests
    {
        [Test]
        public void Things_that_start_with_http_should_be_urls()
        {
            Assert.IsTrue(UrlShorteningService.IsUrl("http://www.foo.com/blah/blah.aspx"));
        }
        [Test]
        public void Things_that_start_with_https_should_not_be_urls()
        {
            Assert.IsTrue(UrlShorteningService.IsUrl("https://www.foo.com/blah/blah.aspx"));
        }
        [Test]
        public void Things_that_start_with_ftp_should_not_be_urls()
        {
            Assert.IsTrue(UrlShorteningService.IsUrl("ftp://www.foo.com/blah/blah.aspx"));
        }
        [Test]
        public void Things_that_start_with_feed_should_not_be_urls()
        {
            Assert.IsFalse(UrlShorteningService.IsUrl("feed://www.foo.com/blah/blah.aspx"));
        }
        [Test]
        public void Web_site_addresses_should_be_urls()
        {
            Assert.IsTrue(UrlShorteningService.IsUrl("http://www.foo.com"));
        }
        [Test]
        public void Web_site_root_addresses_should_be_urls()
        {
            Assert.IsTrue(UrlShorteningService.IsUrl("http://www.foo.com/"));
        }
        [Test]
        public void Addresses_that_dont_end_in_a_filename_extension_should_be_urls()
        {
            Assert.IsTrue(UrlShorteningService.IsUrl("http://www.foo.com/x"));
        }
        [Test]
        public void Addresses_that_have_commas_should_be_urls()
        {
            Assert.IsTrue(UrlShorteningService.IsUrl("http://www.foo.com/x,123"));
        }
    }
}
