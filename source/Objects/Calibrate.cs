using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public class Calibrate: G3Object, ICalibrate
    {
        public Calibrate(G3Api g3Api): base(g3Api, "calibrate")
        {
            Marker = AddSignal("marker", BodyToCalibrationMarker);
        }

        private G3MarkerData BodyToCalibrationMarker(List<JToken> args)
        {
            if (args.Count != 3)
                return null;
            var timestamp = args[0].Value<float>();
            var marker3D = (args[1] as JArray).Arr2Vector3();
            var marker2D = (args[2] as JArray).Arr2Vector2();
            var res = new G3MarkerData(TimeSpan.FromSeconds(timestamp), marker2D, marker3D);
            return res;
        }

        #region Signals

        public IG3Observable<G3MarkerData> Marker { get; }

        #endregion

        #region Commands

        public async Task<bool> EmitMarkers()
        {
            return await G3Api.ExecuteCommandBool(Path, "emit-markers", LogLevel.info);
        }
        public async Task<bool> Run()
        {
            return await G3Api.ExecuteCommandBool(Path, "run", LogLevel.info);
        }

        #endregion
    }
}