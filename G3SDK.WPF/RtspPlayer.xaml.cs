using System.Linq;
using System.Windows;
using FFmpeg.AutoGen;
using Unosquare.FFME.Common;

namespace G3SDK.WPF
{
    /// <summary>
    /// Interaction logic for RtspPlayer.xaml
    /// </summary>
    public partial class RtspPlayer
    {
        public RtspPlayer()
        {
            InitializeComponent();

            Media.MediaOpening += MediaOnMediaOpening;
            Media.MediaInitializing += MediaOnMediaInitializing;
            Media.IsMuted = true;
            Media.MediaChanging += Media_MediaChanging;
        }

        private void MediaOnMediaInitializing(object sender, MediaInitializingEventArgs e)
        {
            // mixed settings trying to get lower latency
            e.Configuration.GlobalOptions.FlagNoBuffer = true;
            e.Configuration.PrivateOptions["flags"] = "low_delay";
            e.Configuration.PrivateOptions["sync"] = "ext";
        }

        private void Media_MediaChanging(object sender, MediaOpeningEventArgs e)
        {
            e.Options.VideoStream = e.Info.Streams.First(s => s.Value.CodecType == AVMediaType.AVMEDIA_TYPE_VIDEO && s.Key != e.Options.VideoStream.StreamId).Value;
        }

        private void MediaOnMediaOpening(object sender, MediaOpeningEventArgs e)
        {
            var availableStreams = e.Info.Streams
                .Where(s => s.Value.CodecType == AVMediaType.AVMEDIA_TYPE_VIDEO)
                .Select(x => x.Value)
                .ToList();

            e.Options.VideoStream = availableStreams.First(s => s.PixelHeight >= 500);

            // mixed settings trying to get lower latency
            e.Options.DecoderParams.EnableFastDecoding = true;
            e.Options.DecoderParams.EnableLowDelayDecoding = true;
            e.Options.MinimumPlaybackBufferPercent = 0.5;
            e.Options.VideoBlockCache = 0;
            e.Options.UseParallelDecoding = true;
            e.Options.UseParallelRendering = true;
            e.Options.IsTimeSyncDisabled = true;
            Media.RendererOptions.AudioDisableSync = true;
        }

        private void RtspPlayer_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is RtspPlayerVM vm)
            {
                vm.SetMedia(Media);
            }
        }
    }
}