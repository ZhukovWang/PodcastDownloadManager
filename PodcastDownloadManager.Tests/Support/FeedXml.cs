using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PodcastDownloadManager.Tests.Support
{
    /// <summary>
    /// Builds RSS documents for the tests, with the escaping handled by <see cref="XDocument"/>.
    /// </summary>
    internal static class FeedXml
    {
        private static readonly XNamespace Itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";

        public static string Rss(string channelTitle, params FeedItem[] items)
        {
            var channel = new XElement("channel",
                new XElement("title", channelTitle),
                new XElement("description", "channel description"),
                items.Select(item => item.ToXml()));

            var rss = new XElement("rss",
                new XAttribute("version", "2.0"),
                new XAttribute(XNamespace.Xmlns + "itunes", Itunes.NamespaceName),
                channel);

            var document = new XDocument(new XDeclaration("1.0", "utf-8", null), rss);

            // A StringWriter reports UTF-16, which would make XDocument write encoding="utf-16";
            // the bytes are then served as UTF-8 and the parser rejects them. Write through a
            // stream with an explicit UTF-8 encoding instead.
            using var stream = new MemoryStream();

            using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
                   {
                       Encoding = new UTF8Encoding(false),
                       Indent = false,
                   }))
            {
                document.Save(writer);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// An RSS document without a channel element.
        /// </summary>
        public static string RssWithoutChannel()
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?><rss version=\"2.0\"></rss>";
        }

        internal static XNamespace ItunesNamespace => Itunes;
    }

    internal sealed class FeedItem
    {
        public string Title;

        public DateTime? PublishedUtc;

        public string EnclosureUrl;

        public string Author;

        public string ItunesAuthor;

        public string ImageUrl;

        public string Description;

        public XElement ToXml()
        {
            var item = new XElement("item");

            if (Title != null)
            {
                item.Add(new XElement("title", Title));
            }

            if (PublishedUtc.HasValue)
            {
                item.Add(new XElement("pubDate", PublishedUtc.Value.ToString("R")));
            }

            if (EnclosureUrl != null)
            {
                item.Add(new XElement("enclosure",
                    new XAttribute("url", EnclosureUrl),
                    new XAttribute("type", "audio/mpeg"),
                    new XAttribute("length", "1")));
            }

            if (Author != null)
            {
                item.Add(new XElement("author", Author));
            }

            if (ItunesAuthor != null)
            {
                item.Add(new XElement(FeedXml.ItunesNamespace + "author", ItunesAuthor));
            }

            if (ImageUrl != null)
            {
                item.Add(new XElement(FeedXml.ItunesNamespace + "image", new XAttribute("href", ImageUrl)));
            }

            if (Description != null)
            {
                item.Add(new XElement("description", Description));
            }

            return item;
        }
    }
}
