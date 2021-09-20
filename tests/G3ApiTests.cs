using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using NUnit.Framework;
using RtspClientSharp;
using RtspClientSharp.RawFrames.Audio;
using RtspClientSharp.RawFrames.Video;
using Unosquare.FFME;
using Timer = System.Timers.Timer;

namespace G3SDK
{
    [TestFixture]
    public class G3ApiTests : G3TestBase
    {
        [Test]
        public async Task StoragePropertiesCanBeRead()
        {
            await EnsureApi();
            var sdCardState = await G3Api.System.Storage.CardState;
            var free = await G3Api.System.Storage.Free;
            var size = await G3Api.System.Storage.Size;
            var remainingTime = await G3Api.System.Storage.RemainingTime;
            var spaceState = await G3Api.System.Storage.SpaceState;
            if ((sdCardState & CardState.Available) != 0)
            {
                Assert.That(remainingTime.TotalSeconds, Is.Positive, "remainingTime");
                Assert.That(free, Is.Positive, "free");
                Assert.That(size, Is.Positive, "size");
                Assert.That(spaceState, Is.Not.Null, "spaceState");
            }
        }

        [Test]
        public async Task UpgradePropertiesCanBeRead()
        {
            await EnsureApi();
            var upgradeInProcess = await G3Api.Upgrade.InProgress;
            Assert.That(upgradeInProcess, Is.Not.Null);
            VerifySignal(G3Api.Upgrade.Completed, "upgrade.completed");
            VerifySignal(G3Api.Upgrade.Progress, "upgrade.progress");
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

            VerifySignal(G3Api.Recordings.ScanStart, "recordings.scanstart");
            VerifySignal(G3Api.Recordings.ScanDone, "recordings.scandone");
        }

        [Test]
        [Ignore("Test takes a long time")]
        public async Task WebsocketLives()
        {
            await EnsureApi();
            var gazeCounter = 0;
            await G3Api.Rudimentary.Gaze.SubscribeAsync(g =>
            {
                gazeCounter++;
            });
            while (gazeCounter == 0)
            {
                await G3Api.Rudimentary.Keepalive();
                await Task.Delay(500);
            }
            var counter = 0;

            while (counter < 500)
            {
                await G3Api.Rudimentary.Keepalive();
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

            var ntpEnabled = await G3Api.System.NtpIsEnabled;
            if (!ntpEnabled)
            {
                await G3Api.System.UseNtp(true);
                ntpEnabled = await G3Api.System.NtpIsEnabled;
                Assert.That(ntpEnabled, Is.EqualTo(true), "ntp was not enabled");
            }

            var ntpSynced = await G3Api.System.NtpIsSynchronized;

            var before = DateTime.Now;

            var time = await G3Api.System.Time;
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
            var ruSerial = await G3Api.System.RecordingUnitSerial;
            var huSerial = await G3Api.System.HeadUnitSerial;
            var version = await G3Api.System.Version;
            var tz = await G3Api.System.TimeZone;
            var time = await G3Api.System.Time;
            var freqs = await G3Api.System.AvailableGazeFrequencies();

            Assert.That(ruSerial, Is.Not.Empty);
            Assert.That(huSerial, Is.Not.Empty);
            Assert.That(version, Is.Not.Empty);
            Assert.That(tz, Is.Not.Empty);
            Assert.That(time, Is.GreaterThan(new DateTime(2020, 01, 01)));
            Assert.That(freqs, Is.Not.Empty);
            Assert.That(freqs, Has.Member(50));
        }

        [Test]
        public async Task WebRtcObjectsCanBeInteractedWith()
        {
            await EnsureApi();
            var session = await G3Api.WebRTC.Create();
            await session.SetIframeStream(true);
            var frequency = await session.CurrentGazeFrequency;
            var stunServer = await session.StunServer;
            var turnServer = await session.TurnServer;
            // Deleting a WebRTC object immediately fails in FW versions prior to 1.20
            if (FwVersion.LessThan(G3Version.Version_1_20_Crayfish))
                await Task.Delay(500);
            var iceCandidates = new List<IceCandidate>();
            session.NewIceCandidate.Subscribe(c => iceCandidates.Add(c));
            var offer = await session.Setup();
            await Task.Delay(1000);
            await G3Api.WebRTC.Delete(session);
            Assert.That(iceCandidates, Is.Not.Empty, "No ICE candidates received in 1s");
            Assert.That(offer, Is.Not.Null, "WebRTC offer is empty");
        }

        [Test]
        public async Task RecorderPropertiesCanBeRead()
        {
            await EnsureApi();
            // no recording in progress
            var folder = await G3Api.Recorder.Folder;
            var visibleName = await G3Api.Recorder.VisibleName;
            var duration = await G3Api.Recorder.Duration;
            var created = await G3Api.Recorder.Created;
            var uuid = await G3Api.Recorder.UUID;
            var timeZone = await G3Api.Recorder.TimeZone;
            var gazeSamples = await G3Api.Recorder.GazeSamples;
            var validGazeSamples = await G3Api.Recorder.ValidGazeSamples;
            var remainingTime = await G3Api.Recorder.RemainingTime;
            var currentGazeFreq = await G3Api.Recorder.CurrentGazeFrequency;

            Assert.That(folder, Is.Null, "folder");
            Assert.That(duration, Is.Null, "duration");
            Assert.That(created, Is.Null, "created");
            Assert.That(uuid, Is.EqualTo(Guid.Empty), "uuid");
            Assert.That(gazeSamples, Is.GreaterThanOrEqualTo(-1));
            Assert.That(validGazeSamples, Is.GreaterThanOrEqualTo(-1));
            Assert.That(remainingTime.TotalSeconds, Is.Not.Negative);
            Assert.That(visibleName, Is.Not.Empty);
            Assert.That(timeZone, Is.Not.Empty);
            Assert.That(currentGazeFreq, Is.EqualTo(0));
        }

        private class Payload
        {
            public string Str;
            public int Num;
        }

        [Test]
        public async Task EventsCanBeRecorded()
        {
            await EnsureApi();

            await G3Api.Rudimentary.Keepalive();
            var eventCount = 0;
            var token = G3Api.Rudimentary.Event.Subscribe(e =>
            {
                Assert.That(e.Tag, Is.EqualTo("tag1"));
                var obj = JsonConvert.DeserializeObject<Payload>(e.Obj);
                Assert.That(obj.Num, Is.EqualTo(123));
                Assert.That(obj.Str, Is.EqualTo("abc"));

                eventCount++;
            });

            var res = await G3Api.Rudimentary.SendEvent("tag1", new Payload() { Num = 123, Str = "abc" });
            Assert.True(res, "send-event failed");

            Thread.Sleep(500);

            Assert.That(eventCount, Is.EqualTo(1));

            Thread.Sleep(1000);
            token.Dispose();


        }

        [Test]
        public async Task EventSampleCanBeRead()
        {
            await EnsureApi();

            await G3Api.Rudimentary.Keepalive();

            var res = await G3Api.Rudimentary.SendEvent("tag1", new Payload() { Num = 123, Str = "abc" });
            Assert.True(res, "send-event failed");

            Thread.Sleep(500);

            var sample = await G3Api.Rudimentary.EventSample;
            Assert.That(sample, Is.Not.Null, "no event-sample");
            Assert.That(sample.Tag, Is.EqualTo("tag1"));
            var sampleObj = JsonConvert.DeserializeObject<Payload>(sample.Obj);
            Assert.That(sampleObj.Num, Is.EqualTo(123));
            Assert.That(sampleObj.Str, Is.EqualTo("abc"));

            Thread.Sleep(1000);
        }

        [Test]
        public async Task ValidateAPI()
        {
            await EnsureApi();
            var warnings = new List<string>();

            if ((G3Api is G3Api x))
            {
                var session = await x.WebRTC.Create();

                foreach (var o in x.Children)
                {
                    await o.ValidateApi(warnings);
                }

                await G3Api.WebRTC.Delete(session);

                var ethernetConfigs = await G3Api.Network.Ethernet.Configurations.Children();
                if (ethernetConfigs.Any())
                    await ethernetConfigs.First().ValidateApi(warnings);

                foreach (var w in warnings)
                    Console.WriteLine(w);
                Assert.IsEmpty(warnings, $"Api warnings: " +
                                         Environment.NewLine + "=====" +
                                         Environment.NewLine + string.Join(Environment.NewLine, warnings) +
                                         Environment.NewLine + "=====");
            }
        }

        [Test]
        public async Task BatteryPropertiesCanBeRead()
        {
            await EnsureApi();
            var remainingTime = await G3Api.System.Battery.RemainingTime;
            var charging = await G3Api.System.Battery.Charging;
            var level = await G3Api.System.Battery.Level;
            var state = await G3Api.System.Battery.State;
            Assert.That(charging, Is.Not.Null);
            Assert.That(state, Is.Not.Null);
            Assert.That(remainingTime.TotalSeconds, Is.Positive, "remainingTime");
            Assert.That(level, Is.Positive, "level");
        }

        [Test]
        public async Task CanSetAndReadMeta()
        {
            await EnsureApi();
            await EnsureSDCard();
            var uniqueString = Guid.NewGuid().ToString();
            var res = await G3Api.Recorder.Start();
            Assert.True(res, "Failed to start recording");
            res = await G3Api.Recorder.MetaInsert("testKey", uniqueString);
            Assert.True(res, "Failed to create metadata");
            var keys = await G3Api.Recorder.MetaKeys();
            Assert.That(keys, Has.Member("testKey"));
            var value = await G3Api.Recorder.MetaLookupString("testKey");
            Assert.That(value, Is.EqualTo(uniqueString), "Failed to read meta info");
            res = await G3Api.Recorder.MetaInsert("testKey", new byte[] { });
            Assert.True(res, "Failed to delete metadata");
            keys = await G3Api.Recorder.MetaKeys();
            Assert.That(keys, Has.No.Member("testKey"), "Failed to remove key");

            await G3Api.Recorder.Cancel();
            var inProgress = await G3Api.Recorder.RecordingInProgress();
            Assert.False(inProgress, "Failed to cancel recording");
        }

        [Test]
        public async Task CanManipulateGazeFrequency()
        {
            await EnsureApi();
            if (FwVersion.LessThan(G3Version.Version_1_14_Nudelsoppa))
                Assert.Ignore("Test only supports v1.14+ ");
            await EnsureSDCard();
            var signals = new List<string>();

            // verify gaze-frequency

            var settingsChangedToken = await G3Api.Settings.Changed.SubscribeAsync(s => signals.Add(s));

            var res = await G3Api.Settings.SetGazeFrequency(50);
            Assert.True(res, "unable to set gaze-frequency to 50");
            Assert.That(await G3Api.Settings.GazeFrequency, Is.EqualTo(50).After(200, 50));
            if (settingsChangedToken != null)
                Assert.That(signals, Has.Member("gaze-frequency").After(200, 50));
            signals.Clear();

            res = await G3Api.Settings.SetGazeFrequency(100);
            Assert.True(res, "unable to set gaze-frequency to 100hz");
            Assert.That(await G3Api.Settings.GazeFrequency, Is.EqualTo(100).After(200, 50));
            if (settingsChangedToken != null)
                Assert.That(signals, Has.Member("gaze-frequency").After(200, 50));
            signals.Clear();

            res = await G3Api.Settings.SetGazeFrequency(50);
            Assert.True(res, "unable to set gaze-frequency to 50hz");
            Assert.That(await G3Api.Settings.GazeFrequency, Is.EqualTo(50).After(200, 50));
            if (settingsChangedToken != null)
                Assert.That(signals, Has.Member("gaze-frequency").After(200, 50));
            signals.Clear();
            if (settingsChangedToken != null)
                settingsChangedToken.Dispose();
        }

        [Test]
        public async Task CanManipulateGazeOverlay()
        {
            await EnsureApi();
            if (FwVersion.LessThan(G3Version.Version_1_14_Nudelsoppa))
                Assert.Ignore("Test only supports v1.14+");
            await EnsureSDCard();
            var signals = new List<string>();

            // verify gaze overlay notifications
            var token = await G3Api.Settings.Changed.SubscribeAsync(s => signals.Add(s));

            // make sure it is false;
            var res = await G3Api.Settings.SetGazeOverlay(false);
            Assert.True(res, "unable to set gaze-overlay, to false");
            Assert.That(await G3Api.Settings.GazeOverlay, Is.EqualTo(false).After(200, 50));
            signals.Clear();

            // try to set it to true
            res = await G3Api.Settings.SetGazeOverlay(true);
            Assert.True(res, "unable to set gaze-overlay, to true");
            Assert.That(await G3Api.Settings.GazeOverlay, Is.EqualTo(true).After(200, 50));
            Assert.That(signals, Has.Member("gaze-overlay").After(200, 50));
            signals.Clear();

            // make recording with gaze overlay
            await G3Api.Recorder.Start();
            Assert.True(await G3Api.Recorder.GazeOverlay, "recorder.gaze-overlay is wrong");
            await Task.Delay(1000);
            var rec1 = await G3Api.Recorder.UUID;
            await G3Api.Recorder.Stop();

            // try to set it to false
            res = await G3Api.Settings.SetGazeOverlay(false);
            Assert.True(res, "unable to set gaze-overlay to off");
            Assert.That(await G3Api.Settings.GazeOverlay, Is.EqualTo(false).After(200, 50));
            Assert.That(signals, Has.Member("gaze-overlay").After(200, 50));

            // make recording without gaze overlay
            await G3Api.Recorder.Start();
            Assert.False(await G3Api.Recorder.GazeOverlay, "recorder.gaze-overlay is wrong");
            await Task.Delay(1000);
            var rec2 = await G3Api.Recorder.UUID;
            await G3Api.Recorder.Stop();

            signals.Clear();

            // check recordings
            var recs = await G3Api.Recordings.Children();
            Assert.True(await recs.First(r => r.UUID == rec1).GazeOverlay);
            Assert.False(await recs.First(r => r.UUID == rec2).GazeOverlay);

            token.Dispose();
        }

        [Test]
        public async Task CheckRoundtripTimesForPropertyAccess()
        {
            await EnsureApi();
            G3Api.LogLevel = LogLevel.error;
            for (int i = 0; i < 40; i++)
            {
                var sw = new Stopwatch();
                sw.Start();
                var serial = await G3Api.System.RecordingUnitSerial;
                Assert.That(serial, Is.Not.Null);
                sw.Stop();
                Console.WriteLine("read serial: " + sw.ElapsedMilliseconds);
            }
        }

        [Test]
        public async Task CanSendEventsDuringRecording()
        {
            await EnsureApi();
            await EnsureSDCard();

            if (FwVersion.LessThan(G3Version.Version_1_20_Crayfish))
                Assert.Ignore("Test will fail in fw less than 1.20");

            var d = await G3Api.Recorder.Duration;
            if (d.HasValue)
                await G3Api.Recorder.Cancel();

            await G3Api.Recorder.Start();
            var eventCounter = 0;
            var t = new Timer
            {
                Interval = 50,
                Enabled = true
            };

            t.Elapsed += async (sender, args) =>
            {
                var evRes = await G3Api.Recorder.SendEvent("tag", new Payload() { Num = eventCounter++, Str = DateTime.Now.ToString() });
                Assert.That(evRes, Is.True);
            };

            Thread.Sleep(3000);
            var res = await G3Api.Recorder.Stop();
            var duration = await G3Api.Recorder.Duration;
            var serial = await G3Api.System.RecordingUnitSerial;
            t.Enabled = false;
            Assert.That(res, Is.True, "Failed to stop recording");
            Assert.That(duration, Is.Null, "Duration not null after recording stopped");
            Assert.That(serial, Contains.Substring("TG0"), "Serial number unexpected after stopped recording");
        }

        [Test]
        public async Task CanStartAndStopRecordings()
        {
            await EnsureApi();
            await EnsureSDCard();
            var d = await G3Api.Recorder.Duration;
            if (d.HasValue)
                await G3Api.Recorder.Cancel();

            var stoppedCount = 0;
            var startedCount = 0;
            var deletedCount = 0;
            var addedCount = 0;
            var removedCount = 0;
            var sw = new Stopwatch();
            var tokens = new List<IDisposable>
            {
                await G3Api.Recordings.ChildAdded.SubscribeAsync(s => addedCount++),
                await G3Api.Recordings.ChildRemoved.SubscribeAsync(s => removedCount++),
                await G3Api.Recordings.Deleted.SubscribeAsync(s => deletedCount++),
                await G3Api.Recorder.Started.SubscribeAsync(s => startedCount++),
                await G3Api.Recorder.Stopped.SubscribeAsync(s => stoppedCount++)
            };

            var start = await G3Api.Recorder.Start();

            Assert.True(start, "Failed to start recording");
            Assert.That(() => startedCount, Is.EqualTo(1).After(1000), "start signal not received");
            Thread.Sleep(1000);

            var currentGazeFrequency = await G3Api.Recorder.CurrentGazeFrequency;

            var duration1 = await G3Api.Recorder.Duration;
            var gazeSamples = await G3Api.Recorder.GazeSamples;
            var validGazeSamples = await G3Api.Recorder.ValidGazeSamples;
            Thread.Sleep(1000);
            var duration2 = await G3Api.Recorder.Duration;
            var gazeSamples2 = await G3Api.Recorder.GazeSamples;
            var validGazeSamples2 = await G3Api.Recorder.ValidGazeSamples;

            Assert.That(currentGazeFrequency, Is.GreaterThan(0));
            Assert.That(duration2, Is.GreaterThan(duration1), "unexpected duration after 1 s");
            Assert.That(gazeSamples2, Is.GreaterThan(gazeSamples), "unexpected gazesamples after 1 s");
            Assert.That(validGazeSamples2, Is.GreaterThanOrEqualTo(validGazeSamples), "unexpected validgazesamples after 1 s");

            var recGuid = await G3Api.Recorder.UUID;
            var folder = await G3Api.Recorder.Folder;
            var visibleName = await G3Api.Recorder.VisibleName;
            // var res = await _api.Recorder.SetVisibleName("MyRecording");
            // Assert.True(res, "failed to set new name");
            // var visibleName2 = await _api.Recorder.VisibleName;
            // Assert.That(visibleName2, Is.EqualTo("MyRecording"), "failed to change name of recording");

            var stop = await G3Api.Recorder.Stop();
            Assert.True(stop, "Failed to stop recording");

            Assert.That(() => stoppedCount, Is.EqualTo(1).After(1000), "stopped signal not received");
            Assert.That(() => addedCount, Is.EqualTo(1).After(1000), "recording added signal not received");

            var recordings = await G3Api.Recordings.Children();
            var rec = recordings.FirstOrDefault(r => r.UUID == recGuid);
            Assert.NotNull(rec, "recording not found after stop");
            var recFolder = await rec.Folder;
            Assert.That(recFolder, Is.EqualTo(folder), "unexpected folder name of recording");

            var recVisibleName = await rec.VisibleName;
            if (visibleName != null)
                Assert.That(recVisibleName, Is.EqualTo(visibleName), "incorrect name of recording");

            var newFoldername = Guid.NewGuid().ToString();
            var move = await rec.Move(newFoldername);
            Assert.True(move, "failed to move recording");
            recFolder = await rec.Folder;
            Assert.That(recFolder, Is.EqualTo(newFoldername), "unsuccessful move of recording");

            var delete = await G3Api.Recordings.Delete(recGuid);
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
            var d = await G3Api.Recorder.Duration;
            if (d.HasValue)
                await G3Api.Recorder.Cancel();

            var start = await G3Api.Recorder.Start();
            Assert.True(start, "Failed to start recording");
            Thread.Sleep(1000);
            await G3Api.Recorder.Cancel();

            Assert.False(await G3Api.Recorder.RecordingInProgress(), "failed to cancel");
            start = await G3Api.Recorder.Start();
            Assert.True(start, "Failed to start recording 2");
            Thread.Sleep(1000);
            await G3Api.Recorder.Cancel();

            Assert.False(await G3Api.Recorder.RecordingInProgress(), "failed to cancel 2");
        }

        private async Task EnsureSDCard()
        {
            var state = await G3Api.System.Storage.CardState;
            Assert.That(state, Is.EqualTo(CardState.Available), "SD card not in state 'available'");
            var space = await G3Api.System.Storage.Free;
            Assert.That(space, Is.GreaterThan(10 * 1024 * 1024), "Too little space left on SD card");
        }

        [Test]
        public async Task ForceProbeReturnsSomething()
        {
            var b = new G3Browser();
            var list = await b.ForceProbe();
            Assert.That(list, Is.Not.Null);
            foreach (var api in list)
                Console.WriteLine(api.IpAddress);
        }

        [Test]
        public async Task ForceProbeReturnsSomething2()
        {
            var b = new G3Browser();
            var list = await b.ForceProbe(IPAddress.Parse("192.168.0.1"), 100);
            Assert.That(list, Is.Not.Null);
            foreach (var api in list)
                Console.WriteLine(api.IpAddress);
        }

        [Test]
        public async Task SubscribeToGazeGivesAtleast10SamplesIn1Second()
        {
            await EnsureApi();
            var gazeCounter = 0;

            var subscription = G3Api.Rudimentary.Gaze.Subscribe(g =>
            {
                gazeCounter++;
            });
            Assert.That(subscription, Is.Not.Null);
            await G3Api.Rudimentary.Keepalive();
            Assert.That(() => gazeCounter, Is.GreaterThan(10).After(1000), "No gaze data coming");

            subscription.Dispose();
            gazeCounter = 0;
            await G3Api.Rudimentary.Keepalive();
            Assert.That(() => gazeCounter, Is.LessThan(10).After(1000), "GazeData keeps coming even after unsubscribe");
        }

        [Test]
        public async Task SubscribeToCalibMarkersGivesAtleast10SamplesIn2Seconds()
        {
            await EnsureApi();
            var emitMarkers = await G3Api.Calibrate.EmitMarkers();
            Assert.That(emitMarkers, Is.True);
            var markers = new ConcurrentBag<G3MarkerData>();

            var subscription = G3Api.Calibrate.Marker.Subscribe(Observer.Create<G3MarkerData>(g => markers.Add(g)));
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
            if (!await G3Api.Network.WifiHwEnabled)
                return;
            if (!await G3Api.Network.WifiEnable)
            {
                Assert.True(await G3Api.Network.SetWifiEnable(true), "unable to enable wifi");
                Thread.Sleep(2000);
                Assert.That(await G3Api.Network.WifiEnable, "wifi not Enabled");
            }
            G3Api.Network.Wifi.Connected.Subscribe(n => Console.WriteLine("Connected"));
            G3Api.Network.Wifi.StateChange.Subscribe(n => Console.WriteLine($"ConnectionState: {n}"));
            Assert.True(await G3Api.Network.SetWifiEnable(false), "unable to disable wifi");
            Assert.False(await G3Api.Network.WifiEnable, "wifi not disabled");
            Assert.True(await G3Api.Network.SetWifiEnable(true), "unable to enable wifi");
            Thread.Sleep(2000);
            Assert.True(await G3Api.Network.WifiEnable, "wifi not enabled");

            var config = await G3Api.Network.Wifi.ActiveConfiguration;
            var activeNetwork = await G3Api.Network.Wifi.ConnectedNetwork;
            var autoConnect = await G3Api.Network.Wifi.AutoConnect;

            var ipv4Address = await G3Api.Network.Wifi.Ipv4Address;
            var ipv4Gateway = await G3Api.Network.Wifi.Ipv4Gateway;
            var ipv4NameServers = await G3Api.Network.Wifi.Ipv4NameServers;
            var ipv6Address = await G3Api.Network.Wifi.Ipv6Address;
            var ipv6Gateway = await G3Api.Network.Wifi.Ipv6Gateway;
            var ipv6NameServers = await G3Api.Network.Wifi.Ipv6NameServers;


            var mac = await G3Api.Network.Wifi.MacAddress;
            var speed = await G3Api.Network.Wifi.Speed;
            var state = await G3Api.Network.Wifi.State;
            var stateReason = await G3Api.Network.Wifi.StateReason;
            var networktype = await G3Api.Network.Wifi.Type;
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
