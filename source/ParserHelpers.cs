﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public class ParserHelpers
    {
        public static readonly Dictionary<string, CardState> CardStateTranslations = new Dictionary<string, CardState>();

        public static Action<string> ErrorHandler { get; set; }

        static ParserHelpers()
        {
            CardStateTranslations["not-inserted"] = CardState.NotInserted;
            CardStateTranslations["available"] = CardState.Available;
            CardStateTranslations["read-only"] = CardState.ReadOnly;
            CardStateTranslations["busy"] = CardState.Busy;
            CardStateTranslations["error"] = CardState.Error;
        }

        public static G3GazeData ParseGazeFromJson(string json)
        {
            JObject obj;
            try
            {
                obj = JObject.Parse(json);
            }
            catch
            {
                ErrorHandler?.Invoke($"Failed to parse Json: {json}");
                return null;
            }

            if (obj["type"].Value<string>() == "gaze")
            {
                var timeStamp = obj["timestamp"].Value<double>();
                var data = obj["data"] as JObject;
                return ParseGazeData(data, timeStamp);
            }

            ErrorHandler?.Invoke($"Type is not gaze: " + json);
            return null;
        }

        public static G3ImuData ParseImuFromJson(string json, G3ImuData.ImuCoordSystem coordSystem)
        {
            var obj = (JObject)JsonConvert.DeserializeObject(json);
            if (obj["type"].Value<string>() == "imu")
            {
                var timeStamp = obj["timestamp"].Value<double>();
                var data = obj["data"] as JObject;
                return ParseImuData(data, timeStamp, coordSystem);
            }

            return null;
        }

        public static G3SyncPortData ParseSyncPortFromJson(string json)
        {
            var obj = (JObject)JsonConvert.DeserializeObject(json);
            if (obj["type"].Value<string>() == "syncport")
            {
                var timeStamp = obj["timestamp"].Value<double>();
                var data = obj["data"] as JObject;
                return ParseSyncPortData(data, timeStamp);
            }

            return null;
        }

        public static G3Event ParseEventFromJson(string json)
        {
            var obj = (JObject)JsonConvert.DeserializeObject(json);
            if (obj.Count == 3 && obj["type"].Value<string>() == "event")
            {
                var timeStamp = obj["timestamp"].Value<double>();
                var data = obj["data"] as JObject;
                return ParseEvent(data, timeStamp);
            }

            return null;
        }

        public static G3SyncPortData ParseSyncPortData(JObject data, double ts)
        {
            return ParseSyncPortData(data, GetTimestamp(ts));
        }
        public static G3SyncPortData ParseSyncPortData(JObject data, TimeSpan ts)
        {
            var dir = ParseEnum(data["direction"].Value<string>(), Direction.In);
            var value = data["value"].Value<int>();
            return new G3SyncPortData(ts, dir, value);
        }

        public static G3Event ParseEvent(JObject data, double ts)
        {
            return ParseEvent(data, GetTimestamp(ts));
        }

        public static G3Event ParseEvent(JObject data, TimeSpan ts)
        {
            var tag = data["tag"].Value<string>();
            var obj = data["object"];
            return new G3Event(ts, tag, JsonConvert.SerializeObject(obj));
        }

        public static G3GazeData ParseGazeData(JObject data, TimeSpan timeStamp)
        {
            var gaze2D = (data["gaze2d"] as JArray).Arr2Vector2();
            var gaze3D = (data["gaze3d"] as JArray).Arr2Vector3();
            var eyeLeft = ParseEyeData(data["eyeleft"] as JObject);
            var eyeRight = ParseEyeData(data["eyeright"] as JObject);
            return new G3GazeData(timeStamp, gaze2D, gaze3D, eyeLeft, eyeRight);

        }
        public static G3GazeData ParseGazeData(JObject data, double timeStamp)
        {
            return ParseGazeData(data, GetTimestamp(timeStamp));
        }

        private static TimeSpan GetTimestamp(double timeStamp)
        {
            var t = TimeSpan.FromTicks((long)(timeStamp * TimeSpan.TicksPerSecond));
            return t;
        }

        public static G3ImuData ParseImuData(JObject data, double timeStamp, G3ImuData.ImuCoordSystem coordSystem)
        {
            return ParseImuData(data, GetTimestamp(timeStamp), coordSystem);
        }

        public static G3ImuData ParseImuData(JObject data, TimeSpan timeStamp, G3ImuData.ImuCoordSystem coordSystem)
        {
            var gyro = ParseV3(data["gyroscope"]);
            var accel = ParseV3(data["accelerometer"]);
            var magn = ParseV3(data["magnetometer"]);
            return new G3ImuData(timeStamp, accel, gyro, magn, coordSystem);
        }

        private static Vector3 ParseV3(JToken jToken)
        {
            if (jToken == null)
                return Vector3Extensions.INVALID;
            return (jToken as JArray).Arr2Vector3();
        }

        public static G3GazeData.EyeData ParseEyeData(JObject eyeData)
        {
            if (eyeData == null)
                return null;
            var gazeOrigin = (eyeData["gazeorigin"] as JArray).Arr2Vector3();
            var gazeDirection = (eyeData["gazedirection"] as JArray).Arr2Vector3();
            var pupilDiameter = eyeData["pupildiameter"]?.Value<float>() ?? Single.NaN;
            return new G3GazeData.EyeData(gazeOrigin, gazeDirection, pupilDiameter);
        }

        public static CardState ParseCardState(string arg)
        {
            var values = arg.Split('+');
            CardState res = 0;
            foreach (var v in values)
            {
                if (CardStateTranslations.TryGetValue(v, out var flag))
                    res |= flag;
            }

            return res;
        }

        public static SpaceState ParseSpaceState(string s)
        {
            return (SpaceState)Enum.Parse(typeof(SpaceState), s, true);
        }

        public static BatteryState ParseBatteryState(string s)
        {
            return (BatteryState)Enum.Parse(typeof(BatteryState), s, true);
        }

        public static float ParseFloat(string s)
        {
            return float.Parse(s, CultureInfo.InvariantCulture);
        }
        public static HuConnectionState ParseHuConnectionState(string s)
        {
            return (HuConnectionState)Enum.Parse(typeof(HuConnectionState), s, true);
        }

        public static TimeSpan ParseTimeSpan(string s)
        {
            return GetTimestamp(double.Parse(s, CultureInfo.InvariantCulture));
        }

        public static DateTime? ParseDateOptional(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;
            if (s == "null")
                return null;
            return DateTime.Parse(s);
        }
        public static DateTime ParseDate(string s)
        {
            return DateTime.Parse(s);
        }

        public static TimeSpan? ParseDurationToTimespan(string s)
        {
            var seconds = double.Parse(s, CultureInfo.InvariantCulture);
            if (seconds <= 0)
                return null;
            return GetTimestamp(seconds);
        }

        public static List<G3Event> ParseEventDataFromCompressedStream(Stream compressedData)
        {
            return ParseFromCompressedStream(compressedData, ParseEventFromJson);
        }

        public static List<T> ParseFromCompressedStream<T>(Stream compressedData, Func<string, T> func)
        {
            var result = new List<T>();
            ParseDataFromCompressedStream(compressedData, func, result.Add);
            return result;
        }

        public static List<G3SyncPortData> ParseSyncPortDataFromCompressedStream(Stream compressedData)
        {
            return ParseFromCompressedStream(compressedData, ParseSyncPortFromJson);
        }

        public static List<G3ImuData> ParseImuDataFromCompressedStream(Stream compressedData, G3ImuData.ImuCoordSystem coordSystem)
        {
            return ParseFromCompressedStream(compressedData, json => ParseImuFromJson(json, coordSystem));
        }

        public static void ParseImuDataFromCompressedStream(Stream compressedData, Action<G3ImuData> addAction, G3ImuData.ImuCoordSystem coordSystem)
        {
            ParseDataFromCompressedStream(compressedData, json => ParseImuFromJson(json, coordSystem), addAction);
        }

        public static void ParseGazeDataFromCompressedStream(Stream compressedData, Action<G3GazeData> addAction)
        {
            ParseDataFromCompressedStream(compressedData, ParseGazeFromJson, addAction);
        }

        public static void ParseDataFromCompressedStream<T>(Stream compressedData, Func<string, T> func, Action<T> addAction)
        {
            using (var stream = new GZipStream(compressedData, CompressionMode.Decompress))
            using (var x = new StreamReader(stream))
            {
                while (!x.EndOfStream)
                {
                    var line = x.ReadLine();
                    var data = func(line);
                    if (data != null)
                        addAction(data);
                }
            }
        }

        public static void ParseGazeDataFromCompressedStream(Stream compressedData, ConcurrentQueue<G3GazeData> list)
        {
            ParseDataFromCompressedStream(compressedData, ParseGazeFromJson, list.Enqueue);
        }

        public static List<G3GazeData> ParseGazeDataFromCompressedStream(Stream compressedData)
        {
            return ParseFromCompressedStream(compressedData, ParseGazeFromJson);
        }

        public static Guid ParseGuid(string arg)
        {
            if (arg == null)
                return Guid.Empty;
            return Guid.Parse(arg);
        }

        public static T ParseEnum<T>(string s, T defaultValue) where T : struct, Enum
        {
            if (Enum.TryParse(s.Replace("-", ""), true, out T res))
                return res;
            return JsonConvert.DeserializeObject<T>(s);
        }

        public static G3GazeData SignalToGaze(List<JToken> bodyValues)
        {
            var ts = bodyValues[0].Value<double>();
            var data = bodyValues[1] as JObject;
            var gaze = ParseGazeData(data, ts);
            return gaze;
        }
        public static G3Event SignalToEvent(List<JToken> bodyValues)
        {
            var ts = bodyValues[0].Value<double>();
            var data = bodyValues[1] as JObject;
            var gaze = ParseEvent(data, ts);
            return gaze;
        }

        public static G3ImuData SignalToIMU(List<JToken> bodyValues, G3ImuData.ImuCoordSystem coordSystem)
        {
            var ts = bodyValues[0].Value<double>();
            var data = bodyValues[1] as JObject;
            var gaze = ParseImuData(data, ts, coordSystem);
            return gaze;
        }

        public static G3SyncPortData SignalToSyncPort(List<JToken> bodyValues)
        {
            var ts = bodyValues[0].Value<double>();
            var data = bodyValues[1] as JObject;
            var sync = ParseSyncPortData(data, ts);
            return sync;
        }

        public static bool SignalToBool(List<JToken> arg)
        {
            return arg[0].Value<bool>();
        }

        public static string SignalToString(List<JToken> arg)
        {
            return arg[0].Value<string>();
        }

        public static Ipv6Method Ipv6MethodParser(string arg)
        {
            return ParseEnum(arg, Ipv6Method.unknown);
        }

        public static Ipv4Method Ipv4MethodParser(string arg)
        {
            return ParseEnum(arg, Ipv4Method.unknown);
        }

        public static G3ImuData.ImuCoordSystem GetCoordSystemFromRecordingFolder(string path)
        {
            var fwversionPath = Path.Combine(path, "meta", MetaDataCapableHelpers.MetaDataKey_RuVersion);
            var version = G3Version.ReadFromFile(fwversionPath);
            if (version == G3Version.Unknown)
                return G3ImuData.ImuCoordSystem.Unknown;
            if (version.GreaterOrEqualTo(G3Version.Version_1_29_Sarek))
                return G3ImuData.ImuCoordSystem.SceneCam;
            return G3ImuData.ImuCoordSystem.Imu;

        }
    }
}