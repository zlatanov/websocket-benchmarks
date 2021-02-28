using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketBenchmarks
{
    [MemoryDiagnoser]
    public class BroadcastBenchmark
    {
        private IWebHost _host;
        private Memory<byte> _clientInput;

        [Params(50, 100, 1000)]
        public int MessageSize { get; set; }

        [Params(10_000)]
        public int MessageCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _host = WebHost.CreateDefaultBuilder()
                 .ConfigureLogging(builder => builder.ClearProviders())
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
                             new Random(0).NextBytes(buffer.Span);

                             for (var i = 0; i < MessageCount; ++i)
                             {
                                 await websocket.SendAsync(buffer, WebSocketMessageType.Binary, true, default).ConfigureAwait(false);
                             }

                             await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
                         }
                         catch (Exception ex)
                         {
                             Environment.FailFast(ex.Message, ex);
                         }
                     });
                 })
                 .Build();

            _host.Start();

            _clientInput = new byte[MessageSize];
        }

        [GlobalCleanup]
        public async Task Cleanup()
        {
            await _host.StopAsync().ConfigureAwait(false);
            _host.Dispose();
        }

        [Benchmark]
        public async Task Broadcast()
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            using var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri("ws://localhost:5000"), CancellationToken.None).ConfigureAwait(false);

            var result = await client.ReceiveAsync(_clientInput, cancellation.Token).ConfigureAwait(false);
            int messageCount = 0;

            while (result.MessageType != WebSocketMessageType.Close)
            {
                var receivedByteCount = result.Count;
                while (!result.EndOfMessage)
                {
                    result = await client.ReceiveAsync(_clientInput.Slice(receivedByteCount), cancellation.Token).ConfigureAwait(false);
                    receivedByteCount += result.Count;
                }
                messageCount += 1;
                result = await client.ReceiveAsync(_clientInput, cancellation.Token).ConfigureAwait(false);
            }

            if (messageCount != MessageCount)
            {
                Environment.FailFast($"Unexpected message count {messageCount}.");
            }

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellation.Token).ConfigureAwait(false);
        }
    }
}
