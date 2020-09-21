using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;

namespace G3SDK
{
    [TestFixture]
    class G3ApiTests
    {
        private G3Api _api;

        [SetUp]
        public void Setup()
        {
            _api = null;
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_api != null && await _api.Recorder.RecordingInProgress())
                await _api.Recorder.Cancel();
        }

        private async Task EnsureApi()
        {
            if (_api != null)
                return;
            var browser = new G3Browser();
            var devices = await browser.ProbeForDevices();
            Assert.IsNotEmpty(devices, "no G3 device found");
            _api = devices.First();
            var inProgress = await _api.Recorder.RecordingInProgress();
            if (inProgress)
            {
                await _api.Recorder.Cancel();
                inProgress = await _api.Recorder.RecordingInProgress();
                Assert.False(inProgress, "Recording is still in progress, can't start test");
            }

        }

        [Test]
        public async Task StoragePropertiesCanBeRead()
        {
            await EnsureApi();
            var sdCardState = await _api.System.Storage.CardState;
            var free = await _api.System.Storage.Free;
            var size = await _api.System.Storage.Size;
            var remainingTime = await _api.System.Storage.RemainingTime;
            var spaceState = await _api.System.Storage.SpaceState;
            if ((sdCardState & CardState.Available) != 0)
            {
                Assert.That(remainingTime.TotalSeconds, Is.Positive, "remainingTime");
                Assert.That(free, Is.Positive, "free");
                Assert.That(size, Is.Positive, "size");
            }
        }

        [Test]
        public async Task UpgradePropertiesCanBeRead()
        {
            await EnsureApi();
            var running = await _api.Upgrade.InProgress;

            VerifySignal(_api.Upgrade.Completed, "upgrade.completed");
            VerifySignal(_api.Upgrade.Progress, "upgrade.progress");
        }

        private void VerifySignal<T>(IG3Observable<T> signal, string signalName)
        {
            using (var token = signal.Subscribe(value => { }))
            {
                Assert.That(() => signal.IsSubscribed, Is.True.After(1000), $"Failed to subscribe to {signalName}");
            }
        }

        [Test]
        public async Task RecordingsPropertiesCanBeRead()
        {
            await EnsureApi();

            VerifySignal(_api.Recordings.ScanStart, "recordings.scanstart");
            VerifySignal(_api.Recordings.ScanDone, "recordings.scandone");
        }

        [Test]
        [Ignore("Test takes a long time")]
        public async Task WebsocketLives()
        {
            await EnsureApi();
            var gazeCounter = 0;
            await _api.Rudimentary.Gaze.SubscribeAsync(g =>
            {
                gazeCounter++;
            });
            while (gazeCounter == 0)
            {
                await _api.Rudimentary.Keepalive();
                Task.Delay(500);
            }
            var counter = 0;

            while (counter < 500)
            {
                await _api.Rudimentary.Keepalive();
                await Task.Delay(1000);
                Assert.That(gazeCounter, Is.GreaterThan(0));
                Console.WriteLine($"Counter: {counter} Gaze: {gazeCounter}");
                gazeCounter = 0;
                counter++;
            }
        }

        [Test]
        public async Task NtpTimeIsGood()
        {
            await EnsureApi();

            var ntpEnabled = await _api.System.NtpIsEnabled;
            if (!ntpEnabled)
            {
                await _api.System.UseNtp(true);
                ntpEnabled = await _api.System.NtpIsEnabled;
                Assert.That(ntpEnabled, Is.EqualTo(true), "ntp was not enabled");
            }

            var ntpSynced = await _api.System.NtpIsSynchronized;

            var before = DateTime.Now;

            var time = await _api.System.Time;
            var after = DateTime.Now;
            var localTime = time.ToLocalTime();
            Console.WriteLine($"Ntp synced.: {ntpSynced}");

            Console.WriteLine($"Roundtrip......: {(after - before).TotalMilliseconds}ms");
            Console.WriteLine($"Before.........: {before:O} {before.Kind} ");
            Console.WriteLine($"RUTime (parsed.: {time:O} {time.Kind}");
            Console.WriteLine($"RUTime (local).: {localTime:O} {localTime.Kind} (diff: {(localTime - before).TotalMilliseconds}ms)");
            Console.WriteLine($"After..........: {after:O} {after.Kind} (diff: {(after - localTime).TotalMilliseconds}ms)");
            Assert.That(localTime, Is.GreaterThan(before.AddSeconds(-1)), "RUTimeIsTooEarly");
            Assert.That(localTime, Is.LessThan(before.AddSeconds(1)), "RUTimeIsTooLate");

        }

        [Test]
        public async Task SystemPropertiesCanBeRead()
        {
            await EnsureApi();
            var ruSerial = await _api.System.RecordingUnitSerial;
            var huSerial = await _api.System.HeadUnitSerial;
            var version = await _api.System.Version;
            var tz = await _api.System.TimeZone;
            var time = await _api.System.Time;
            Assert.That(ruSerial, Is.Not.Empty);
            Assert.That(huSerial, Is.Not.Empty);
            Assert.That(version, Is.Not.Empty);
            Assert.That(tz, Is.Not.Empty);
            Assert.That(time, Is.GreaterThan(new DateTime(2020, 01, 01)));
        }

        [Test]
        public async Task WebRtcObjectsCanBeInteractedWith()
        {
            await EnsureApi();
            var session = await _api.WebRTC.Create();
            await session.SetIframeStream(true);
            await _api.WebRTC.Delete(session);
        }

        [Test]
        public async Task RecorderPropertiesCanBeRead()
        {
            await EnsureApi();
            var folder = await _api.Recorder.Folder;
            var visibleName = await _api.Recorder.VisibleName;
            var duration = await _api.Recorder.Duration;
            var created = await _api.Recorder.Created;
            var uuid = await _api.Recorder.UUID;
            var timeZone = await _api.Recorder.TimeZone;
            var gazeSamples = await _api.Recorder.GazeSamples;
            var validGazeSamples = await _api.Recorder.ValidGazeSamples;
            var remainingTime = await _api.Recorder.RemainingTime;
            Assert.That(gazeSamples, Is.GreaterThanOrEqualTo(-1));
            Assert.That(validGazeSamples, Is.GreaterThanOrEqualTo(-1));
            Assert.That(remainingTime.TotalSeconds, Is.Not.Negative);
            Assert.That(visibleName, Is.Not.Empty);
            Assert.That(timeZone, Is.Not.Empty);
        }

        private class payload
        {
            public string str;
            public int num;
        }

        [Test]
        public async Task EventsCanBeRecorded()
        {
            await EnsureApi();
            //            await _api.Recorder.Start();

            await _api.Rudimentary.Keepalive();
            var eventCount = 0;
            var token = _api.Rudimentary.Event.Subscribe(e =>
            {
                Assert.That(e.Tag, Is.EqualTo("tag1"));
                var obj = JsonConvert.DeserializeObject<payload>(e.Obj);
                Assert.That(obj.num, Is.EqualTo(123));
                Assert.That(obj.str, Is.EqualTo("abc"));

                eventCount++;
            });

            var res = await _api.Rudimentary.SendEvent("tag1", new payload() { num = 123, str = "abc" });
            Assert.True(res, "send-event failed");

            Thread.Sleep(500);

            var sample = await _api.Rudimentary.EventSample;
            Assert.That(sample.Tag, Is.EqualTo("tag1"));
            var sampleObj = JsonConvert.DeserializeObject<payload>(sample.Obj);
            Assert.That(sampleObj.num, Is.EqualTo(123));
            Assert.That(sampleObj.str, Is.EqualTo("abc"));

            Thread.Sleep(1000);
            Assert.That(eventCount, Is.EqualTo(1));
            token.Dispose();


        }


        [Test]
        public async Task ValidateAPI()
        {
            await EnsureApi();
            var warnings = new System.Collections.Generic.List<string>();
            await _api.System.ValidateApi(warnings);
            await _api.System.Storage.ValidateApi(warnings);
            await _api.System.Battery.ValidateApi(warnings);

            await _api.WebRTC.ValidateApi(warnings);

            var session = await _api.WebRTC.Create();
            await session.ValidateApi(warnings);
            Thread.Sleep(1000);
            await _api.WebRTC.Delete(session);


            await _api.Recorder.ValidateApi(warnings);
            await _api.Recordings.ValidateApi(warnings);
            var recordings = await _api.Recordings.Children();
            if (recordings.Any())
                await recordings.First().ValidateApi(warnings);

            await _api.Upgrade.ValidateApi(warnings);

            await _api.Network.ValidateApi(warnings);
            await _api.Network.Wifi.ValidateApi(warnings);
            var wifiConfigs = await _api.Network.Wifi.Configurations.Children();
            if (wifiConfigs.Any())
                await wifiConfigs.First().ValidateApi(warnings);

            await _api.Network.Ethernet.ValidateApi(warnings);
            var ethernetConfigs = await _api.Network.Ethernet.Configurations.Children();
            if (ethernetConfigs.Any())
                await ethernetConfigs.First().ValidateApi(warnings);

            await _api.Calibrate.ValidateApi(warnings);
            await _api.Rudimentary.ValidateApi(warnings);
            foreach (var w in warnings)
                Console.WriteLine(w);
            Assert.IsEmpty(warnings, $"Api warnings: " +
                                     Environment.NewLine + "=====" +
                                     Environment.NewLine + string.Join(Environment.NewLine, warnings) +
                                     Environment.NewLine + "=====");

        }

        [Test]
        public async Task BatteryPropertiesCanBeRead()
        {
            await EnsureApi();
            var remainingTime = await _api.System.Battery.RemainingTime;
            var charging = await _api.System.Battery.Charging;
            var level = await _api.System.Battery.Level;
            var state = await _api.System.Battery.State;
            Assert.That(remainingTime.TotalSeconds, Is.Positive, "remainingTime");
            Assert.That(level, Is.Positive, "level");
        }

        [Test]
        public async Task CanSetAndReadMeta()
        {
            await EnsureApi();
            await EnsureSDCard();
            var uniqueString = Guid.NewGuid().ToString();
            var res = await _api.Recorder.Start();
            Assert.True(res, "Failed to start recording");
            res = await _api.Recorder.MetaInsert("testKey", uniqueString);
            Assert.True(res, "Failed to create metadata");
            var keys = await _api.Recorder.MetaKeys();
            Assert.That(keys, Has.Member("testKey"));
            var value = await _api.Recorder.MetaLookupString("testKey");
            Assert.That(value, Is.EqualTo(uniqueString), "Failed to read meta info");
            res = await _api.Recorder.MetaInsert("testKey", new byte[] { });
            Assert.True(res, "Failed to delete metadata");
            keys = await _api.Recorder.MetaKeys();
            Assert.That(keys, Has.No.Member("testKey"), "Failed to remove key");

            await _api.Recorder.Cancel();
            var inProgress = await _api.Recorder.RecordingInProgress();
            Assert.False(inProgress, "Failed to cancel recording");
        }

        [Test]
        public async Task CheckRoundtripTimesForPropertyAccess()
        {
            await EnsureApi();
            _api.LogLevel = LogLevel.error;
            for (int i = 0; i < 40; i++)
            {
                var sw = new Stopwatch();
                sw.Start();
                var serial = await _api.System.RecordingUnitSerial;
                sw.Stop();
                Console.WriteLine("read serial: " + sw.ElapsedMilliseconds);
            }
        }

        [Test]
        public async Task CanStartAndStopRecordings()
        {
            await EnsureApi();
            await EnsureSDCard();
            var d = await _api.Recorder.Duration;
            if (d.HasValue)
                await _api.Recorder.Cancel();

            var stoppedCount = 0;
            var startedCount = 0;
            var deletedCount = 0;
            var addedCount = 0;
            var removedCount = 0;
            var sw = new Stopwatch();
            var tokens = new System.Collections.Generic.List<IDisposable>
            {
                await _api.Recordings.ChildAdded.SubscribeAsync(s => addedCount++),
                await _api.Recordings.ChildRemoved.SubscribeAsync(s => removedCount++),
                await _api.Recordings.Deleted.SubscribeAsync(s => deletedCount++),
                await _api.Recorder.Started.SubscribeAsync(s => startedCount++),
                await _api.Recorder.Stopped.SubscribeAsync(s => stoppedCount++)
            };


            var start = await _api.Recorder.Start();

            Assert.True(start, "Failed to start recording");
            Assert.That(() => startedCount, Is.EqualTo(1).After(1000), "start signal not received");
            Thread.Sleep(1000);
            var duration1 = await _api.Recorder.Duration;
            var gazeSamples = await _api.Recorder.GazeSamples;
            var validGazeSamples = await _api.Recorder.ValidGazeSamples;
            Thread.Sleep(1000);
            var duration2 = await _api.Recorder.Duration;
            var gazeSamples2 = await _api.Recorder.GazeSamples;
            var validGazeSamples2 = await _api.Recorder.ValidGazeSamples;
            Assert.That(duration2, Is.GreaterThan(duration1), "unexpected duration after 1 s");
            Assert.That(gazeSamples2, Is.GreaterThan(gazeSamples), "unexpected gazesamples after 1 s");
            Assert.That(validGazeSamples2, Is.GreaterThanOrEqualTo(validGazeSamples), "unexpected validgazesamples after 1 s");

            var recGuid = await _api.Recorder.UUID;
            var folder = await _api.Recorder.Folder;
            var visibleName = await _api.Recorder.VisibleName;
            // var res = await _api.Recorder.SetVisibleName("MyRecording");
            // Assert.True(res, "failed to set new name");
            // var visibleName2 = await _api.Recorder.VisibleName;
            // Assert.That(visibleName2, Is.EqualTo("MyRecording"), "failed to change name of recording");

            var stop = await _api.Recorder.Stop();
            Assert.True(stop, "Failed to stop recording");

            Assert.That(() => stoppedCount, Is.EqualTo(1).After(1000), "stopped signal not received");
            Assert.That(() => addedCount, Is.EqualTo(1).After(1000), "recording added signal not received");

            var recordings = await _api.Recordings.Children();
            var rec = recordings.FirstOrDefault(r => r.UUID == recGuid);
            Assert.NotNull(rec, "recording not found after stop");
            var recFolder = await rec.Folder;
            Assert.That(recFolder, Is.EqualTo(folder), "unexpected folder name of recording");

            var recVisibleName = await rec.VisibleName;
            if (visibleName != "null")
                Assert.That(recVisibleName, Is.EqualTo(visibleName), "incorrect name of recording");

            var newFoldername = Guid.NewGuid().ToString();
            var move = await rec.Move(newFoldername);
            Assert.True(move, "failed to move recording");
            recFolder = await rec.Folder;
            Assert.That(recFolder, Is.EqualTo(newFoldername), "unsuccessful move of recording");

            var delete = await _api.Recordings.Delete(recGuid);
            Assert.True(delete, "failed to delete recording");
            Assert.That(() => removedCount, Is.EqualTo(1).After(1000), "recording removed signal not received");
            Assert.That(() => deletedCount, Is.EqualTo(1).After(1000), "recording deleted signal not received");

            foreach (var t in tokens)
                t.Dispose();
        }

        [Test]
        public async Task CanStartAndCancelRecordings()
        {
            await EnsureApi();
            await EnsureSDCard();
            var d = await _api.Recorder.Duration;
            if (d.HasValue)
                await _api.Recorder.Cancel();

            var start = await _api.Recorder.Start();
            Assert.True(start, "Failed to start recording");
            Thread.Sleep(1000);
            await _api.Recorder.Cancel();

            Assert.False(await _api.Recorder.RecordingInProgress(), "failed to cancel");
            start = await _api.Recorder.Start();
            Assert.True(start, "Failed to start recording 2");
            Thread.Sleep(1000);
            await _api.Recorder.Cancel();

            Assert.False(await _api.Recorder.RecordingInProgress(), "failed to cancel 2");

        }

        private async Task EnsureSDCard()
        {
            var state = await _api.System.Storage.CardState;
            Assert.That(state, Is.EqualTo(CardState.Available), "SD card not in state 'available'");
            var space = await _api.System.Storage.Free;
            Assert.That(space, Is.GreaterThan(10 * 1024 * 1024), "Too little space left on SD card");
        }

        [Test]
        public async Task SubscribeToGazeGivesAtleast10SamplesIn1Second()
        {
            await EnsureApi();
            var gazeCounter = 0;

            var subscription = _api.Rudimentary.Gaze.Subscribe(g =>
            {
                gazeCounter++;
            });
            Assert.That(subscription, Is.Not.Null);
            await _api.Rudimentary.Keepalive();
            Assert.That(() => gazeCounter, Is.GreaterThan(10).After(1000), "No gaze data coming");

            subscription.Dispose();
            gazeCounter = 0;
            await _api.Rudimentary.Keepalive();
            Assert.That(() => gazeCounter, Is.LessThan(10).After(1000), "GazeData keeps coming even after unsubscribe");
        }

        [Test]
        public async Task SubscribeToCalibMarkersGivesAtleast10SamplesIn2Seconds()
        {
            await EnsureApi();
            var emitMarkers = await _api.Calibrate.EmitMarkers();
            Assert.That(emitMarkers, Is.True);
            var markers = new ConcurrentBag<G3MarkerData>();

            var subscription = _api.Calibrate.Marker.Subscribe(Observer.Create<G3MarkerData>(g => markers.Add(g)));
            Assert.That(subscription, Is.Not.Null);
            Assert.That(() => markers.Count, Is.GreaterThanOrEqualTo(10).After(2000).PollEvery(100), "No marker data coming");

            subscription.Dispose();
            await Task.Delay(200);
            markers = new ConcurrentBag<G3MarkerData>();
            Assert.That(() => markers.Count, Is.EqualTo(0).After(1000), "MarkerData keeps coming even after unsubscribe");
        }

        [Test]
        public async Task CanDoStuffWithWifi()
        {
            await EnsureApi();
            if (!await _api.Network.WifiHwEnabled)
                return;
            if (!await _api.Network.WifiEnable)
            {
                Assert.True(await _api.Network.SetWifiEnable(true), "unable to enable wifi");
                Thread.Sleep(2000);
                Assert.That(await _api.Network.WifiEnable, "wifi not Enabled");
            }
            _api.Network.Wifi.Connected.Subscribe(n => Console.WriteLine("Connected"));
            _api.Network.Wifi.StateChange.Subscribe(n => Console.WriteLine($"ConnectionState: {n}"));
            Assert.True(await _api.Network.SetWifiEnable(false), "unable to disable wifi");
            Assert.False(await _api.Network.WifiEnable, "wifi not disabled");
            Assert.True(await _api.Network.SetWifiEnable(true), "unable to enable wifi");
            Thread.Sleep(2000);
            Assert.True(await _api.Network.WifiEnable, "wifi not enabled");

            var config = await _api.Network.Wifi.ActiveConfiguration;
            var activeNetwork = await _api.Network.Wifi.ConnectedNetwork;
            var autoConnect = await _api.Network.Wifi.AutoConnect;

            var ipv4Address = await _api.Network.Wifi.Ipv4Address;
            var ipv4Gateway = await _api.Network.Wifi.Ipv4Gateway;
            var ipv4NameServers = await _api.Network.Wifi.Ipv4NameServers;
            var ipv6Address = await _api.Network.Wifi.Ipv6Address;
            var ipv6Gateway = await _api.Network.Wifi.Ipv6Gateway;
            var ipv6NameServers = await _api.Network.Wifi.Ipv6NameServers;


            var mac = await _api.Network.Wifi.MacAddress;
            var speed = await _api.Network.Wifi.Speed;
            var state = await _api.Network.Wifi.State;
            var stateReason = await _api.Network.Wifi.StateReason;
            var networktype = await _api.Network.Wifi.Type;
            Assert.That(networktype, Is.EqualTo(NetworkType.Wifi), "unexpected network type");
            Assert.That(speed, Is.GreaterThanOrEqualTo(0), "unexpected network speed");
            Assert.That(ipv4Address, Is.Not.Empty, "ip4 address was emtpy");
            Assert.That(ipv4Gateway, Is.Not.Empty, "ip4 gateway was empty");

            Assert.That(ipv6Address, Is.Not.Empty, "ip6 address was emtpy");
            Assert.That(ipv6Gateway, Is.Not.Empty, "ip6 gateway was empty");

            Assert.That(mac, Is.Not.Empty, "unexpected mac address");
            Assert.That(state, Is.Not.EqualTo(ConnectionState.Unknown), "unexpected connection state");
            Assert.That(config, Is.Not.Null, "unexpected null wifi config guid");

        }
    }
}
