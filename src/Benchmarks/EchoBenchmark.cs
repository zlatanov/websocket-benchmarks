using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketBenchmarks
{
    [MemoryDiagnoser]
    public class EchoBenchmark
    {
        private IWebHost _host;
        private WebSocket _client;
        private Memory<byte> _clientInput;
        private Memory<byte> _clientOutput;

        [Params(50, 100, 1000)]
        public int MessageSize { get; set; }

        [Params(10_000)]
        public int MessageCount { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            _host = WebHost.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<SocketTransportOptions>(options =>
                    {
                        options.UnsafePreferInlineScheduling = true;
                    });
                })
                .Configure(app =>
                {
                    app.UseWebSockets(new WebSocketOptions
                    {
                        KeepAliveInterval = Timeout.InfiniteTimeSpan
                    });
                    app.Use(async (context, next) =>
                    {
                        try
                        {
                            using var websocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);

                            Memory<byte> buffer = new byte[MessageSize];
                            var result = await websocket.ReceiveAsync(buffer, default).ConfigureAwait(false);

                            while (result.MessageType != WebSocketMessageType.Close)
                            {
                                var receivedByteCount = result.Count;
                                while (!result.EndOfMessage)
                                {
                                    result = await websocket.ReceiveAsync(buffer.Slice(receivedByteCount), default).ConfigureAwait(false);
                                    receivedByteCount += result.Count;
                                }

                                await websocket.SendAsync(buffer, WebSocketMessageType.Binary, true, default).ConfigureAwait(false);
                                result = await websocket.ReceiveAsync(buffer, default).ConfigureAwait(false);
                            }

                            await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Environment.FailFast(ex.Message, ex);
                        }
                    });
                }).Build();

            _host.Start();

            var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri("ws://localhost:5000"), CancellationToken.None).ConfigureAwait(false);

            _client = client;
            _clientInput = new byte[MessageSize];
            _clientOutput = new byte[MessageSize];

            new Random(0).NextBytes(_clientOutput.Span);
        }

        [GlobalCleanup]
        public async Task Cleanup()
        {
            await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
            await _host.StopAsync().ConfigureAwait(false);
            _host.Dispose();
            _client.Dispose();
        }

        [Benchmark]
        public async Task Echo()
        {
            for (var i = 0; i < MessageCount; ++i)
            {
                await _client.SendAsync(_clientOutput, WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);
                var response = await _client.ReceiveAsync(_clientInput, CancellationToken.None).ConfigureAwait(false);

                if (!response.EndOfMessage || response.Count != _clientOutput.Length)
                {
                    Environment.FailFast($"Unexpected end of message or different response message size: {response.Count}, eof: {response.EndOfMessage}.");
                }
            }
        }
    }
}
