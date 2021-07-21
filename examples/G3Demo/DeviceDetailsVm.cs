using System.Threading.Tasks;
using System.Windows.Threading;
using G3SDK;
using Zeroconf;

namespace G3Demo
{
    public class DeviceDetailsVm: ViewModelBase
    {
        private readonly IZeroconfHost _zeroconfHost;
        private G3Api _g3;
        private bool _selected;
        public string Serial { get; private set; }

        public DeviceDetailsVm(IZeroconfHost zeroconfHost, Dispatcher dispatcher): base(dispatcher)
        {
            _zeroconfHost = zeroconfHost;
        }

        public string Id => _zeroconfHost.Id;

        public bool Selected
        {
            get => _selected;
            set
            {

                if (value == _selected) return;
                _selected = value;
                OnPropertyChanged();
            }
        }

        public LiveViewVM CreateLiveViewVM()
        {
            return new LiveViewVM(_g3, Dispatcher);
        }

        public async Task Init()
        {
            _g3 = new G3Api(_zeroconfHost.IPAddress);
            Serial = await _g3.System.RecordingUnitSerial;
        }
    }
}