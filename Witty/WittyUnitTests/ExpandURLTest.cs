using System;
using System.IO;
using System.Net;
using NUnit.Framework;
using System.Runtime.Serialization.Json;

namespace WittyUnitTests
{
    [TestFixture]
    public class ExpandURLTest
    {
        [SetUp]
        public void Init()
        {

        }

        [Test]
        public void GetSingleLongURLPleaseSpike()
        {
            string tinyURL = "http://bit.ly/enAo";
            string formattedUri = string.Format("http://www.longurlplease.com/api/v1.1?q={0}", tinyURL);

            var serviceUri = new Uri(formattedUri, UriKind.Absolute);
            var webRequest = (HttpWebRequest)System.Net.WebRequest.Create(serviceUri);

            var response = (HttpWebResponse)webRequest.GetResponse();

            string jsonResponse = string.Empty;

            using (StreamReader sr = new StreamReader(response.GetResponseStream()))
            {
                // Insert fancy DataContractJsonSerializer in .NET 3.5 stuff here
                jsonResponse = sr.ReadToEnd();
                //new DataContractJsonSerializer(...)
            }

            /* 
             *  See http://www.longurlplease.com/docs
             *  http://code.google.com/p/wittytwitter/issues/detail?id=80&q=is.gd
             *  
             *  Multiple short URLs may be submitted:             
             *  http://www.longurlplease.com/api/v1.1?q=http://bit.ly/enAo&q=http://short.ie/bestxkcd
             * 
             *  With following result:       
             * {"http://short.ie\/bestxkcd": "http:\/\/razvan784.blogspot.com\/2007\/09\/best-of-xkcd.html",
             *  "http://bit.ly\/enAo": "http:\/\/www.boasas.com\/?c=1030"}
             *  
             */
        }

    }
}


