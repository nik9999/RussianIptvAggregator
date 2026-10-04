using System.Net;
using Polly;
using RussianIptvAggregator.Domain.Interfaces;

namespace RussianIptvAggregator.Infrastructure.Http;

/// <summary>
/// HTTP fetcher with short timeouts to avoid hanging on unreachable hosts.
/// </summary>
public sealed class ResilientStreamFetcher : IStreamFetcher, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly int _retryCount;
    private readonly TimeSpan _requestTimeout;
    private bool _disposed;

    public ResilientStreamFetcher(
        HttpClient httpClient,
        int retryCount = 0,
        TimeSpan? requestTimeout = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RussianIptvAggregator/1.0");
        _retryCount = retryCount;
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<string> DownloadTextAsync(string url, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var absoluteUrl = EnsureAbsoluteUrl(url);
        Console.WriteLine($"[ResilientStreamFetcher] DownloadTextAsync: {absoluteUrl}");

        var lastException = default(Exception);
        for (var attempt = 0; attempt <= _retryCount; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(_requestTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
                using var request = new HttpRequestMessage(HttpMethod.Get, absoluteUrl);
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync(linkedCts.Token);
                }

                if ((int)response.StatusCode >= 500)
                {
                    lastException = new HttpRequestException($"Server error: {(HttpStatusCode)response.StatusCode}");
                    await RetryBackoffAsync(attempt, cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(linkedCts.Token);
            }
            catch (OperationCanceledException ex) when (attempt < _retryCount)
            {
                Console.WriteLine($"[ResilientStreamFetcher] Timeout on attempt {attempt + 1}/{_retryCount + 1} for {absoluteUrl}");
                lastException = ex;
                await RetryBackoffAsync(attempt, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < _retryCount)
            {
                Console.WriteLine($"[ResilientStreamFetcher] HttpRequestException on attempt {attempt + 1}/{_retryCount + 1}: {ex.Message}");
                lastException = ex;
                await RetryBackoffAsync(attempt, cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException("Download failed after retries");
    }

    public async Task<byte[]> DownloadSegmentAsync(string url, int maxBytes, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var absoluteUrl = EnsureAbsoluteUrl(url);
        Console.WriteLine($"[ResilientStreamFetcher] DownloadSegmentAsync: {absoluteUrl} (max {maxBytes} bytes)");

        var lastException = default(Exception);
        for (var attempt = 0; attempt <= _retryCount; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(_requestTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
                using var request = new HttpRequestMessage(HttpMethod.Get, absoluteUrl);
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms, maxBytes, linkedCts.Token);
                    return ms.GetBuffer().AsSpan(0, (int)ms.Length).ToArray();
                }

                if ((int)response.StatusCode >= 500)
                {
                    lastException = new HttpRequestException($"Server error: {(HttpStatusCode)response.StatusCode}");
                    await RetryBackoffAsync(attempt, cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                using var segStream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
                using var segMs = new MemoryStream();
                await segStream.CopyToAsync(segMs, maxBytes, linkedCts.Token);
                return segMs.GetBuffer().AsSpan(0, (int)segMs.Length).ToArray();
            }
            catch (OperationCanceledException ex) when (attempt < _retryCount)
            {
                Console.WriteLine($"[ResilientStreamFetcher] Timeout on attempt {attempt + 1}/{_retryCount + 1} for {absoluteUrl}");
                lastException = ex;
                await RetryBackoffAsync(attempt, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < _retryCount)
            {
                Console.WriteLine($"[ResilientStreamFetcher] HttpRequestException on attempt {attempt + 1}/{_retryCount + 1}: {ex.Message}");
                lastException = ex;
                await RetryBackoffAsync(attempt, cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException("Download failed after retries");
    }

    private static string EnsureAbsoluteUrl(string url)
    {
        if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            return url;

        Console.WriteLine($"[ResilientStreamFetcher] WARNING: URL is not absolute, prefixing with http://: {url}");
        return "http://" + url;
    }

    private static async Task RetryBackoffAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        await Task.Delay(delay, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}
