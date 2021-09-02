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

        [TestMethod]
        public void LoadBpm()
        {
            var line = "#BPM 12.5";
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);

            Assert.AreEqual(12.5, model.Bpm);
            Assert.IsFalse(logs.Any());
        }

        [TestMethod]
        public void LoadNegativeBpm()
        {
            var line = "#BPM -12.5";
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);

            Assert.IsTrue(logs.Any());
            Assert.AreEqual(State.Warning, logs[0].State);
            Assert.AreEqual($"#negative BPMはサポートされていません : {line}", logs[0].Message);
        }

        [DataTestMethod]
        [DataRow("#PLAYER aaa", "#PLAYER")]
        [DataRow("#BPM aaa", "#BPM")]
        public void LoadNumberFailed(string line, string name)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);

            Assert.IsTrue(logs.Any());
            Assert.AreEqual(State.Warning, logs[0].State);
            Assert.AreEqual($"{name}に数字が定義されていません : {line}", logs[0].Message);
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
            Assert.AreEqual($"{name}に無効な数字が定義されています : {line}", logs[0].Message);
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

        [DataTestMethod]
        [DataRow("@TEST1 VALUE1", "TEST1", "VALUE1")]
        [DataRow("%TEST2 VALUE2", "TEST2", "VALUE2")]
        public void LoadMap(string line, string key, string value)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);

            Assert.AreEqual(value, model.Values[key]);
        }

        [DataTestMethod]
        [DataRow("@TEST1VALUE1")]
        [DataRow("%TEST2VALUE2 ")]
        public void LoadMapFailed(string line)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);

            Assert.IsFalse(model.Values.Any());
        }

        [DataTestMethod]
        [DataRow("#BPM01 12.5", 1, 12.5)]
        public void LoadBpmSequence(string line, int seq, double value)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);
            Assert.AreEqual(value, processor.BpmTable[seq]);
        }

        [DataTestMethod]
        [DataRow(@"#WAV0z Folder\wav", 35, "Folder/wav")]
        public void LoadWavSequence(string line, int seq, string value)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);
            Assert.AreEqual(value, processor.WavTable[seq]);
        }

        [DataTestMethod]
        [DataRow(@"#BMPA0 Folder/wav", 360, "Folder/wav")]
        public void LoadBmpSequence(string line, int seq, string value)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);
            Assert.AreEqual(value, processor.BgaTable[seq]);
        }

        [DataTestMethod]
        [DataRow(@"#STOP00 -394", 0, 2)]
        public void LoadNegativeStopSequence(string line, int seq, double value)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);
            Assert.AreEqual((int)value, (int)processor.StopTable[seq]); // 誤差が出るのでintに丸めておく
            Assert.IsTrue(logs.Any());
            Assert.AreEqual(State.Warning, logs[0].State);
            Assert.AreEqual($"#negative STOPはサポートされていません : {line}", logs[0].Message);
        }

        [DataTestMethod]
        [DataRow(@"#SCROLL01 -394", 1, -394)]
        [DataRow(@"#SCROLL05 394", 5, 394)]
        public void LoadScrolSequence(string line, int seq, double value)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);
            Assert.AreEqual((int)value, (int)processor.ScrollTable[seq]); 
            Assert.IsFalse(logs.Any());
        }

        [DataTestMethod]
        [DataRow("#00100:00", 1, "#00100:00")]
        [DataRow("#99900:00", 999, "#99900:00")]
        public void LoadBarLine(string line, int seq, string value)
        {
            var model = new BmsModel();
            var logs = new List<DecodeLog>();

            var processor = new LineProcessor();
            processor.Process(model, line, logs);
            Assert.AreEqual(value, processor.BarTable[seq][0]);
            Assert.IsFalse(logs.Any());
        }
    }
}
