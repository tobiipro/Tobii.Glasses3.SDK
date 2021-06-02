using System;
using System.IO;
using System.Net;

namespace G3SDK
{
    public class RecordingDownloader
    {
        private readonly Recording _rec;

        public RecordingDownloader(Recording rec)
        {
            _rec = rec;
        }
        public string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        public async void DownloadRecording(string folderName)
        {
            var recG3Path = Path.Combine(folderName, "recording.g3");
            var basePath = $"{_rec.G3Api.IpAddress}/{_rec.HttpPath}";

            using (var client = new WebClient())
            {
                client.DownloadFile(basePath, recG3Path);
                var recG3 = G3FileParser.ReadRecording(recG3Path);
                if (recG3.events != null)
                    client.DownloadFile(new Uri($"{basePath}/{recG3.events.file}"), Path.Combine(folderName, recG3.events.file));
                client.DownloadFile($"{basePath}/{recG3.gaze.file}", Path.Combine(folderName, recG3.gaze.file));
                client.DownloadFile($"{basePath}/{recG3.scenecamera.file}", Path.Combine(folderName, recG3.scenecamera.file));
                if (recG3.eyecameras != null)
                    client.DownloadFile($"{basePath}/{recG3.scenecamera.file}", Path.Combine(folderName, recG3.scenecamera.file));
                var metaFolder = Path.Combine(folderName, recG3.metafolder);
                Directory.CreateDirectory(metaFolder);
                foreach (var metakey in await _rec.MetaKeys())
                {
                    client.DownloadFile($"{basePath}/{recG3.metafolder}/{metakey}", Path.Combine(metaFolder, metakey));
                }
            }
        }
    }
}
