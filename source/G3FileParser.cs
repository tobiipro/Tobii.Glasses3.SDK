using System;
using System.IO;
using Newtonsoft.Json;

namespace G3SDK
{
    public class G3FileParser
    {
        public static G3Participant ReadParticipant(string path)
        {
            var participantFile = Path.Combine(path, "meta", "participant");
            if (File.Exists(participantFile))
            {
                var s = File.ReadAllText(participantFile);
                var p = JsonConvert.DeserializeObject<G3Participant>(s);
                return p;
            }

            return null;
        }
        public static G3RecordingJson ReadRecording(string path)
        {
            var recordingFile = Path.Combine(path, "recording.g3");
            if (File.Exists(recordingFile))
            {
                var s = File.ReadAllText(recordingFile);
                var r = JsonConvert.DeserializeObject<G3RecordingJson>(s);
                return r;
            }

            return null;
        }
    }

    public class G3Participant
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class G3RecordingJson
    {
        public string uuid;
        public string name;
        [JsonProperty(PropertyName = "meta-folder")]
        public string metafolder;

        public int version;
        public double duration;
        public string created;
        public SceneCameraInfo scenecamera;
        public EyeCamerasInfo eyecameras;
        public GazeInfo gaze;
        public EventsInfo events;

        public class FileInfo
        {
            public string file;
        }

        public class SceneCameraInfo : FileInfo
        {
            public SnapshotInfo[] snapshots;

            [JsonProperty(PropertyName = "camera-calibration")]
            public CameraCalibrationInfo cameracalibration;
        }

        public class CameraCalibrationInfo
        {
            public double[] position; // vector 3
            [JsonProperty(PropertyName = "focal-length")]
            public double[] focallength; // vector 2
            public double[][] rotation; // vector 3x3
            public double skew; // scalar
            [JsonProperty(PropertyName = "principal-point")]
            public double[] principalpoint; // vector 2
            [JsonProperty(PropertyName = "radial-distortion")]
            public double[] radialdistortion; // vector 3
            [JsonProperty(PropertyName = "tangential-distortion")]
            public double[] tangentialdistortion; // vector 2
            public double[] resolution; // vector 2
        }

        public class SnapshotInfo : FileInfo
        {
            public double time;
        }

        public class GazeInfo : FileInfo
        {
            public int samples;
            [JsonProperty(PropertyName = "valid-samples")]
            public int validsamples;
        }

        public class EyeCamerasInfo : FileInfo
        {

        }
        public class EventsInfo : FileInfo
        {

        }

        public DateTime createdDate => DateTime.Parse(created);
    }
 
}

