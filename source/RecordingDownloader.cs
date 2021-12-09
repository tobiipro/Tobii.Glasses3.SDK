using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace G3SDK
{
    public class RecordingDownloader
    {
        private readonly IG3Api _g3;

        public RecordingDownloader(IG3Api g3)
        {
            _g3 = g3;
        }


        public async Task DownloadRecording(IRecording rec, string targetFolder, IProgress<double> progress)
        {
            var basePath = $"http://{_g3.IpAddress}{await rec.HttpPath}";
            var targetRecFolder = Path.Combine(targetFolder, await rec.Folder);

            using (var client = new WebClient())
            {
                var recG3Path = Path.Combine(targetRecFolder, "recording.g3");
                Directory.CreateDirectory(targetRecFolder);
                client.DownloadFile(basePath, recG3Path);
                var recG3 = G3FileParser.ReadRecording(targetRecFolder);
                var downloadQ = new Queue<DownloadItem>();
                if (recG3.events != null)
                    downloadQ.Enqueue(new DownloadItem(recG3.events.file));

                downloadQ.Enqueue(new DownloadItem(recG3.gaze.file));
                downloadQ.Enqueue(new DownloadItem(recG3.scenecamera.file));

                if (recG3.eyecameras != null)
                    downloadQ.Enqueue(new DownloadItem(recG3.eyecameras.file));

                var metaFolder = Path.Combine(targetRecFolder, recG3.metafolder);
                Directory.CreateDirectory(metaFolder);
                foreach (var metakey in await rec.MetaKeys())
                {
                    downloadQ.Enqueue(new DownloadItem(Path.Combine(recG3.metafolder, metakey)) { SkipSize = true });
                }

                var totalBytesToDownload = 0D;
                foreach (var x in downloadQ)
                {
                    try
                    {
                        var request = WebRequest.CreateHttp(basePath + x.UriPart);
                        //                    request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:40.0) Gecko/20100101 Firefox/40.1";
                        request.Method = "HEAD";
                        using (var response = await request.GetResponseAsync())
                        {
                            x.Length = response.ContentLength;
                            totalBytesToDownload += x.Length;
                        }
                    }
                    catch
                    {
                    }
                }


                progress.Report(0);
                var bytesCompleted = 0L;
                client.DownloadProgressChanged += (sender, args) =>
                {
                    var received = (args.BytesReceived + bytesCompleted) / totalBytesToDownload;
                    progress.Report(received);
                };

                foreach (var x in downloadQ)
                {
                    var targetFileName = Path.Combine(targetRecFolder, x.FileName);
                    var targetFileFolder = Path.GetDirectoryName(targetFileName);
                    if (!Directory.Exists(targetFileFolder))
                        Directory.CreateDirectory(targetFileFolder);
                    await client.DownloadFileTaskAsync(basePath + x.UriPart, targetFileName);
                    bytesCompleted += x.Length;
                }
            }
        }
    }

    public class DownloadItem
    {
        //        public string Target { get; }
        public string FileName { get; }
        //        public Uri Source { get; }
        public long Length { get; set; }
        public bool SkipSize { get; set; }
        public string UriPart => "/" + FileName.Replace("\\", "/");

        public DownloadItem(string fileName)
        {
            //Target = target;
            FileName = fileName;
            //Source = new Uri(source);
        }
    }
}
