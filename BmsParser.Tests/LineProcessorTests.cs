using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BmsParser.Tests
{
    [TestClass]
    public class LineProcessorTests
    {
        [DataTestMethod]
        [DataRow("#PLAYER 123", 123, nameof(BmsModel.Player))]
        [DataRow("#LNMODE 3", 3, nameof(BmsModel.LNMode))]
        public void LoadNumber(string line, int value, string propertyName)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);

            Assert.AreEqual(value, (int)typeof(BmsModel).GetProperty(propertyName).GetValue(model));
            Assert.IsFalse(logs.Any());
        }

        [DataTestMethod]
        [DataRow("#PLAYER aaa", "#PLAYER", nameof(BmsModel.Player))]
        public void LoadNumberFailed(string line, string name, string propertyName)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);

            Assert.IsTrue(logs.Any());
            Assert.AreEqual(State.Warning, logs[0].State);
            Assert.AreEqual($"{name}に数字が定義されていません", logs[0].Message);
        }

        [DataTestMethod]
        [DataRow("#RANK 10", "#RANK", nameof(BmsModel.JudgeRank))]
        public void LoadNumberOutOfRange(string line, string name, string propertyName)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);

            Assert.IsTrue(logs.Any());
            Assert.AreEqual(State.Warning, logs[0].State);
            Assert.AreEqual($"{name}に無効な数字が定義されています", logs[0].Message);
        }

        [DataTestMethod]
        [DataRow("#GENRE TEST", "TEST", nameof(BmsModel.Genre))]
        [DataRow("#TITLE TEST", "TEST", nameof(BmsModel.Title))]
        [DataRow("#SUBTITLE TEST", "TEST", nameof(BmsModel.SubTitle))]
        [DataRow("#ARTIST TEST", "TEST", nameof(BmsModel.Artist))]
        [DataRow("#SUBARTIST TEST", "TEST", nameof(BmsModel.SubArtist))]
        public void LoadText(string line, string value, string propertyName)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);

            Assert.AreEqual(value, (string)typeof(BmsModel).GetProperty(propertyName).GetValue(model));
            Assert.IsFalse(logs.Any());
        }

        [DataTestMethod]
        [DataRow(@"#BANNER Folder\bmp", "Folder/bmp", nameof(BmsModel.Banner))]
        [DataRow(@"#STAGEFILE Folder/bmp", "Folder/bmp", nameof(BmsModel.StageFile))]
        public void LoadPath(string line, string value, string propertyName)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);

            Assert.AreEqual(value, (string)typeof(BmsModel).GetProperty(propertyName).GetValue(model));
            Assert.IsFalse(logs.Any());
        }
    }
}
