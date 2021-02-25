using BenchmarkDotNet.Attributes;
using System;
using System.IO;
using System.Net.WebSockets;

namespace WebSocketBenchmarks
{
    [MemoryDiagnoser]
    public class CreateFromStreamBenchmark
    {
        [Benchmark]
        public void CreateFromStream() => WebSocket.CreateFromStream(Stream.Null, isServer: true, null, TimeSpan.Zero);
    }
}
