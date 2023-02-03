using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace G3SDK
{

    public interface ISceneCamera : IG3Object
    {
        IG3Observable<ZoomSetResult> ZoomSet { get; }
        IG3Observable<string> Changed { get; }
        Task<float> AutoExposureChangeSpeed { get; }
        Task<float> AutoExposureGazeSpotSize { get; }
        Task<float> AutoExposureGazeSpotWeight { get; }
        Task<bool> ZoomEnabled { get; }
        Task<float> ZoomX { get; }
        Task<float> ZoomY { get; }
        Task<bool> SetAutoExposureChangeSpeed(float value);
        Task<bool> SetAutoExposureGazeSpotSize(float value);
        Task<bool> SetAutoExposureGazeSpotWeight(float value);
        Task<DisableZoomState> DisableZoom();
        Task<EnableZoomState> EnableZoom(float normalizedXCenter, float normalizedYCenter);
    }

    public class SceneCamera : G3Object, ISceneCamera
    {
        public const string AutoExposureChangeSpeedName = "autoexposure-change-speed";
        public const string AutoExposureGazeSpotSizeName = "autoexposure-gaze-spot-size";
        public const string AutoExposureGazeSpotWeightName = "autoexposure-gaze-spot-weight";
        private readonly RWProperty<float> _autoexposureChangeSpeed;
        private readonly RWProperty<float> _autoexposureGazeSpotSize;
        private readonly RWProperty<float> _autoexposureGazeSpotWeight;
        private readonly ROProperty<bool> _zoomEnabled;
        private readonly ROProperty<float> _zoomX;
        private readonly ROProperty<float> _zoomY;

        public SceneCamera(G3Api api, string parentUrl) : base(api, $"{parentUrl}/scenecamera")
        {
            _autoexposureChangeSpeed = AddRWProperty(AutoExposureChangeSpeedName, ParserHelpers.ParseFloat, f => f.ToString(CultureInfo.InvariantCulture));
            _autoexposureGazeSpotSize = AddRWProperty(AutoExposureGazeSpotSizeName, ParserHelpers.ParseFloat, f => f.ToString(CultureInfo.InvariantCulture));
            _autoexposureGazeSpotWeight = AddRWProperty(AutoExposureGazeSpotWeightName, ParserHelpers.ParseFloat, f => f.ToString(CultureInfo.InvariantCulture));
            _zoomEnabled = AddROProperty("zoom-enabled", bool.Parse);
            _zoomX = AddROProperty("zoom-x", ParserHelpers.ParseFloat);
            _zoomY = AddROProperty("zoom-y", ParserHelpers.ParseFloat);

            Changed = AddSignal("changed", ConvertChanged);
            ZoomSet = AddSignal("zoom-set", ConvertZoomSet);
        }

        private ZoomSetResult ConvertZoomSet(List<JToken> arg)
        {
            return new ZoomSetResult(arg[0].Value<bool>(),
                arg[1].Value<float>(),
                arg[2].Value<float>());
        }

        public IG3Observable<ZoomSetResult> ZoomSet { get; }

        private string ConvertChanged(List<JToken> arg)
        {
            return arg[0].Value<string>();
        }

        public IG3Observable<string> Changed { get; }


        public Task<float> AutoExposureChangeSpeed => _autoexposureChangeSpeed.Value();
        public Task<float> AutoExposureGazeSpotSize => _autoexposureGazeSpotSize.Value();
        public Task<float> AutoExposureGazeSpotWeight => _autoexposureGazeSpotWeight.Value();
        public Task<bool> ZoomEnabled => _zoomEnabled.Value();
        public Task<float> ZoomX => _zoomX.Value();
        public Task<float> ZoomY => _zoomY.Value();

        public Task<bool> SetAutoExposureChangeSpeed(float value)
        {
            return _autoexposureChangeSpeed.Set(value);
        }
        public Task<bool> SetAutoExposureGazeSpotSize(float value)
        {
            return _autoexposureGazeSpotSize.Set(value);
        }
        public Task<bool> SetAutoExposureGazeSpotWeight(float value)
        {
            return _autoexposureGazeSpotWeight.Set(value);
        }

        public async Task<DisableZoomState> DisableZoom()
        {
            var res = await G3Api.ExecuteCommand<string>(Path, "disable-zoom", LogLevel.info);
            switch (res)
            {
                case "success": return DisableZoomState.Success;
                default: return DisableZoomState.Unknown;
            }
        }
        public async Task<EnableZoomState> EnableZoom(float normalizedXCenter, float normalizedYCenter)
        {
            var res = await G3Api.ExecuteCommand<string>(Path, "enable-zoom", LogLevel.info, normalizedXCenter, normalizedYCenter);
            switch (res)
            {
                case "success": return EnableZoomState.Success;
                case "No action handler found for enable-zoom": return EnableZoomState.NoActionHandlerFound;
                case "fail-zoom-coordinates-out-of-range": return EnableZoomState.CoordinatesOutOfRange;
                default:
                    G3Api.Log(LogLevel.info, $"Unknown error message from enable-zoom: {res}");
                    return EnableZoomState.Unknown;
            }
        }



        // public IG3Observable<HuConnectionState> ConnectionStateChanged { get; }
        //
        // private HuConnectionState ConvertConnectionStateChanged(List<JToken> arg)
        // {
        //     return ParserHelpers.ParseHuConnectionState(arg[0].Value<string>());
        // }
    }

    public class ZoomSetResult
    {
        public ZoomSetResult(bool enabled, float x, float y)
        {
            Enabled = enabled;
            X = x;
            Y = y;
        }

        public bool Enabled { get; }
        public float X { get; }
        public float Y { get; }
    }

    public enum EnableZoomState
    {
        Success,
        Unknown,
        NoActionHandlerFound,
        CoordinatesOutOfRange
    }

    public enum DisableZoomState
    {
        Success,
        Unknown
    }
}