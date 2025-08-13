namespace EasyReasy.KnowledgeBase.Tests.TestUtilities
{
    /// <summary>
    /// A stream wrapper that adds configurable delays to simulate slow I/O operations for testing.
    /// </summary>
    public sealed class SlowStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly int _delayMillisecondsPerRead;
        private readonly int _delayMillisecondsPerByte;

        /// <summary>
        /// Initializes a new instance of the <see cref="SlowStream"/> class.
        /// </summary>
        /// <param name="innerStream">The stream to wrap.</param>
        /// <param name="delayMillisecondsPerRead">Delay in milliseconds for each read operation.</param>
        /// <param name="delayMillisecondsPerByte">Additional delay in milliseconds per byte read.</param>
        public SlowStream(Stream innerStream, int delayMillisecondsPerRead = 10, int delayMillisecondsPerByte = 1)
        {
            _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
            _delayMillisecondsPerRead = delayMillisecondsPerRead;
            _delayMillisecondsPerByte = delayMillisecondsPerByte;
        }

        /// <summary>
        /// Creates a slow stream from a byte array.
        /// </summary>
        /// <param name="data">The byte array to wrap.</param>
        /// <param name="delayMillisecondsPerRead">Delay in milliseconds for each read operation.</param>
        /// <param name="delayMillisecondsPerByte">Additional delay in milliseconds per byte read.</param>
        /// <returns>A slow stream wrapping the byte array.</returns>
        public static SlowStream FromBytes(byte[] data, int delayMillisecondsPerRead = 10, int delayMillisecondsPerByte = 1)
        {
            MemoryStream memoryStream = new MemoryStream(data);
            return new SlowStream(memoryStream, delayMillisecondsPerRead, delayMillisecondsPerByte);
        }

        /// <summary>
        /// Creates a slow stream from a string.
        /// </summary>
        /// <param name="text">The string to wrap.</param>
        /// <param name="delayMillisecondsPerRead">Delay in milliseconds for each read operation.</param>
        /// <param name="delayMillisecondsPerByte">Additional delay in milliseconds per byte read.</param>
        /// <returns>A slow stream wrapping the string.</returns>
        public static SlowStream FromString(string text, int delayMillisecondsPerRead = 10, int delayMillisecondsPerByte = 1)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(text);
            return FromBytes(data, delayMillisecondsPerRead, delayMillisecondsPerByte);
        }

        private async Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default)
        {
            if (milliseconds > 0)
            {
                await Task.Delay(milliseconds, cancellationToken);
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // Delay for the read operation
            await DelayAsync(_delayMillisecondsPerRead, cancellationToken);

            // Read from inner stream
            int bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);

            // Additional delay based on bytes read
            if (bytesRead > 0)
            {
                await DelayAsync(bytesRead * _delayMillisecondsPerByte, cancellationToken);
            }

            return bytesRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Synchronous version - use Task.Run to simulate async behavior
            return Task.Run(async () => await ReadAsync(buffer, offset, count)).Result;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await DelayAsync(_delayMillisecondsPerRead, cancellationToken);
            await _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
            await DelayAsync(count * _delayMillisecondsPerByte, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Task.Run(async () => await WriteAsync(buffer, offset, count)).Wait();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await DelayAsync(_delayMillisecondsPerRead, cancellationToken);
            await _innerStream.FlushAsync(cancellationToken);
        }

        public override void Flush()
        {
            Task.Run(async () => await FlushAsync()).Wait();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanWrite => _innerStream.CanWrite;
        public override bool CanSeek => _innerStream.CanSeek;
        public override long Length => _innerStream.Length;
        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}