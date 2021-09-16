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
    }
}
