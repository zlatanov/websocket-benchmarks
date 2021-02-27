using BenchmarkDotNet.Attributes;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketBenchmarks
{
    [MemoryDiagnoser]
    public class ReceiveBenchmark
    {
        [Params(true, false)]
        public bool IsServer { get; set; }

        [Params(0, 64, 128, 4096, 16 * 1024, 1_048_576)]
        public int MessageSize { get; set; }

        public WebSocket WebSocket { get; set; }

        private Memory<byte> Buffer { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            Buffer = new byte[MessageSize];
            new Random(0).NextBytes(Buffer.Span);

            var memoryStream = new MemoryStream();
            var temp = WebSocket.CreateFromStream(memoryStream, !IsServer, null, TimeSpan.Zero);

            temp.SendAsync(Buffer, WebSocketMessageType.Binary, true, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            WebSocket = WebSocket.CreateFromStream(new ReplayStream(memoryStream.ToArray()), IsServer, null, TimeSpan.Zero);
        }

        [Benchmark]
        public ValueTask<ValueWebSocketReceiveResult> Receive() => WebSocket.ReceiveAsync(Buffer, CancellationToken.None);
    }
}
