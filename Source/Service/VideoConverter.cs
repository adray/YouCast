using Google.Apis.Download;
using Google.Apis.YouTube.v3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Service
{
    public interface IVideoDownloader
    {
        void Download(string url, string file);
    }

    public class YoutubeVideoDownloader : IVideoDownloader
    {
        private YouTubeService _youtubeService;

        public YoutubeVideoDownloader(YouTubeService service)
        {
            _youtubeService = service;
        }

        public void Download(string url, string file)
        {
            using (var writer = new StreamWriter(file))
            {
                new MediaDownloader(_youtubeService).Download(url, writer.BaseStream);
            }
        }
    }

    public class VideoConverter
    {
        private static Dictionary<string, VideoConversionJob> _downloads = new Dictionary<string, VideoConversionJob>();
        private static object _lock = new object();

        private class VideoConversionJob
        {
            private Task task;

            public VideoConversionJob(Task task)
            {
                this.task = task;
            }

            public void Start()
            {                
                task.Start();
                Await();
            }

            public void Await()
            {
                if (!task.IsCompleted)
                {
                    task.Wait();
                }
            }
        }

        public VideoConverter()
        {            
            if (!Directory.Exists("Downloads"))
            {
                Directory.CreateDirectory("Downloads");
            }
        }
        
        public Stream DownloadAndConvert(string source, string target, VideoLibrary.Video video, IVideoDownloader downloader)
        {
            source = string.Format(@"Downloads\{0}", source);
            target = string.Format(@"Downloads\{0}", target);
            
            bool download = false;
            VideoConversionJob job = null;
            lock (_lock)
            {
                if (_downloads.TryGetValue(source, out job))
                {
                    download = false;
                }
                else
                {
                    download = true;
                    job = new VideoConversionJob(new Task(() =>
                        {
                            // Download
                            downloader.Download(video.Uri, source);

                            // Convert
                            var args = string.Format("-y -i {0} -r 20 -s 352x288 -b 400k -acodec aac -strict experimental -ac 1 -ar 8000 -ab 24k {1}", source, target);
                            var proc = new Process()
                            {
                                StartInfo = new ProcessStartInfo(@"C:\Users\Adam\Documents\ffmpeg\bin\ffmpeg.exe", args)
                                {
                                    CreateNoWindow = true,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                },
                            };
                            string error = string.Empty;
                            proc.ErrorDataReceived += (o, e) =>
                            {
                                error += e.Data;
                                Console.WriteLine(e.Data);
                            };
                            proc.Start();
                            proc.BeginErrorReadLine();
                            proc.WaitForExit();
                            Console.WriteLine("File converted.");
                        }));
                    _downloads.Add(source, job);
                }
            }

            if (download)
            {
                job.Start();
            }
            else
            {
                job.Await();
            }

            return new StreamReader(target).BaseStream;
        }
    }
}
