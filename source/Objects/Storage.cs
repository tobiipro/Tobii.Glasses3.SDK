using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public class Storage: G3Object, IStorage
    {
        private readonly ROProperty<long> _free;
        private readonly ROProperty<long> _size;
        private readonly ROProperty<TimeSpan> _remainingTime;
        private readonly ROProperty<SpaceState> _spaceState;
        private readonly ROProperty<CardState> _cardState;
        private readonly ROProperty<bool> _busy;

        public Storage(G3Api g3Api, string rootUrl): base(g3Api, $"{rootUrl}/storage")
        {
            _free = AddROProperty("free", long.Parse);
            _size = AddROProperty("size", long.Parse);
            _remainingTime = AddROProperty("remaining-time", ParserHelpers.ParseTimeSpan);
            _spaceState = AddROProperty("space-state", ParserHelpers.ParseSpaceState);
            _cardState = AddROProperty("card-state", ParserHelpers.ParseCardState);
            _busy = AddROProperty("busy", bool.Parse);
            StateChanged = AddSignal("state-changed", ConvertStorageChange);
            BusyChanged = AddSignal("busy-changed", ParserHelpers.SignalToBool);
        }

        
        public Task<long> Free => _free.Value();
        public Task<long> Size => _size.Value();
        public Task<bool> Busy => _busy.Value();
        public Task<TimeSpan> RemainingTime => _remainingTime.Value();
        public Task<SpaceState> SpaceState => _spaceState.Value();
        public Task<CardState> CardState => _cardState.Value();

        public IG3Observable<(SpaceState spaceState, CardState cardState)> StateChanged { get; }
        public IG3Observable<bool> BusyChanged { get;  }

        private (SpaceState spaceState, CardState cardState) ConvertStorageChange(List<JToken> arg)
        {
            var spaceState = ParserHelpers.ParseSpaceState(arg[0].Value<string>());
            var cardState = ParserHelpers.ParseCardState(arg[1].Value<string>());
            return (spaceState, cardState);
        }
    }

    public interface IStorage: IG3Object
    {
        Task<long> Free { get; }
        Task<long> Size { get; }
        Task<bool> Busy { get; }
        Task<TimeSpan> RemainingTime { get; }
        Task<SpaceState> SpaceState { get; }
        Task<CardState> CardState { get; }
        IG3Observable<(SpaceState spaceState, CardState cardState)> StateChanged { get; }
        IG3Observable<bool> BusyChanged { get; }
    }
}