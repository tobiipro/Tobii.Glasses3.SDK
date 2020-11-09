using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace G3SDK
{
    public class G3TestBase
    {
        protected G3Version FwVersion { get; private set; }
        protected G3Api G3Api { get; private set; }
        protected async Task EnsureApi()
        {
            if (G3Api != null)
                return;
            var browser = new G3Browser();
            var devices = await browser.ProbeForDevices();
            Assert.IsNotEmpty(devices, "no G3 device found");
            G3Api = devices.First();
            FwVersion = new G3Version(await G3Api.System.Version);

            var inProgress = await G3Api.Recorder.RecordingInProgress();
            if (inProgress)
            {
                await G3Api.Recorder.Cancel();
                inProgress = await G3Api.Recorder.RecordingInProgress();
                Assert.False(inProgress, "Recording is still in progress, can't start test");
            }
        }

        [SetUp]
        public void Setup()
        {
            G3Api = null;
        }

        [TearDown]
        public async Task TearDown()
        {
            if (G3Api != null && await G3Api.Recorder.RecordingInProgress())
                await G3Api.Recorder.Cancel();
        }
    }
}