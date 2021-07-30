using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public class Upgrade: G3Object, IUpgrade
    {
        private readonly ROProperty<bool> _inProgress;
        public Upgrade(G3Api g3Api): base(g3Api, "upgrade")
        {
            _inProgress = AddROProperty( "in-progress", bool.Parse);
            Progress = AddSignal("progress", ConvertProgress);
            Completed = AddSignal("completed", ParserHelpers.SignalToBool);
        }

        public IG3Observable<bool> Completed { get; }

        public IG3Observable<UpgradeState> Progress { get; }

        private UpgradeState ConvertProgress(List<JToken> arg)
        {
            var uploadProgress = arg[0].Value<float>();
            var upgradeProgress = arg[1].Value<float>();
            return new UpgradeState(uploadProgress, upgradeProgress);
        }

        public Task<bool> InProgress => _inProgress.Value();
    }

    public interface IUpgrade
    {
        IG3Observable<bool> Completed { get; }
        IG3Observable<UpgradeState> Progress { get; }
        Task<bool> InProgress { get; }
    }


    public class UpgradeState
    {
        public float UploadProgress { get; }
        public float UpgradeProgress { get; }

        public UpgradeState(float uploadProgress, float upgradeProgress)
        {
            UploadProgress = uploadProgress;
            UpgradeProgress = upgradeProgress;
        }
    }
}