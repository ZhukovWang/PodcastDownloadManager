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

        /// <summary>
        /// Applies every pending metadata sidecar in the download directory.
        /// </summary>
        /// <returns>The number of sidecars that could not be applied.</returns>
        public static int AutoAddMetadata(ref IOutput output)
        {
            string downloadDirectory = ProgramConfiguration.DownloadConfigurations.DownloadPodcastPath;

            if (string.IsNullOrEmpty(downloadDirectory) || !Directory.Exists(downloadDirectory))
            {
                Logger.Log.Error($"The download directory \"{downloadDirectory}\" does not exist.");
                output.WriteError($"Download directory \"{downloadDirectory}\" does not exist.");
                return 1;
            }

            int failures = 0;

            string[] allFiles = Directory.GetFiles(downloadDirectory);

            foreach (string file in allFiles)
            {
                if (!file.Contains(".metadata"))
                {
                    continue;
                }

                Logger.Log.Info($"Get the metadata file, name is {file}.");

                try
                {
                    string[] metadata = System.IO.File.ReadAllLines(file);

                    if (metadata.Length < 7)
                    {
                        Logger.Log.Error($"The metadata file \"{file}\" does not have 7 lines.");
                        output.WriteError($"\"{file}\" is not a valid metadata file.");
                        failures++;
                        continue;
                    }

                    string audioTitle = metadata[0];
                    string audioArtist = metadata[1];
                    string audioAlbum = metadata[2];
                    string audioGenre = metadata[3];
                    DateTime audioDate = DateTime.Parse(metadata[4]);
                    string audioImageUrl = metadata[5];
                    string audioComment = metadata[6];

                    string audioName = Path.GetDirectoryName(file) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(file);

                    if (!System.IO.File.Exists(audioName))
                    {
                        Logger.Log.Error($"Audio file does not exist, and name is \"{audioName}\".");
                        output.WriteError($"\"{audioName}\" does NOT exist.");
                        failures++;
                        continue;
                    }

                    Logger.Log.Info($"Audio file exists, and name is \"{audioName}\".");

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
                            DownloadHelper.DownloadFileAsync(audioImageUrl, imageName).GetAwaiter().GetResult();
                            audioFile.Tag.Pictures = new IPicture[] { new Picture(imageName) };
                        }
                        catch (Exception e)
                        {
                            failures++;
                            Logger.Log.Error($"Image of \"{audioName}\" download failed. Exception is {e.Message}.");
                            output.WriteError($"Image of \"{audioName}\" download failed.");
                        }
                    }

                    audioFile.Tag.Comment = audioComment;

                    audioFile.Save();
                    output.WriteLine($"\"{file}\" 's metadata has been written.");

                    System.IO.File.Delete(file);
                    System.IO.File.Delete(imageName);
                }
                catch (Exception e)
                {
                    failures++;
                    Logger.Log.Error($"Write metadata of \"{file}\" failed. Exception is {e}");
                    output.WriteError($"Failed to write metadata for \"{file}\": {e.Message}");
                }
            }

            return failures;
        }
    }
}