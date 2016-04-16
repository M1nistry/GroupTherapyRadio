using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Threading;
using System.Xml;
using NAudio.Lame;
using NAudio.Wave;
using TagLib;
using File = System.IO.File;

namespace GroupTherapyRadio
{
    internal class Program
    {
        private static string _downloadPath;
        private static bool _convert;
        private static bool _keepOriginal = true;
        private static StreamWriter _logWriter;

        private static void Main(string[] args)
        {
            for (var i = 0; i <= (args.Length - 1); i++)
            {
                if (args[i] == "-path") _downloadPath = args[i + 1];
                if (args[i] == "-convert")
                {
                    _convert = true;
                    if ((i+1) <= (args.Length - 1)) _keepOriginal = args[i + 1] == "1";
                }
            }

            if (string.IsNullOrEmpty(_downloadPath))
            {
                WriteLog(@"Failed to detect a download path, please make sure you're running with a correct -path argument (eg. -path C:\Users\Test\Desktop)");
                Environment.Exit(1);
            }

            if (!Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory).Contains(AppDomain.CurrentDomain.BaseDirectory + "GTRLog.txt")) WriteLog("Group Therapy Radio log file created.");

            var episodeHistory = GetHistory();
            var episodeList = GetEpisodes();
            WriteLog($"Latest episode {episodeList[0].Id.ToUpper()} publsihed on {episodeList[0].PublishDate}");
            if (episodeHistory.Contains(episodeList[0].Id)) WriteLog("No new episode found since our last download.");
            if (!episodeHistory.Contains(episodeList[0].Id)) 
            {
                if (DownloadEpisode(episodeList[0]))
                {
                    WriteLog($"Successfully downloaded Episode {episodeList[0].Id.ToUpper()}");
                    WriteHistory(episodeList[0].Id);
                    if (_convert) ConvertEpisode(_downloadPath + "\\" + episodeList[0].Title + ".mp4");

                    var episodeFile = TagLib.File.Create(_downloadPath + "\\" + episodeList[0].Title + (_convert ? ".mp3" : ".mp4"));
                    episodeFile.Tag.Title = episodeList[0].Title.Replace("Above & Beyond - ", "");
                    episodeFile.Tag.AlbumArtists = new [] { "Above & Beyond" };

                    var image = File.ReadAllBytes(AppDomain.CurrentDomain.BaseDirectory + @"Group_Therapy_Podcast_600x600.jpg");
                    var pic = new Picture
                    {
                        Type = PictureType.FrontCover,
                        MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg,
                        Description = "Cover",
                        Data = image
                    };
                    episodeFile.Tag.Pictures = new IPicture[] { pic };
                    episodeFile.Tag.Album = "Group Therapy Radio";
                    episodeFile.Tag.Track = (uint) episodeList[0].EpisodeNumber;
                    episodeFile.Save();
                }
            }
            Console.ReadLine();
        }

        private static void ConvertEpisode(string path)
        {
            try
            {
                WriteLog(@"Begining file conversion...");
                using (var rdr = new AudioFileReader(path))
                using (var wtr = new LameMP3FileWriter(path.Replace(".mp4", ".mp3"), rdr.WaveFormat, LAMEPreset.VBR_100))
                {
                    rdr.CopyTo(wtr);
                }
                WriteLog(@"Finished file conversion!");
            }
            catch (Exception e)
            {
                WriteLog(@"Error occured while converting - " + e.Message);
                return;
            }
            if (!_keepOriginal)
            {
                File.Delete(path);
                WriteLog(@"Removed original mp4 file");
            }
        }

        private static bool DownloadEpisode(TherapyItem episode)
        {
            WriteLog($"Attempting to download Episode {episode.EpisodeNumber}");
            var client = new WebClient();
            client.DownloadFileCompleted += client_DownloadCompleted;
            try
            {
                client.DownloadFileAsync(episode.DownloadUri, _downloadPath + "\\" + episode.Title + ".mp4");
                while (client.IsBusy) Thread.Sleep(200);
                return true;
            }
            catch (WebException e)
            {
                WriteLog(e.Message);
                return false;
            }
        }

        private static void client_DownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null) WriteLog("Failed while downloading: " + e.Error.Message);
        }
        

        public static string[] GetHistory()
        {
            if (!Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory).Contains(AppDomain.CurrentDomain.BaseDirectory + "GTRHistory.txt")) return new string[0];
            var sR = new StreamReader(AppDomain.CurrentDomain.BaseDirectory + @"GTRHistory.txt");
            var historyString = sR.ReadToEnd();
            var split = historyString.Split(Environment.NewLine.ToCharArray()).Where(x => !string.IsNullOrEmpty(x)).ToArray();
            sR.Close();
            return split;
        }

        public static List<TherapyItem> GetEpisodes()
        {
            var episodeList = new List<TherapyItem>();
            var link = "http://static.aboveandbeyond.nu/grouptherapy/podcast.xml";
            var request = WebRequest.Create(link);
            request.Timeout = 5000;

            using (var response = request.GetResponse())
            using (var reader = XmlReader.Create(response.GetResponseStream()))
            {
                var feed = SyndicationFeed.Load(reader);

                if (feed != null)
                {
                    episodeList.AddRange(feed.Items.Select(item => new TherapyItem
                    {
                        Id = item.Id, PublishDate = item.PublishDate.DateTime, Title = item.Summary.Text, DownloadUri = item.Links[1].Uri
                    }));
                }
            }
            return episodeList;
        }

        private static void WriteLog(string logMessage)
        {
            try
            {
                _logWriter = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + @"GTRLog.txt", true);
                _logWriter.WriteLine($"[{DateTime.Now}] {logMessage}");
                _logWriter.Close();
            }
            catch (Exception e)
            {
                return;
            }
        }

        private static void WriteHistory(string episode)
        {
            var historyWritter = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + @"GTRHistory.txt", true);
            historyWritter.WriteLine(episode + Environment.NewLine);
            historyWritter.Close();
        }
    }
}
