using NFlags;
using System;
using System.IO;
using TagLib;

namespace PodcastDownloadManager.FileMetadata
{
    public static class AudioMetadata
    {
        public static void CreateAudioMetadata(string metadataFilePath, string podcastTitle, string releaseTitle, string author, string imageUrl,
            string comment, DateTime time)
        {
            FileStream fs = System.IO.File.Create(metadataFilePath);

            //Title
            FileTools.AddText(fs, $"{releaseTitle}\n");

            //Artist
            FileTools.AddText(fs, $"{author}\n");

            //Album
            FileTools.AddText(fs, $"{podcastTitle}\n");

            //Genre
            FileTools.AddText(fs, "Podcast\n");

            //Date
            FileTools.AddText(fs, $"{time:G}\n");

            //ImageUrl
            FileTools.AddText(fs, $"{imageUrl}\n");

            //Comment
            comment = comment.Replace("\r", "");
            comment = comment.Replace("\n", "");
            FileTools.AddText(fs, $"{comment}\n");

            fs.Close();
        }

        public static void AutoAddMetadata(ref IOutput output)
        {
            string[] allFiles = Directory.GetFiles(ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath);

            foreach (string file in allFiles)
            {
                if (file.Contains(".metadata"))
                {
                    Logger.Log.Info($"Get the metadata file, name is {file}.");

                    string[] metadata = System.IO.File.ReadAllLines(file);

                    string audioTitle = metadata[0];
                    string audioArtist = metadata[1];
                    string audioAlbum = metadata[2];
                    string audioGenre = metadata[3];
                    DateTime audioDate = DateTime.Parse(metadata[4]);
                    string audioImageUrl = metadata[5];
                    string audioComment = metadata[6];

                    string audioName = Path.GetDirectoryName(file) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(file);

                    if (System.IO.File.Exists(audioName))
                    {
                        Logger.Log.Info($"Audio file exists, and name is {audioName}.");

                        var audioFile = TagLib.File.Create(audioName);

                        audioFile.Tag.Title = audioTitle;
                        audioFile.Tag.AlbumArtists = new[] { audioArtist };
                        audioFile.Tag.Performers = new[] { audioArtist };
                        audioFile.Tag.Album = audioAlbum;
                        audioFile.Tag.Composers = new[] { audioArtist };
                        audioFile.Tag.Genres = new[] { audioGenre };
                        audioFile.Tag.DateTagged = audioDate;

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
                                DownloadHelper.DownloadFileAsync(audioImageUrl, imageName).Wait();
                                audioFile.Tag.Pictures = new IPicture[] { new Picture(imageName) };
                            }
                            catch (Exception e)
                            {
                                Logger.Log.Error($"Image of \"{audioName}\" download failed. Exception is {e.Message}.");
                                output.WriteLine($"Image of \"{audioName}\" download failed.");
                            }
                        }

                        audioFile.Tag.Comment = audioComment;

                        audioFile.Save();
                        output.WriteLine($"\"{file}\" 's metadata has been written.");

                        System.IO.File.Delete(file);
                        System.IO.File.Delete(imageName);
                    }
                    else
                    {
                        Logger.Log.Error($"Audio file does not exist, and name is \"{audioName}\".");
                        output.WriteLine($"\"{audioName}\" does NOT exist.");
                    }
                }
            }
        }
    }
}