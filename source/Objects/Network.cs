using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public class Network : DynamicChildNode, INetwork
    {
        private readonly ROProperty<bool> _wifiHwEnabled;
        private readonly RWProperty<bool> _wifiEnable;

        public Network(G3Api g3Api) : base(g3Api, "network")
        {
            _wifiHwEnabled = AddROProperty("wifi-hw-enabled", bool.Parse);
            _wifiEnable = AddRWProperty_bool("wifi-enable");
            Wifi = new Wifi(G3Api, Path);
            Ethernet = new Ethernet(G3Api, Path);
        }

        public override async Task<IEnumerable<G3Object>> GetSDKChildren()
        {
            return await Task.FromResult(new G3Object[] { Wifi, Ethernet }.AsEnumerable());
        }

        public Task<bool> WifiHwEnabled => _wifiHwEnabled.Value();

        public Task<bool> WifiEnable => _wifiEnable.Value();

        public Task<bool> SetWifiEnable(bool value)
        {
            return _wifiEnable.Set(value);
        }

        public async Task Reset()
        {
            await G3Api.ExecuteCommand(Path, "reset", LogLevel.info);
        }

        public Wifi Wifi { get; }
        public Ethernet Ethernet { get; }

    }
    public interface INetwork
    {
        Task<bool> WifiHwEnabled { get; }
        Task<bool> WifiEnable { get; }
        Wifi Wifi { get; }
        Ethernet Ethernet { get; }
        Task<bool> SetWifiEnable(bool value);
        Task Reset();
    }

    public class NetworkInterface : DynamicChildNode
    {
        private readonly ROProperty<ConnectionState> _state;
        private readonly ROProperty<ConnectionStateReason> _stateReason;
        private readonly ROProperty<NetworkType> _type;
        private readonly ROProperty _macAddress;
        private readonly ROProperty _ipv4Address;
        private readonly ROProperty _ipv4Gateway;
        private readonly ROProperty<string[]> _ipv4NameServers;
        private readonly ROProperty<int> _speed;
        private readonly RWProperty<bool> _autoConnect;
        private readonly ROProperty _connectedNetwork;
        private readonly ROProperty<Guid> _activeConfiguration;
        private readonly ROProperty<string[]> _ipv6NameServers;
        private readonly ROProperty _ipv6Gateway;
        private readonly ROProperty _ipv6Address;

        public NetworkInterface(G3Api g3Api, string path) : base(g3Api, path)
        {
            _state = AddROProperty("state", s => ParserHelpers.ParseEnum(s, ConnectionState.Unknown));
            _stateReason = AddROProperty("state-reason", s => ParserHelpers.ParseEnum(s, ConnectionStateReason.Unknown));
            _type = AddROProperty("type", s => ParserHelpers.ParseEnum(s, NetworkType.Unknown));
            _macAddress = AddROProperty("mac-address");
            _ipv4Address = AddROProperty("ipv4-address");
            _ipv4Gateway = AddROProperty("ipv4-gateway");
            _ipv4NameServers = AddROProperty("ipv4-name-servers", JsonConvert.DeserializeObject<string[]>);
            _ipv6Address = AddROProperty("ipv6-address");
            _ipv6Gateway = AddROProperty("ipv6-gateway");
            _ipv6NameServers = AddROProperty("ipv6-name-servers", JsonConvert.DeserializeObject<string[]>);
            _speed = AddROProperty("speed", int.Parse);
            _autoConnect = AddRWProperty_bool("auto-connect");
            _activeConfiguration = AddROProperty("active-configuration", ParserHelpers.ParseGuid);
            _connectedNetwork = AddROProperty("connected-network");
            Connected = AddSignal("connected", list => new Notification());
            StateChange = AddSignal("state-change", ParseStateChange);
        }

        public IG3Observable<ConnectionState> StateChange { get; }

        private ConnectionState ParseStateChange(List<JToken> list)
        {
            return ParserHelpers.ParseEnum(list.First().Value<string>(), ConnectionState.Unknown);
        }

        public IG3Observable<Notification> Connected { get; set; }

        public Task<ConnectionState> State => _state.Value();
        public Task<ConnectionStateReason> StateReason => _stateReason.Value();
        public Task<NetworkType> Type => _type.Value();
        public Task<string> MacAddress => _macAddress.GetString();
        public Task<string> Ipv4Address => _ipv4Address.GetString();
        public Task<string> Ipv4Gateway => _ipv4Gateway.GetString();
        public Task<string[]> Ipv4NameServers => _ipv4NameServers.Value();
        public Task<string> Ipv6Address => _ipv6Address.GetString();
        public Task<string> Ipv6Gateway => _ipv6Gateway.GetString();
        public Task<string[]> Ipv6NameServers => _ipv6NameServers.Value();
        public Task<int> Speed => _speed.Value();
        public Task<bool> AutoConnect => _autoConnect.Value();
        public Task<Guid> ActiveConfiguration => _activeConfiguration.Value();
        public Task<string> ConnectedNetwork => _connectedNetwork.GetString();

        public Task<Guid> CreateConfig(string name)
        {
            return G3Api.ExecuteCommand<Guid>(Path, "create-config", LogLevel.info, name);
        }
        public Task<bool> Connect(Guid configId)
        {
            return G3Api.ExecuteCommand<bool>(Path, "connect", LogLevel.info, configId.ToString());
        }
        public Task Disconnect()
        {
            return G3Api.ExecuteCommand(Path, "disconnect", LogLevel.info);
        }
        public Task<bool> SetAutoConnect(bool value)
        {
            return _autoConnect.Set(value);
        }
    }

    public class NetworkConfigurations : DynamicChildNode
    {
        public NetworkConfigurations(G3Api g3Api, string path) : base(g3Api, $"{path}/configurations")
        {
        }
    }

    public class EthernetConfigurations : NetworkConfigurations
    {
        public EthernetConfigurations(G3Api g3Api, string path) : base(g3Api, path)
        {
        }

        public async Task<List<EthernetConfiguration>> Children()
        {
            var childIds = await GetChildren();
            var children = new List<EthernetConfiguration>();
            foreach (var child in childIds)
            {
                var uuid = Guid.Parse(child);
                children.Add(new EthernetConfiguration(G3Api, Path, uuid));
            }

            return children;
        }

        public override async Task<IEnumerable<G3Object>> GetSDKChildren()
        {
            return await Children();
        }
    }

    public class WifiConfigurations : NetworkConfigurations
    {
        public WifiConfigurations(G3Api g3Api, string path) : base(g3Api, path)
        {
        }

        public async Task<List<WifiConfiguration>> Children()
        {
            var childIds = await GetChildren();
            var children = new List<WifiConfiguration>();
            foreach (var child in childIds)
            {
                var uuid = Guid.Parse(child);
                children.Add(new WifiConfiguration(G3Api, Path, uuid));
            }

            return children;
        }

        public override async Task<IEnumerable<G3Object>> GetSDKChildren()
        {
            return await Children();
        }
    }

    public class NetworkConfiguration : G3Object
    {
        #region fields
        private readonly RWProperty<bool> _autoConnect;
        private readonly RWProperty<bool> _dhcpServerEnable;
        private readonly RWProperty<string> _id;
        private readonly RWProperty<string> _ipv4Address;
        private readonly RWProperty<string> _ipv4Gateway;
        private readonly RWProperty<string> _ipv4NameServers;
        private readonly RWProperty<Ipv4Method> _ipv4Method;
        private readonly RWProperty<string> _ipv6Address;
        private readonly RWProperty<string> _ipv6Gateway;
        private readonly RWProperty<string> _ipv6NameServers;
        private readonly RWProperty<Ipv6Method> _ipv6Method;
        private readonly RWProperty<int> _dhcpServerLeaseTime;
        private readonly RWProperty<string> _dhcpServerRangeHigh;
        private readonly RWProperty<string> _dhcpServerRangeLow;
        private readonly ROProperty<bool> _default;
        #endregion

        public NetworkConfiguration(G3Api g3Api, string path, Guid uuid) : base(g3Api, $"{path}/{uuid}")
        {
            _autoConnect = AddRWProperty_bool("autoconnect");
            _dhcpServerEnable = AddRWProperty_bool("dhcp-server-enable");
            _dhcpServerLeaseTime = AddRWProperty("dhcp-server-lease-time", int.Parse);

            _dhcpServerRangeHigh = AddRWProperty("dhcp-server-range-low");
            _dhcpServerRangeLow = AddRWProperty("dhcp-server-range-high");

            _default = AddROProperty("default", bool.Parse);
            _id = AddRWProperty("id");
            _ipv4Address = AddRWProperty("ipv4-address");
            _ipv4Gateway = AddRWProperty("ipv4-gateway");
            _ipv4NameServers = AddRWProperty("ipv4-name-servers");
            _ipv4Method = AddRWProperty("ipv4-method", ParserHelpers.Ipv4MethodParser);

            _ipv6Address = AddRWProperty("ipv6-address");
            _ipv6Gateway = AddRWProperty("ipv6-gateway");
            _ipv6NameServers = AddRWProperty("ipv6-name-servers");
            _ipv6Method = AddRWProperty("ipv6-method", ParserHelpers.Ipv6MethodParser);
        }

        #region properties
        public Task<bool> AutoConnect => _autoConnect.Value();
        public Task<bool> Default => _default.Value();
        public Task<bool> DhcpServerEnable => _dhcpServerEnable.Value();
        public Task<string> DhcpServerRangeLow => _dhcpServerRangeLow.Value();
        public Task<string> DhcpServerRangeHigh => _dhcpServerRangeHigh.Value();
        public Task<string> Id => _id.Value();
        public Task<string> Ipv4Address => _ipv4Address.Value();
        public Task<string> Ipv4Gateway => _ipv4Gateway.Value();
        public Task<string> Ipv4NameServers => _ipv4NameServers.Value();
        public Task<Ipv4Method> Ipv4Method => _ipv4Method.Value();
        public Task<string> Ipv6Address => _ipv6Address.Value();
        public Task<string> Ipv6Gateway => _ipv6Gateway.Value();
        public Task<string> Ipv6NameServers => _ipv6NameServers.Value();
        public Task<Ipv6Method> Ipv6Method => _ipv6Method.Value();
        #endregion

        #region Property setters
        public Task<bool> SetId(string value)
        {
            return _id.Set(value);
        }

        public Task<bool> SetAutoconnect(bool value)
        {
            return _autoConnect.Set(value);
        }
        public Task<bool> SetDhcpServerEnable(bool value)
        {
            return _dhcpServerEnable.Set(value);
        }
        public Task<bool> SetDhcpServerLeaseTime(int value)
        {
            return _dhcpServerLeaseTime.Set(value);
        }
        public Task<bool> SetDhcpServerRangeLow(string value)
        {
            return _dhcpServerRangeLow.Set(value);
        }
        public Task<bool> SetDhcpServerRangeHigh(string value)
        {
            return _dhcpServerRangeHigh.Set(value);
        }
        public Task<bool> SetIpv4Address(string value)
        {
            return _ipv4Address.Set(value);
        }
        public Task<bool> SetIpv4NameServers(string value)
        {
            return _ipv4NameServers.Set(value);
        }
        public Task<bool> SetIpv4Gateway(string value)
        {
            return _ipv4Gateway.Set(value);
        }
        public Task<bool> SetIpv4Method(Ipv4Method value)
        {
            return _ipv4Method.Set(value);
        }

        public Task<bool> SetIpv6Address(string value)
        {
            return _ipv6Address.Set(value);
        }
        public Task<bool> SetIpv6NameServers(string value)
        {
            return _ipv6NameServers.Set(value);
        }
        public Task<bool> SetIpv6Gateway(string value)
        {
            return _ipv6Gateway.Set(value);
        }
        public Task<bool> SetIpv6Method(Ipv6Method value)
        {
            return _ipv6Method.Set(value);
        }
        #endregion

        #region actions
        public async Task<bool> Save()
        {
            return await G3Api.ExecuteCommandBool(Path, "save", LogLevel.info);
        }
        public async Task<bool> Restore()
        {
            return await G3Api.ExecuteCommandBool(Path, "restore", LogLevel.info);
        }
        public async Task<bool> Delete()
        {
            return await G3Api.ExecuteCommandBool(Path, "delete", LogLevel.info);
        }

        #endregion

    }
    public class EthernetConfiguration : NetworkConfiguration
    {
        public EthernetConfiguration(G3Api g3Api, string path, Guid uuid) : base(g3Api, path, uuid)
        {
        }
    }

    public class WifiConfiguration : NetworkConfiguration
    {
        private readonly RWProperty<string> _ssid;
        private readonly RWProperty<string> _ssidName;
        private readonly RWProperty<bool> _accessPoint;
        private readonly RWProperty<int> _channel;
        private readonly RWProperty<string> _psk;
        private readonly RWProperty<WifiSecurity> _security;

        public WifiConfiguration(G3Api g3Api, string path, Guid uuid) : base(g3Api, path, uuid)
        {
            _accessPoint = AddRWProperty_bool("access-point");
            _ssid = AddRWProperty("ssid");
            _ssidName = AddRWProperty("ssid-name");
            _channel = AddRWProperty("channel", int.Parse);
            _psk = AddRWProperty("psk");
            _security = AddRWProperty("security", s => ParserHelpers.ParseEnum(s, WifiSecurity.unknown));
        }

        public Task<WifiSecurity> Security => _security.Value();
        public Task<string> Psk => _psk.Value();
        public Task<string> SsidName => _ssidName.Value();
        public Task<string> Ssid => _ssid.Value();
        public Task<int> Channel => _channel.Value();

        public async Task<byte[]> SsidAsBytes()
        {
            return Convert.FromBase64String(await Ssid);
        }

        public Task<bool> AccessPoint => _accessPoint.Value();

        public Task<bool> SetAccessPoint(bool value)
        {
            return _accessPoint.Set(value);
        }
        public Task<bool> SetPsk(string value)
        {
            return _psk.Set(value);
        }
        public Task<bool> SetSecurity(WifiSecurity value)
        {
            return _security.Set(value);
        }

        public Task<bool> SetSsidName(string value)
        {
            return _ssidName.Set(value);
        }

        public Task<bool> SetSsid(byte[] value)
        {
            var b64 = Convert.ToBase64String(value);
            if (b64 == "")
                b64 = null;
            return _ssid.Set(b64);
        }
        public Task<bool> SetChannel(int value)
        {
            return _channel.Set(value);
        }

    }

    public enum WifiSecurity
    {
        open, wpapsk, unknown
    }

    public enum NetworkType
    {
        Unknown,
        Wifi,
        Ethernet
    }

    public enum ConnectionStateReason
    {
        Unknown,
        None,
        ConfigFail,
        AuthFail,
        DhcpFail,
        ConfigIpFail,
        ConnectionRemoved,
        UserRequest,
        Carrier,
        AccessPointFail
    }

    public class Wifi : NetworkInterface
    {
        public Wifi(G3Api g3Api, string path) : base(g3Api, $"{path}/wifi")
        {
            Configurations = new WifiConfigurations(g3Api, Path);
        }
        public WifiConfigurations Configurations { get; }

        public async Task<bool> Scan()
        {
            return await G3Api.ExecuteCommandBool(Path, "scan", LogLevel.info);
        }
        public async Task<bool> ConnectNetwork(Guid g, string password)
        {
            return await G3Api.ExecuteCommandBool(Path, "connect-network", LogLevel.info, g.ToString(), password);
        }
    }

    public class Ethernet : NetworkInterface
    {
        public Ethernet(G3Api g3Api, string path) : base(g3Api, $"{path}/ethernet")
        {
            Configurations = new EthernetConfigurations(g3Api, Path);
        }

        public EthernetConfigurations Configurations { get; }

    }

    public enum ConnectionState
    {
        Unknown,
        Disconnected,
        Config,
        Auth,
        IpConfig,
        Connected,
        Disconnecting,
        Failed
    }

    public enum Ipv6Method
    {
        manual,
        automatic,
        dhcp,
        linklocal,
        ignore,
        unknown
    }

    public enum Ipv4Method
    {
        manual,
        dhcp,
        linklocal,
        disable,
        unknown
    }

}