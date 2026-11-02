namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// A handler in front of another that writes its own per-request state into
    /// <see cref="HttpRequestMessage.Options"/> on every send, as tracing and resilience handlers do, and records
    /// whether each request already carried that state when it arrived.
    /// </summary>
    public class OptionStampingHandler : DelegatingHandler
    {
        /// <summary>
        /// The option this handler writes.
        /// </summary>
        public static readonly HttpRequestOptionsKey<string> StampKey = new HttpRequestOptionsKey<string>("handler-stamp");

        private readonly List<bool> _arrivedStamped = new List<bool>();

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionStampingHandler"/> class in front of
        /// <paramref name="innerHandler"/>.
        /// </summary>
        /// <param name="innerHandler">The handler requests are passed on to.</param>
        public OptionStampingHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        /// <summary>
        /// Gets, for each request in arrival order, whether it already carried <see cref="StampKey"/>.
        /// </summary>
        public IReadOnlyList<bool> ArrivedStamped => _arrivedStamped;

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _arrivedStamped.Add(request.Options.TryGetValue(StampKey, out string? _));
            request.Options.Set(StampKey, "stamped while sending");
            return base.SendAsync(request, cancellationToken);
        }
    }
}
