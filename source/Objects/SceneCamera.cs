using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace G3SDK
{

    public interface ISceneCamera : IG3Object
    {
        IG3Observable<string> Changed { get; }
        Task<float> AutoExposureChangeSpeed { get; }
        Task<float> AutoExposureGazeSpotSize { get; }
        Task<float> AutoExposureGazeSpotWeight { get; }
        Task<bool> Zoomed { get; }
        Task<float> ZoomX { get; }
        Task<float> ZoomY { get; }
        Task<bool> SetAutoExposureChangeSpeed(float value);
        Task<bool> SetAutoExposureGazeSpotSize(float value);
        Task<bool> SetAutoExposureGazeSpotWeight(float value);
        Task<ZoomOffResult> ZoomOff();
        Task<ZoomOnResult> ZoomOn(float normalizedXCenter, float normalizedYCenter);
    }

    public class SceneCamera : G3Object, ISceneCamera
    {
        public const string AutoExposureChangeSpeedName = "autoexposure-change-speed";
        public const string AutoExposureGazeSpotSizeName = "autoexposure-gaze-spot-size";
        public const string AutoExposureGazeSpotWeightName = "autoexposure-gaze-spot-weight";
        public const string ZoomXName = "zoom-x";
        public const string ZoomYName = "zoom-y";
        public const string ZoomedName = "zoomed";
        public const string ZoomOnName = "zoom-on";
        public const string ZoomOffName = "zoom-off";
        private readonly RWProperty<float> _autoexposureChangeSpeed;
        private readonly RWProperty<float> _autoexposureGazeSpotSize;
        private readonly RWProperty<float> _autoexposureGazeSpotWeight;
        private readonly ROProperty<bool> _zoomed;
        private readonly ROProperty<float> _zoomX;
        private readonly ROProperty<float> _zoomY;

        public SceneCamera(G3Api api, string parentUrl) : base(api, $"{parentUrl}/scenecamera")
        {
            _autoexposureChangeSpeed = AddRWProperty(AutoExposureChangeSpeedName, ParserHelpers.ParseFloat, f => f.ToString(CultureInfo.InvariantCulture));
            _autoexposureGazeSpotSize = AddRWProperty(AutoExposureGazeSpotSizeName, ParserHelpers.ParseFloat, f => f.ToString(CultureInfo.InvariantCulture));
            _autoexposureGazeSpotWeight = AddRWProperty(AutoExposureGazeSpotWeightName, ParserHelpers.ParseFloat, f => f.ToString(CultureInfo.InvariantCulture));
            _zoomed = AddROProperty(ZoomedName, bool.Parse);
            _zoomX = AddROProperty(ZoomXName, ParserHelpers.ParseFloat);
            _zoomY = AddROProperty(ZoomYName, ParserHelpers.ParseFloat);

            Changed = AddSignal("changed", ConvertChanged);
        }

        private string ConvertChanged(List<JToken> arg)
        {
            return arg[0].Value<string>();
        }

        public IG3Observable<string> Changed { get; }


        public Task<float> AutoExposureChangeSpeed => _autoexposureChangeSpeed.Value();
        public Task<float> AutoExposureGazeSpotSize => _autoexposureGazeSpotSize.Value();
        public Task<float> AutoExposureGazeSpotWeight => _autoexposureGazeSpotWeight.Value();
        public Task<bool> Zoomed => _zoomed.Value();
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

        public async Task<ZoomOffResult> ZoomOff()
        {
            var res = await G3Api.ExecuteCommand<string>(Path, ZoomOffName, LogLevel.info);
            switch (res)
            {
                case "success": return ZoomOffResult.Success;
                default:
                    G3Api.Log(LogLevel.info, $"Unknown error message from {ZoomOffName}: {res}");
                    return ZoomOffResult.Unknown;
            }
        }
        public async Task<ZoomOnResult> ZoomOn(float normalizedXCenter, float normalizedYCenter)
        {
            var res = await G3Api.ExecuteCommand<string>(Path, ZoomOnName, LogLevel.info, normalizedXCenter, normalizedYCenter);
            switch (res)
            {
                case "success": return ZoomOnResult.Success;
                case "No action handler found for "+ZoomOnName: return ZoomOnResult.NoActionHandlerFound;
                case "fail-zoom-coordinates-out-of-range": return ZoomOnResult.CoordinatesOutOfRange;
                default:
                    G3Api.Log(LogLevel.info, $"Unknown error message from {ZoomOnName}: {res}");
                    return ZoomOnResult.Unknown;
            }
        }
    }

    public enum ZoomOnResult
    {
        Success,
        Unknown,
        NoActionHandlerFound,
        CoordinatesOutOfRange
    }

    public enum ZoomOffResult
    {
        Success,
        Unknown
    }
}