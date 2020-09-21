﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    [DebuggerDisplay("{Path}")]
    public class G3Object: IG3Object
    {
        private readonly HashSet<ISignal> _signals = new HashSet<ISignal>();
        private readonly HashSet<ROProperty> _readOnlyProperties = new HashSet<ROProperty>();
        private readonly HashSet<ROProperty> _readWriteProperties = new HashSet<ROProperty>();
        private G3ObjectDescription _desc;
        public G3Api G3Api { get; }
        public string Path { get; }

        public G3Object(G3Api g3Api, string path)
        {
            G3Api = g3Api;
            Path = path;
            AddROProperty("name");
        }

        protected ROProperty<T> AddROProperty<T>(string propName, Func<string, T> convert)
        {
            var prop = new ROProperty<T>(G3Api, Path, propName, convert);
            _readOnlyProperties.Add(prop.Prop);
            return prop;
        }
        protected RWProperty<string> AddRWProperty(string propName)
        {
            return AddRWProperty(propName, s=>s);
        }

        protected RWProperty<bool> AddRWProperty_bool(string propName)
        {
            return AddRWProperty(propName, bool.Parse, b => b.ToString().ToLower());
        }

        protected RWProperty<T> AddRWProperty<T>(string propName, Func<string, T> parse, Func<T, string> toString = null)
        {
            var prop = new RWProperty<T>(G3Api, Path, propName, parse, toString);
            _readWriteProperties.Add(prop.Prop);
            return prop;
        }

        protected ROProperty AddROProperty(string propName)
        {
            var prop = new ROProperty(G3Api, Path, propName);
            _readOnlyProperties.Add(prop);
            return prop;
        }

        protected IG3Observable<T> AddSignal<T>(string signalName, Func<List<JToken>, T> bodyTranslator)
        {
            var s = G3Api.SignalHandler.CreateSignal(Path, signalName, bodyTranslator);
            _signals.Add(s as ISignal);
            return s;
        }

        private async Task EnsureDesc()
        {
            if (_desc == null)
            {
                var doc = await G3Api.GetRestRequest($"{Path}?help=true");
                _desc = JsonConvert.DeserializeObject<G3ObjectDescription>(doc);
            }
        }

        public async Task ValidateApi(List<string> warnings)
        {
            await EnsureDesc();
            foreach (var s in _signals)
            {
                if (!_desc.signals.TryGetValue(s.SignalName, out var signalDesc))
                    warnings.Add($"Unknown signal name registered: {Path}:{s.SignalName}");
            }
            
            foreach (var signalName in _desc.signals.Keys)
            {
                if (_signals.All(s => s.SignalName != signalName))
                    warnings.Add($"Unimplemented signal! {Path}:{signalName}");
            }

            foreach (var p in _readOnlyProperties)
            {
                if (!_desc.properties.TryGetValue(p.PropName, out var propDesc))
                    warnings.Add($"Unknown RO-property name registered: {Path}.{p.PropName}");
                else
                {
                    if (propDesc.IsReadWrite)
                        warnings.Add($"ReadOnly property registered for a R/W property: {Path}.{p.PropName}");
                }
            }

            foreach (var p in _readWriteProperties)
            {
                if (!_desc.properties.TryGetValue(p.PropName, out var propDesc))
                    warnings.Add($"Unknown RW-property name registered: {Path}.{p.PropName}");
                else
                {
                    if (propDesc.IsReadOnly)
                        warnings.Add($"ReadWrite property registered for a ReadOnly property: {Path}.{p.PropName}");
                }

                var m = GetType().GetMethod("Set" + CamelCase(p.PropName));
                if (m == null)
                    warnings.Add($"ReadWrite property has no set-method: {Path}.{p.PropName}");
            }

            foreach (var propertyName in _desc.properties.Keys)
            {
                if (_readOnlyProperties.All(p => p.PropName != propertyName) &&
                    _readWriteProperties.All(p => p.PropName != propertyName))
                    warnings.Add($"Unimplemented property! {Path}.{propertyName}");
            }

            foreach (var actionName in _desc.actions.Keys)
            {
                var methods = GetType().GetMethods().Where(m=>m.Name == CamelCase(actionName));
                if (!methods.Any())

                {
                    var action = _desc.actions[actionName];
                    
                    warnings.Add($"Unimplemented action! {Path}.{actionName}({string.Join(", ", action.Args)}): {action.Return}");
                }
            }
        }

        private string CamelCase(string name)
        {
            var parts = name.Split('-');
            return string.Join("", parts.Select(p => p.Substring(0, 1).ToUpper() + p.Substring(1)));
        }

        public async Task<JObject> GetDocJson()
        {
            var json = await G3Api.GetRestRequest(Path + "?help=true");
            var obj = JObject.Parse(json);
            return obj;
        }

        public async Task<IEnumerable<G3Object>> AllChildren()
        {
            var res = new List<G3Object>();
            var obj = await GetDocJson();
            var children = obj.GetValue("children");

            if (children is JArray array)
            {
                for (int i=0; i<array.Count; i++)
                    res.Add(new G3Object(G3Api, Path+"/"+array[i].Value<string>()));
                res.Sort((o1, o2) => o1.Path.CompareTo(o2.Path) );
            }
            else
            {
                
            }
            return res;
        }
    }

    public class G3ObjectDescription
    {
        public Dictionary<string, G3PropertyDescription> properties { get; set; }
        public Dictionary<string, G3ActionDescription> actions { get; set; }
        public Dictionary<string, G3SignalDescription> signals { get; set; }
    }

    public class G3SignalDescription
    {
        public object[] args { get; set; }
        public string help { get; set; }
    }

    public class G3ActionDescription
    {
        [JsonProperty(PropertyName = "args")]
        public string[] Args { get; set; }

        [JsonProperty(PropertyName = "return")]
        public string Return { get; set; }

        [JsonProperty(PropertyName = "help")]
        public string Help { get; set; }
    }

    public class G3PropertyDescription
    {
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }
        [JsonProperty(PropertyName = "range")]
        public object[] Range { get; set; }
        [JsonProperty(PropertyName = "mode")]
        public string Mode { get; set; }
        [JsonProperty(PropertyName = "help")]
        public string Help { get; set; }
        public bool IsReadOnly => Mode == "r";
        public bool IsReadWrite => Mode == "rw";
    }

    public interface IG3Object
    {
        G3Api G3Api { get; }
        string Path { get; }
    }
}