using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public interface IHeadUnit : IG3Object
    {
        IG3Observable<HuConnectionState> ConnectionStateChanged { get; }

        Task<HuConnectionState> ConnectionState { get; }
    }

    public class HeadUnit : G3Object, IHeadUnit
    {
        private readonly ROProperty<HuConnectionState> _connectionState;

        public HeadUnit(G3Api api, string parentUrl): base(api, $"{parentUrl}/headunit")
        {
            _connectionState = AddROProperty("connection-state", s => ParserHelpers.ParseHuConnectionState(s));
            ConnectionStateChanged = AddSignal("state-changed", ConvertConnectionStateChanged);
        }

        public IG3Observable<HuConnectionState> ConnectionStateChanged { get; }

        public Task<HuConnectionState> ConnectionState => _connectionState.Value();

        private HuConnectionState ConvertConnectionStateChanged(List<JToken> arg)
        {
            return ParserHelpers.ParseHuConnectionState(arg[0].Value<string>());
        }
    }
    public enum HuConnectionState
    {
        connected, disconnected, unknown
    }
}