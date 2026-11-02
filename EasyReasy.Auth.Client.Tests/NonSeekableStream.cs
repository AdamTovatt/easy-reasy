namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// A read-only stream over fixed bytes that cannot seek, the way a network or pipe stream cannot.
    /// </summary>
    public class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;

        /// <summary>
        /// Initializes a new instance of the <see cref="NonSeekableStream"/> class over the given bytes.
        /// </summary>
        /// <param name="bytes">The bytes the stream reads.</param>
        public NonSeekableStream(byte[] bytes)
        {
            _inner = new MemoryStream(bytes, writable: false);
        }

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
