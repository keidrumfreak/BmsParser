using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BmsParser
{
    class Bmson
    {
        [JsonPropertyName("info")]
        public BmsonInfo Info { get; set; }

        [JsonPropertyName("lines")]
        public BarLine[] Lines { get; set; }

        [JsonPropertyName("bpm_events")]
        public BpmEvent[] BpmEvents { get; set; }

        [JsonPropertyName("stop_events")]
        public StopEvent[] StopEvents { get; set; }

        [JsonPropertyName("sound_channels")]
        public SoundChannel[] SoundChannels { get; set; }

        [JsonPropertyName("bga")]
        public Bga Bga { get; set; }
    }

    class BmsonInfo
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("subtitle")]
        public string SubTitle { get; set; }

        [JsonPropertyName("artist")]
        public string Artist { get; set; }

        [JsonPropertyName("subartists")]
        public string[] SubArtists { get; set; }

        [JsonPropertyName("genre")]
        public string Genre { get; set; }

        [JsonPropertyName("mode_hint")]
        public string ModeHint { get; set; }

        [JsonPropertyName("chart_name")]
        public string ChartName { get; set; }

        [JsonPropertyName("level")]
        public ulong Level { get; set; }

        [JsonPropertyName("init_bpm")]
        public double InitBpm { get; set; }

        [JsonPropertyName("judge_rank")]
        public double JudgeRank { get; set; }

        [JsonPropertyName("total")]
        public double Total { get; set; }

        [JsonPropertyName("back_image")]
        public string BackImage { get; set; }

        [JsonPropertyName("eyecatch_image")]
        public string EyecatchImage { get; set; }

        [JsonPropertyName("banner_image")]
        public string BannerImage { get; set; }

        [JsonPropertyName("preview_image")]
        public string PreviewMusic { get; set; }

        [JsonPropertyName("resolution")]
        public int Resolution { get; set; }
    }

    public class BarLine
    {
        [JsonPropertyName("y")]
        int Y { get; set; }
    }

    public class SoundChannel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("notes")]
        public Note[] Notes { get; set; }
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
        public BgaHeader[] BgaHeader { get; set; }

        [JsonPropertyName("bga_events")]
        public BgaEvent[] BgaEvents { get; set; }

        [JsonPropertyName("layer_events")]
        public BgaEvent[] LayerEvents { get; set; }

        [JsonPropertyName("poor_events")]
        public BgaEvent[] PoorEvents { get; set; }
    }

    public class BgaHeader
    {
        [JsonPropertyName("id")]
        public int ID { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public class BgaEvent
    {
        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("id")]
        public int ID { get; set; }
    }
}
