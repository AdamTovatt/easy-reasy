using System.Net;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// Empty content that records whether it was disposed, so a test can tell whether the response holding it was.
    /// </summary>
    public class DisposeTrackingContent : HttpContent
    {
        /// <summary>
        /// Gets whether the content has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc />
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
