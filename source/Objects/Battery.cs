using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public class Battery: G3Object, IBattery
    {
        private readonly ROProperty<float> _level;
        private readonly ROProperty<TimeSpan> _remainingTime;
        private readonly ROProperty<bool> _charging;
        private readonly ROProperty<BatteryState> _state;

        public Battery(G3Api api, string parentUrl): base(api, $"{parentUrl}/battery")
        {
            _level = AddROProperty( "level", s=>float.Parse(s, CultureInfo.InvariantCulture));
            _remainingTime = AddROProperty("remaining-time", s => TimeSpan.FromSeconds(int.Parse(s)));
            _charging = AddROProperty("charging", bool.Parse);
            _state = AddROProperty("state", ParserHelpers.ParseBatteryState);

            StateChanged = AddSignal("state-changed", ConvertBatteryChanged);

        }
        private (BatteryState, bool) ConvertBatteryChanged(List<JToken> arg)
        {
            var batteryState = ParserHelpers.ParseBatteryState(arg[0].Value<string>());
            var charging = bool.Parse(arg[1].Value<string>());
            return (batteryState, charging);
        }


        #region Properties
        public Task<float> Level => _level.Value();
        public Task<TimeSpan> RemainingTime => _remainingTime.Value();
        public Task<bool> Charging => _charging.Value();
        public Task<BatteryState> State => _state.Value();
        #endregion

        #region Signals
        public IG3Observable<(BatteryState State, bool Charging)> StateChanged { get; }
        #endregion


    }

    public interface IBattery: IG3Object
    {
        Task<float> Level { get; }
        Task<TimeSpan> RemainingTime { get; }
        Task<bool> Charging { get; }
        Task<BatteryState> State { get; }
        IG3Observable<(BatteryState State, bool Charging)> StateChanged { get; }
    }

    public enum BatteryState
    {
        full, good, low, verylow, unknown
    }
}