using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PodcastDownloadManager.Tests.Support
{
    /// <summary>
    /// A loopback HTTP server standing in for a podcast host. It runs on a plain socket, so the
    /// download code is exercised for real (headers, status codes, body streaming) without any
    /// outbound network access.
    /// </summary>
    internal sealed class FakeFeedServer : IDisposable
    {
        private readonly TcpListener _listener;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly ConcurrentDictionary<string, Route> _routes = new ConcurrentDictionary<string, Route>();

        private readonly ConcurrentDictionary<string, string> _userAgents =
            new ConcurrentDictionary<string, string>();

        private readonly int _port;

        public FakeFeedServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();

            _port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public string Url(string path)
        {
            return $"http://127.0.0.1:{_port}/{Normalize(path).TrimStart('/')}";
        }

        /// <summary>
        /// Serves <paramref name="body"/> with 200, after an optional delay before the headers and
        /// another optional delay after the headers but before the body.
        /// </summary>
        public void Serve(string path, string body, int delayMs = 0, int bodyDelayMs = 0)
        {
            _routes[Normalize(path)] = new Route(body, delayMs, bodyDelayMs, 0, 200);
        }

        /// <summary>
        /// Always answers <paramref name="statusCode"/>.
        /// </summary>
        public void FailWith(string path, int statusCode)
        {
            _routes[Normalize(path)] = new Route(null, 0, 0, int.MaxValue, statusCode);
        }

        /// <summary>
        /// Answers <paramref name="statusCode"/> once, then serves <paramref name="body"/>.
        /// </summary>
        public void FailOnce(string path, int statusCode, string body = "")
        {
            _routes[Normalize(path)] = new Route(body, 0, 0, 1, statusCode);
        }

        public int Hits(string path)
        {
            return _routes.TryGetValue(Normalize(path), out Route route) ? route.Hits : 0;
        }

        /// <summary>
        /// The User-Agent header of the most recent request for <paramref name="path"/>.
        /// </summary>
        public string UserAgentFor(string path)
        {
            return _userAgents.TryGetValue(Normalize(path), out string userAgent) ? userAgent : null;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            _cts.Dispose();
        }

        private static string Normalize(string path)
        {
            return "/" + path.TrimStart('/');
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await _listener.AcceptTcpClientAsync(token);
                }
                catch (Exception)
                {
                    return;
                }

                _ = Task.Run(() => ServeAsync(client, token));
            }
        }

        private async Task ServeAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);

                    string requestLine = await reader.ReadLineAsync(token);

                    if (requestLine == null)
                    {
                        return;
                    }

                    string[] parts = requestLine.Split(' ');
                    string path = Normalize(parts.Length > 1 ? parts[1].Split('?')[0] : "/");

                    while (await reader.ReadLineAsync(token) is { } headerLine && !string.IsNullOrEmpty(headerLine))
                    {
                        const string prefix = "User-Agent:";

                        if (headerLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            _userAgents[path] = headerLine.Substring(prefix.Length).Trim();
                        }
                    }

                    int status;
                    string body;
                    int bodyDelayMs = 0;

                    if (!_routes.TryGetValue(path, out Route route))
                    {
                        status = 404;
                        body = "not found";
                    }
                    else if (route.ShouldFail())
                    {
                        status = route.FailureStatus;
                        body = route.Body ?? "error";
                    }
                    else
                    {
                        if (route.DelayMs > 0)
                        {
                            await Task.Delay(route.DelayMs, token);
                        }

                        status = 200;
                        body = route.Body ?? string.Empty;
                        bodyDelayMs = route.BodyDelayMs;
                    }

                    byte[] payload = Encoding.UTF8.GetBytes(body);
                    byte[] header = Encoding.ASCII.GetBytes(
                        $"HTTP/1.1 {status} {Reason(status)}\r\n" +
                        "Content-Type: application/rss+xml\r\n" +
                        $"Content-Length: {payload.Length}\r\n" +
                        "Connection: close\r\n\r\n");

                    await stream.WriteAsync(header, 0, header.Length, token);

                    if (bodyDelayMs > 0)
                    {
                        // Let the client see the headers before stalling on the body.
                        await stream.FlushAsync(token);
                        await Task.Delay(bodyDelayMs, token);
                    }

                    await stream.WriteAsync(payload, 0, payload.Length, token);
                    await stream.FlushAsync(token);
                }
                catch (Exception)
                {
                    // The client closed the connection; nothing to report in a test helper.
                }
            }
        }

        private static string Reason(int status)
        {
            switch (status)
            {
                case 200: return "OK";
                case 404: return "Not Found";
                case 429: return "Too Many Requests";
                case 500: return "Internal Server Error";
                case 503: return "Service Unavailable";
                default: return "Status";
            }
        }

        private sealed class Route
        {
            private readonly object _gate = new object();

            private int _failuresLeft;

            private int _hits;

            public Route(string body, int delayMs, int bodyDelayMs, int failuresLeft, int failureStatus)
            {
                Body = body;
                DelayMs = delayMs;
                BodyDelayMs = bodyDelayMs;
                _failuresLeft = failuresLeft;
                FailureStatus = failureStatus;
            }

            public string Body { get; }

            public int DelayMs { get; }

            public int BodyDelayMs { get; }

            public int FailureStatus { get; }

            public int Hits => Volatile.Read(ref _hits);

            public bool ShouldFail()
            {
                Interlocked.Increment(ref _hits);

                lock (_gate)
                {
                    if (_failuresLeft <= 0)
                    {
                        return false;
                    }

                    _failuresLeft--;
                    return true;
                }
            }
        }
    }
}
