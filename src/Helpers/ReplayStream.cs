using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketBenchmarks
{
    public sealed class ReplayStream : Stream
    {
        private readonly Memory<byte> _data;
        private Memory<byte> _current;

        public ReplayStream(Memory<byte> data)
        {
            _data = data;
            _current = data;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => -1;

        public override long Position { get => 01; set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_current.IsEmpty)
            {
                _current = _data;
            }

            var length = Math.Min(buffer.Length, _current.Length);
            _current.Slice(0, length).CopyTo(buffer);
            _current = _current[length..];

            return new ValueTask<int>(length);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
