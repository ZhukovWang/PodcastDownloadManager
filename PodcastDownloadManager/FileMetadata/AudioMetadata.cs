using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using TagLib;
using File = System.IO.File;

namespace PodcastDownloadManager.FileMetadata
{
    public static class AudioMetadata
    {
        public static void CreateAudioMetadata(string metadataFilePath, string podcastTitle, string releaseTitle, string author, string imageUrl,
            string comment, DateTime time)
        {
            FileStream fs = File.Create(metadataFilePath);

            //Title
            FileTools.AddText(fs, $"{releaseTitle}\n");

            //Artist
            FileTools.AddText(fs, $"{author}\n");

            //Album
            FileTools.AddText(fs, $"{podcastTitle}\n");

            //Genre
            FileTools.AddText(fs, "Podcast\n");

            //Date
            FileTools.AddText(fs, $"{time.ToString("G")}\n");

            //ImageUrl
            FileTools.AddText(fs, $"{imageUrl}\n");

            //Comment
            comment = comment.Replace("\r", "");
            comment = comment.Replace("\n", "");
            FileTools.AddText(fs, $"{comment}\n");

            fs.Close();
        }

        public static void AutoAddMetadata(out List<string> output)
        {
            output = new List<string>();

            string[] allFiles = Directory.GetFiles(ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath);

            foreach (string file in allFiles)
            {
                if (file.Contains(".metadata"))
                {
                    Logger.Log.Info($"Get the metadata file, name is {file}.");

                    string[] metadata = File.ReadAllLines(file);

                    string audioTitle = metadata[0];
                    string audioArtist = metadata[1];
                    string audioAlbum = metadata[2];
                    string audioGenre = metadata[3];
                    DateTime audioDate = DateTime.Parse(metadata[4]);
                    string audioImageUrl = metadata[5];
                    string audioComment = metadata[6];

                    string audioName = Path.GetDirectoryName(file) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(file);

                    if (File.Exists(audioName))
                    {
                        Logger.Log.Info($"Audio file exists, and name is {audioName}.");

                        var tfile = TagLib.File.Create(audioName);

                        tfile.Tag.Title = audioTitle;
                        tfile.Tag.AlbumArtists = new[] { audioArtist };
                        tfile.Tag.Album = audioAlbum;
                        tfile.Tag.Genres = new[] { audioGenre };
                        tfile.Tag.DateTagged = audioDate;

                        string imageName = $"{audioName}.png";
                        if (audioImageUrl != "null")
                        {
                            if (audioImageUrl.Contains(".png"))
                            {
                                imageName = $"{audioName}.png";
                            }
                            else if (audioImageUrl.Contains(".jpg"))
                            {
                                imageName = $"{audioName}.jpg";
                            }
                            else if (audioImageUrl.Contains(".jpeg"))
                            {
                                imageName = $"{audioName}.jpeg";
                            }

                            try
                            {
                                var webClient = new WebClient();
                                webClient.DownloadFile(audioImageUrl, imageName);

                                tfile.Tag.Pictures = new[] { new Picture(imageName), };
                            }
                            catch (Exception e)
                            {
                                Logger.Log.Error($"Image of \"{audioName}\" download failed. Exception is {e.Message}.");
                                output.Add($"Image of \"{audioName}\" download failed.");
                            }
                        }

                        tfile.Tag.Comment = audioComment;

                        tfile.Save();

                        File.Delete(file);
                        File.Delete(imageName);
                    }
                    else
                    {
                        Logger.Log.Error($"Audio file does not exist, and name is \"{audioName}\".");
                        output.Add($"\"{audioName}\" does NOT exist.");
                    }
                }
            }
        }
    }
}