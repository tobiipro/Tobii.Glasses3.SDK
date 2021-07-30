using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace G3SDK
{
    public class Settings : G3Object, ISettings
    {
        private readonly RWProperty<bool> _gazeOverlay;
        private readonly RWProperty<int> _gazeFrequency;
        private readonly StringEnumConverter _converter = new StringEnumConverter();
        private readonly JsonSerializer x = JsonSerializer.Create();

        
        private string ConvertJsonEnumToString(object value)
        {
            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            using (var w = new JsonTextWriter(sw))
            {
                _converter.WriteJson(w, value, x);
            }
            return sb.ToString();
        }

        public Settings(G3Api g3Api) : base(g3Api, "settings")
        {
            _gazeOverlay = AddRWProperty_bool("gaze-overlay");
            _gazeFrequency = AddRWProperty("gaze-frequency", int.Parse);
            Changed = AddSignal("changed", ParserHelpers.SignalToString);
        }

        public IG3Observable<string> Changed { get; }

        public Task<bool> GazeOverlay => _gazeOverlay.Value();

        public Task<bool> SetGazeOverlay(bool value)
        {
            return _gazeOverlay.Set(value);
        }
        public Task<int> GazeFrequency => _gazeFrequency.Value();

        public Task<bool> SetGazeFrequency(int value)
        {
            return _gazeFrequency.Set(value);
        }
    }

    public interface ISettings
    {
        IG3Observable<string> Changed { get; }
        Task<bool> GazeOverlay { get; }
        Task<int> GazeFrequency { get; }
        Task<bool> SetGazeOverlay(bool value);
        Task<bool> SetGazeFrequency(int value);
    }
}