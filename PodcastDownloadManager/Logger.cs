using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace PodcastDownloadManager
{
    public static class Logger
    {
        public static readonly NLog.Logger Log = NLog.LogManager.GetLogger("logfile");
    }
}