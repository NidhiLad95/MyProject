using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GenxAi_Solutions.Utils
{
    /// <summary>
    /// Retries on transient server errors (5xx, 408) and DNS/host-not-found glitches.
    /// No external packages required.
    /// </summary>
    public sealed class DnsAndTransientRetryHandler : DelegatingHandler
    {
        private readonly int _maxRetries;
        private readonly TimeSpan[] _delays;

        public DnsAndTransientRetryHandler(int maxRetries = 3)
        {
            _maxRetries = Math.Max(0, maxRetries);
            _delays = new[]
            {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5)
            };
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    var response = await base.SendAsync(request, ct);

                    // Retry for transient server/timeouts
                    if (response.StatusCode == HttpStatusCode.RequestTimeout ||
                        (int)response.StatusCode >= 500)
                    {
                        if (attempt < _maxRetries)
                        {
                            await Task.Delay(_delays[Math.Min(attempt, _delays.Length - 1)], ct);
                            continue;
                        }
                    }

                    return response;
                }
                catch (HttpRequestException ex) when (ex.InnerException is SocketException se &&
                       (se.SocketErrorCode == SocketError.HostNotFound || se.SocketErrorCode == SocketError.TryAgain))
                {
                    if (attempt < _maxRetries)
                    {
                        await Task.Delay(_delays[Math.Min(attempt, _delays.Length - 1)], ct);
                        continue;
                    }
                    throw;
                }
            }
        }
    }
}
