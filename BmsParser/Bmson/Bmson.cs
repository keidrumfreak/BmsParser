using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BmsParser.Bmson
{
    class Bmson
    {
        [JsonPropertyName("info")]
        public BmsonInfo? Info { get; set; }

        [JsonPropertyName("lines")]
        public BarLine[] Lines { get; set; } = [];

        [JsonPropertyName("bpm_events")]
        public BpmEvent[] BpmEvents { get; set; } = [];

        [JsonPropertyName("stop_events")]
        public StopEvent[] StopEvents { get; set; } = [];

        [JsonPropertyName("scroll_events")]
        public ScrollEvent[] ScrollEvents { get; set; } = [];

        [JsonPropertyName("sound_channels")]
        public required SoundChannel[] SoundChannels { get; set; }

        [JsonPropertyName("bga")]
        public Bga? Bga { get; set; }

        [JsonPropertyName("mine_channels")]
        public MineChannel[] MineChannels { get; set; } = [];

        [JsonPropertyName("key_channels")]
        public MineChannel[] KeyChannels { get; set; } = [];
    }

    class BmsonInfo
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("subtitle")]
        public string Subtitle { get; set; } = string.Empty;

        [JsonPropertyName("artist")]
        public string Artist { get; set; } = string.Empty;

        [JsonPropertyName("subartists")]
        public string[] SubArtists { get; set; } = [];

        [JsonPropertyName("genre")]
        public string Genre { get; set; } = string.Empty;

        [JsonPropertyName("mode_hint")]
        public string ModeHint { get; set; } = string.Empty;

        [JsonPropertyName("chart_name")]
        public string ChartName { get; set; } = string.Empty;

        [JsonPropertyName("level")]
        public ulong Level { get; set; }

        [JsonPropertyName("init_bpm")]
        public double InitBpm { get; set; }

        [JsonPropertyName("judge_rank")]
        public double JudgeRank { get; set; } = 100;

        [JsonPropertyName("total")]
        public double Total { get; set; } = 100;

        [JsonPropertyName("back_image")]
        public string BackImage { get; set; } = string.Empty;

        [JsonPropertyName("eyecatch_image")]
        public string EyecatchImage { get; set; } = string.Empty;

        [JsonPropertyName("banner_image")]
        public string BannerImage { get; set; } = string.Empty;

        [JsonPropertyName("preview_image")]
        public string PreviewMusic { get; set; } = string.Empty;

        [JsonPropertyName("resolution")]
        public int Resolution { get; set; } = 240;

        [JsonPropertyName("ln_type")]
        public int LNType { get; set; }
    }

    public class BarLine
    {
        [JsonPropertyName("y")]
        public int Y { get; set; }
    }

    public class SoundChannel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public BmsonNote[] Notes { get; set; } = [];
    }

    public class BmsonNote
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("l")]
        public int L { get; set; }

        [JsonPropertyName("c")]
        public bool C { get; set; }

        [JsonPropertyName("t")]
        public int T { get; set; }

        [JsonPropertyName("up")]
        public bool Up { get; set; } = false;
    }

    public class BpmEvent
    {
        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("bpm")]
        public double Bpm { get; set; }
    }

    public class StopEvent
    {
        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }
    }

    public class Bga
    {
        [JsonPropertyName("bga_header")]
        public BgaHeader[]? BgaHeader { get; set; }

        [JsonPropertyName("bga_sequence")]
        public BgaSequence[]? BgaSequence { get; set; }

        [JsonPropertyName("bga_events")]
        public BgaEvent[]? BgaEvents { get; set; }

        [JsonPropertyName("layer_events")]
        public BgaEvent[]? LayerEvents { get; set; }

        [JsonPropertyName("poor_events")]
        public BgaEvent[]? PoorEvents { get; set; }
    }

    public class BgaHeader
    {
        [JsonPropertyName("id")]
        public int ID { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class BgaEvent
    {
        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("id")]
        public int ID { get; set; }

        [JsonPropertyName("id_set")]
        public int[] IDSet { get; set; } = [];

        [JsonPropertyName("condition")]
        public string Condition { get; set; } = string.Empty;

        [JsonPropertyName("interval")]
        public int Interval { get; set; }
    }

    public class BgaSequence
    {
        [JsonPropertyName("id")]
        public int ID { get; set; }

        [JsonPropertyName("sequence")]
        public BmsonSequence[] Sequence { get; set; } = [];
    }

    public class BmsonSequence
    {
        [JsonPropertyName("time")]
        public int Time { get; set; }

        [JsonPropertyName("id")]
        public int? ID { get; set; }
    }

    public class ScrollEvent
    {
        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("rate")]
        public double Rate { get; set; }
    }

    public class MineChannel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public BmsonMineNote[] Notes { get; set; } = [];
    }

    public class BmsonMineNote
    {
        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("damage")]
        public double Damage { get; set; }
    }
}
