using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace G3SDK
{
    public class Settings : G3Object
    {
        private readonly RWProperty<GazeOverlay> _gazeOverlay;
        private readonly RWProperty<GazeFrequency> _gazeFrequency;
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
            _gazeOverlay = AddRWProperty("gaze-overlay", s => ParserHelpers.ParseEnum(s, G3SDK.GazeOverlay.Default), v => ConvertJsonEnumToString(v));
            _gazeFrequency = AddRWProperty("gaze-frequency", ParserHelpers.ConvertGazeFrequencyFromString, v => ConvertJsonEnumToString(v));
            Changed = AddSignal("changed", ParserHelpers.SignalToString);
        }

        public IG3Observable<string> Changed { get; set; }

        public Task<GazeOverlay> GazeOverlay => _gazeOverlay.Value();

        public Task<bool> SetGazeOverlay(GazeOverlay value)
        {
            return _gazeOverlay.Set(value);
        }
        public Task<GazeFrequency> GazeFrequency => _gazeFrequency.Value();

        public Task<bool> SetGazeFrequency(GazeFrequency value)
        {
            return _gazeFrequency.Set(value);
        }
    }

}