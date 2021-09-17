using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
}
