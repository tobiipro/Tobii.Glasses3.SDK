using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;

namespace G3SDK
{
    public class G3TestBase
    {
        protected G3Version FwVersion { get; private set; }
        protected IG3Api G3Api { get; private set; }
        protected async Task EnsureApi()
        {
            if (G3Api != null)
                return;
            var browser = new G3Browser();
            var devices = await browser.ProbeForDevices();
            var units = Environment.GetEnvironmentVariable("G3UNITS")?.ToUpper().Split(',');
            var validDevices = new Dictionary<string, G3Api>();
            foreach (var d in devices)
            {
                var serial = (await d.System.RecordingUnitSerial).ToUpper();
                if (units == null || units.Length == 0 || units.Any(s => serial.Contains(s.Trim())))
                {
                    // devices that are connected via multiple network interfaces only need to be added once
                    if (!validDevices.ContainsKey(serial)) 
                        validDevices[serial] = d;
                }
            }

            if (validDevices.Count == 1)
                G3Api = validDevices.Values.First();
            else if (validDevices.Count == 0)
                G3Api = new G3Simulator.G3Simulator();
            else
                throw new Exception("more than one device found, aborted");


            FwVersion = new G3Version(await G3Api.System.Version);

            var inProgress = await G3Api.Recorder.RecordingInProgress();
            if (inProgress)
            {
                await G3Api.Recorder.Cancel();
                Assert.That(await G3Api.Recorder.RecordingInProgress(), Is.False.After(200, 50), "Recording is still in progress, can't start test");
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