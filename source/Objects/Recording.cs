using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public class Recording : G3Object, IMetaDataCapable
    {
        private readonly ROProperty _httpPath;
        private readonly ROProperty _rtspPath;
        private readonly ROProperty _folder;
        private readonly RWProperty<string> _visibleName;
        private readonly ROProperty<TimeSpan> _duration;
        private readonly ROProperty<DateTime> _created;
        private readonly ROProperty<int> _validGazeSamples;
        private readonly ROProperty<int> _gazeSamples;
        private readonly ROProperty _timezone;
        private readonly ROProperty<bool> _gazeOverlay;

        public Recording(G3Api g3api, string parentUrl, Guid uuid) : base(g3api, $"{parentUrl}/{uuid}")
        {
            UUID = uuid;
            _folder = AddROProperty("folder");
            _visibleName = AddRWProperty("visible-name");
            _duration = AddROProperty("duration", ParserHelpers.ParseTimeSpan);
            _created = AddROProperty("created", ParserHelpers.ParseDate);
            _gazeSamples = AddROProperty("gaze-samples", int.Parse);
            _validGazeSamples = AddROProperty("valid-gaze-samples", int.Parse);
            _timezone = AddROProperty("timezone");
            _gazeOverlay = AddROProperty("gaze-overlay", bool.Parse);


            _httpPath = AddROProperty("http-path");
            _rtspPath = AddROProperty("rtsp-path");
        }

        #region Properties
        public Task<string> Folder => _folder.GetString();
        public Task<string> VisibleName => _visibleName.Value();
        public Task<string> TimeZone => _timezone.GetString();
        public Task<bool> GazeOverlay => _gazeOverlay.Value();

        public Task<bool> SetVisibleName(string value)
        {
            return _visibleName.Set(value);
        }

        public Task<DateTime> Created => _created.Value();
        public Task<TimeSpan> Duration => _duration.Value();
        public Task<int> GazeSamples => _gazeSamples.Value();
        public Task<int> ValidGazeSamples => _validGazeSamples.Value();
        public Task<string> HttpPath => _httpPath.GetString();
        public Task<string> RtspPath => _rtspPath.GetString();
        public Guid UUID { get; }
        #endregion

        #region Actions

        public async Task<bool> Move(string folderName)
        {
            return await G3Api.ExecuteCommandBool(Path, "move", LogLevel.info, folderName);
        }

        public async Task<bool> MetaInsert(string key, string value)
        {
            return await MetaDataCapableExtensions.MetaInsert(this, key, value);
        }
        public async Task<bool> MetaInsert(string key, byte[] data)
        {
            return await MetaDataCapableExtensions.MetaInsert(this, key, data);
        }

        public async Task<string[]> MetaKeys()
        {
            return await MetaDataCapableExtensions.MetaKeys(this);
        }

        public async Task<string> MetaLookupString(string key)
        {
            return await MetaDataCapableExtensions.MetaLookupString(this, key);
        }
        public async Task<byte[]> MetaLookup(string key)
        {
            return await MetaDataCapableExtensions.MetaLookup(this, key);
        }

        #endregion

        public async Task<List<G3GazeData>> GazeData()
        {
            var gazeFilePath = await GazeFilePath();

            using (var compressedData = await G3Api.GetRequestStream(gazeFilePath, "gzip"))
            {
                return ParserHelpers.ParseGazeDataFromCompressedStream(compressedData);
            }
        }

        public async Task<(ConcurrentQueue<G3GazeData>, Task)> GazeDataAsync()
        {
            var gazeFilePath = await GazeFilePath();

            var res = new ConcurrentQueue<G3GazeData>();
            var t = Task.Run(() =>
                {
                    using (var response = G3Api.GetWebResponse(gazeFilePath, "gzip"))
                    using (var compressedData = response.GetResponseStream())
                        ParserHelpers.ParseGazeDataFromCompressedStream(compressedData, res);
                });

            return (res, t);
        }

        private async Task<string> GazeFilePath()
        {
            var httpPath = await HttpPath;
            var recordingJson = await G3Api.GetRequest(httpPath);
            var json = (JObject)JsonConvert.DeserializeObject(recordingJson);
            var gazeNode = json["gaze"];
            var gazeFileNode = gazeNode["file"];
            var gazeFileName = gazeFileNode.Value<string>();
            var gazeFilePath = $"{httpPath}/{gazeFileName}";
            return gazeFilePath;
        }

        public async Task<Uri> GetUri(string fileName)
        {
            return new Uri($"http://{G3Api.IpAddress}{await HttpPath}/{fileName}");
        }
    }
}