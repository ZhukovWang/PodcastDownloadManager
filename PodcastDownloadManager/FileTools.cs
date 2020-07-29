using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PodcastDownloadManager
{
    public static class FileTools
    {
        public static void AddText(FileStream fs, string value)
        {
            byte[] info = new UTF8Encoding(true).GetBytes(value);
            fs.Write(info, 0, info.Length);
        }
    }
}