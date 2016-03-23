using Google.Apis.Download;
using Google.Apis.YouTube.v3;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Service
{
    public interface IVideoDownloader
    {
        void Download(string url, string file);
    }

    public sealed class YoutubeVideoDownloader : IVideoDownloader
    {
        private YouTubeService _youtubeService;

        public YoutubeVideoDownloader(YouTubeService service)
        {
            _youtubeService = service;
        }

        public void Download(string url, string file)
        {
            try
            {
                using (var writer = new StreamWriter(file))
                {
                    new MediaDownloader(_youtubeService).Download(url, writer.BaseStream);
                }
            }
            catch (IOException)
            {
                
            }
        }
    }

    public sealed class VideoConverter
    {
        private static Dictionary<string, VideoConversionJob> _downloads = new Dictionary<string, VideoConversionJob>();
        private static object _lock = new object();

        private class VideoConversionJob
        {
            private Task _task;

            public VideoConversionJob(Task task)
            {
                _task = task;
                _task.Start();
            }

            public void Join()
            {
                _task.Wait();
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
            source = $@"Downloads\{source}";
            target = $@"Downloads\{target}";
            
            VideoConversionJob job = null;
            lock (_lock)
            {
                if (!_downloads.TryGetValue(source, out job))
                {
                    job = new VideoConversionJob(new Task(() =>
                        {
                            downloader.Download(video.Uri, source);
                            
                            var args = string.Format("-y -i {0} -r 20 -s 352x288 -b 400k -acodec aac -strict experimental -ac 1 -ar 8000 -ab 24k {1}", source, target);
                            try
                            {
                                var proc = new Process()
                                {
                                    StartInfo = new ProcessStartInfo(YoutubeFeed.VideoConversionPath, args)
                                    {
                                        CreateNoWindow = true,
                                        RedirectStandardOutput = false,
                                        RedirectStandardError = false,
                                        UseShellExecute = false,
                                    },
                                };

                                proc.Start();
                                proc.WaitForExit();
                            }
                            catch (InvalidOperationException)
                            {

                            }
                            catch (Win32Exception)
                            {

                            }
                        }));
                    _downloads.Add(source, job);
                }
            }

            job.Join();

            return new StreamReader(target).BaseStream;
        }
    }
}
