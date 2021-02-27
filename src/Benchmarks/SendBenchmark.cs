using BenchmarkDotNet.Attributes;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketBenchmarks
{
    [MemoryDiagnoser]
    public class SendBenchmark
    {
        [Params(true, false)]
        public bool IsServer { get; set; }

        [Params(0, 64, 128, 4 * 1024, 16 * 1024, 1_048_576)]
        public int MessageSize { get; set; }

        public WebSocket WebSocket { get; set; }

        private Memory<byte> Payload { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            WebSocket = WebSocket.CreateFromStream(Stream.Null, IsServer, null, TimeSpan.Zero);
            Payload = new byte[MessageSize];

            new Random(0).NextBytes(Payload.Span);
        }

        [Benchmark]
        public ValueTask Send() => WebSocket.SendAsync(Payload, WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None);
    }
}
