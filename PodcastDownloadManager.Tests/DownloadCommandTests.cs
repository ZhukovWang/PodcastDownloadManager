using System;
using PodcastDownloadManager.Commands;
using Xunit;

namespace PodcastDownloadManager.Tests
{
    public class DownloadCommandTests
    {
        [Fact]
        public void Date_is_parsed_as_local_midnight_and_converted_to_utc()
        {
            DateTime expected = DateTime.SpecifyKind(new DateTime(2024, 1, 1), DateTimeKind.Local)
                .ToUniversalTime();

            Assert.True(DownloadCommand.TryParseDate("20240101", out DateTime date));
            Assert.Equal(DateTimeKind.Utc, date.Kind);
            Assert.Equal(expected, date);
        }

        [Theory]
        [InlineData("2024-01-01")]
        [InlineData("2024011")]
        [InlineData("")]
        public void Invalid_dates_are_rejected(string value)
        {
            Assert.False(DownloadCommand.TryParseDate(value, out _));
        }
    }
}
