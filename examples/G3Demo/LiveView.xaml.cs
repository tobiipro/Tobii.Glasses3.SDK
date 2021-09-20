using System;
using System.Linq;
using System.Windows;
using FFmpeg.AutoGen;
using Unosquare.FFME.Common;

namespace G3Demo
{
    public partial class LiveView
    {
        private DeviceVM _vm;

        public LiveView()
        {
            InitializeComponent();
            Media.MediaOpening += MediaOnMediaOpening;
            Media.DataFrameReceived += MediaOnDataFrameReceived;
            Media.MediaInitializing += MediaOnMediaInitializing;
            Media.RenderingVideo += (sender, args) => _vm.DrawGaze(args.StartTime, Media.ActualWidth, Media.ActualHeight);
        }

        private void MediaOnMediaInitializing(object sender, MediaInitializingEventArgs e)
        {
            // mixed settings trying to get lower latency
            e.Configuration.GlobalOptions.FlagNoBuffer = true;
            e.Configuration.PrivateOptions["flags"] = "low_delay";
            e.Configuration.PrivateOptions["sync"] = "ext";
        }

        private void MediaOnDataFrameReceived(object sender, DataFrameReceivedEventArgs e)
        {
            _vm.HandleData(e.Frame, e.Stream);
        }

        private void MediaOnMediaOpening(object sender, MediaOpeningEventArgs e)
        {
            var availableStreams = e.Info.Streams
                .Where(s => s.Value.CodecType == AVMediaType.AVMEDIA_TYPE_VIDEO)
                .Select(x => x.Value)
                .ToList();

            e.Options.VideoStream = availableStreams.First(s=>s.PixelHeight>=500);

            // mixed settings trying to get lower latency
            e.Options.DecoderParams.EnableFastDecoding = true;
            e.Options.DecoderParams.EnableLowDelayDecoding = true;
            e.Options.MinimumPlaybackBufferPercent = 0;
            e.Options.VideoBlockCache = 0;
            e.Options.UseParallelDecoding = true;
            e.Options.UseParallelRendering = true;
            e.Options.IsTimeSyncDisabled = true;
            Media.RendererOptions.AudioDisableSync = true;

        }

        private async void LiveView_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is DeviceVM vm && vm.LiveVideoUri != null)
            {
                _vm = vm;
                await Media.Open(vm.LiveVideoUri);
            }
        }
    }
}