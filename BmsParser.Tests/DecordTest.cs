using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BmsParser.Bmson;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BmsParser.Tests
{
    [TestClass]
    public class DecordTest
    {
        [TestMethod]
        public void Decord()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var decorder = new BmsDecoder();
            var model = decorder.Decode("_ld2013_a.bms");
            Assert.AreEqual("6c633f8678c0c94757f471e68bd1ea07d5b00ca3a82c0bb19d6555a5f83d54dd", model.Sha256);
            Assert.AreEqual("87bf3f70b00cc56c8b1f93ee1961d6b3", model.MD5);
            Assert.AreEqual(1558, model.GetTotalNotes());
            Assert.AreEqual(115125, model.LastTime);
        }

        [TestMethod]
        public void DecordBmson()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var decorder = new BmsonDecoder();
            var model = decorder.Decode("_Everyday evermore [PIANO].bmson");
            Assert.AreEqual("a49dc164418606a0a5ce46628e1cf825ed3d9375d1cecf438688def6e492e0e8", model.Sha256);
            //Assert.AreEqual("87bf3f70b00cc56c8b1f93ee1961d6b3", model.MD5);
            Assert.AreEqual(1500, model.GetTotalNotes());
            Assert.AreEqual(156846, model.LastTime);
        }

        [TestMethod]
        public void Decord2()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var decorder = new BmsDecoder();
            using var log = File.OpenWrite("decodelog.log");
            using var writer = new StreamWriter(log);
            var model = decorder.Decode("_DPAG.bms");
            Assert.AreEqual("4d8a0ebdafd666c3791c8dce3d56311c8c2edc718c714f88073e23356153f254", model.Sha256);
            Assert.AreEqual("66739cb4d49693b07fa33ed2265f1f6f", model.MD5);
            Assert.AreEqual(1619, model.GetTotalNotes());
            Assert.AreEqual(113072, model.LastTime);
        }

        [TestMethod]
        public void Decord3()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var decorder = new BmsDecoder();
            using var log = File.OpenWrite("decodelog.log");
            using var writer = new StreamWriter(log);
            var model = decorder.Decode("gekkou(7).bme");
            Assert.AreEqual("c5d6d75b2a06e1a3cdcf9f0fd89f9ff44fa570dc623bb976b5fb1ed5198a1932", model.Sha256);
            Assert.AreEqual("d8fa134c6deb2a9e6736dd220839cfd3", model.MD5);
            Assert.AreEqual(1200, model.GetTotalNotes());
            Assert.AreEqual(117473, model.LastTime);
        }
    }
}
