using System;
using System.Threading.Tasks;

namespace G3SDK
{
    public class ROProperty
    {
        public G3Api Api { get; }
        public string Path { get; }
        public string PropName { get; }

        public ROProperty(G3Api g3Api, string path, string propName)
        {
            Api = g3Api;
            Path = path;
            PropName = propName;
        }
        public async Task<string> GetString()
        {
            var res = await Api.GetProperty(Path, PropName);
            if (res == "null")
                return null;

            if (res.StartsWith("\""))
                res = res.Substring(1);
            if (res.EndsWith("\""))
                res = res.Substring(0, res.Length - 1);
            return res;
        }
    }

    public class ROProperty<T>
    {
        protected internal ROProperty Prop { get; }
        private readonly Func<string, T> _convert;

        public ROProperty(G3Api g3Api, string path, string propName, Func<string, T> convert)
        {
            _convert = convert;
            Prop = new ROProperty(g3Api, path, propName);
        }

        public async Task<T> Value()
        {
            var value = await Prop.GetString();
            try
            {
                return _convert(value);
            }
            catch (FormatException e)
            {
                throw new FormatException($"Failed to parse [{value}] into a {typeof(T).Name}", e);
            }
        }
    }

    public class RWProperty<T> : ROProperty<T>
    {
        private readonly Func<T, string> _toString;

        public RWProperty(G3Api g3Api, string path, string propName, Func<string, T> parse, Func<T, string> toString = null) : base(g3Api, path, propName, parse)
        {
            _toString = toString;
        }

        public async Task<bool> Set(T value)
        {
            var s = _toString != null ? _toString(value) : value.ToString();
            return await Prop.Api.SetProperty(Prop.Path, Prop.PropName, LogLevel.info, s);
        }
    }
}