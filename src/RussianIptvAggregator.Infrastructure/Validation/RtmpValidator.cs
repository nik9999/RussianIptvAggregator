using System.Net.Sockets;
using RussianIptvAggregator.Domain.Channels;
using RussianIptvAggregator.Domain.Interfaces;
using RussianIptvAggregator.Domain.Streams;

namespace RussianIptvAggregator.Infrastructure.Validation;

/// <summary>
/// RTMP validator that checks TCP connectivity to the RTMP host:port.
/// </summary>
public class RtmpValidator : IStreamValidator
{
    private readonly TimeSpan _connectTimeout;

    public RtmpValidator(TimeSpan? connectTimeout = null)
    {
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<StreamHealthResult> ValidateAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var uri = new Uri(channel.StreamUrl);
            var host = uri.Host;
            var port = uri.IsDefaultPort ? 1935 : uri.Port;

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, port, cancellationToken);

            if (tcpClient.Connected)
            {
                tcpClient.Close();
                return StreamHealthResult.Success(
                    "RTMP",
                    httpStatusCode: 200,
                    contentType: "application/x-mpegURL",
                    responseTime: stopwatch.Elapsed);
            }

            return StreamHealthResult.Failure("RTMP", "TCP connection refused", null, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            return StreamHealthResult.Failure("RTMP", "TCP connection failed", ex.Message, stopwatch.Elapsed);
        }
    }
}
