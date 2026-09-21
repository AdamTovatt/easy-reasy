using System.Net;
using System.Net.Sockets;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// A loopback host that behaves like the wrong address in the way that matters: it answers any auth path with a
    /// redirect to its own sign-in page, which then answers <c>200</c> with HTML.
    /// </summary>
    /// <remarks>
    /// A fake message handler cannot stand in for this. What the client reports as the address that answered comes
    /// from the handler's own redirect following, so only a real handler talking to a real redirect proves the
    /// reported URI is the one the request ended at rather than the one it started from.
    /// </remarks>
    internal sealed class SignInPageHost : IDisposable
    {
        /// <summary>The path the redirect points at, and so the path the reported URI must end at.</summary>
        public const string SignInPath = ".auth/login/aad";

        /// <summary>The page the sign-in path answers with.</summary>
        public const string SignInPageHtml = "<!DOCTYPE html>\n<html>\n  <head><title>Sign in</title></head>\n  <body>Sign in to continue</body>\n</html>";

        /// <summary>
        /// How many times <see cref="Start"/> picks a port before giving up. A free port can be taken between
        /// being found and being claimed — by any process on the machine, so one test at a time is no protection —
        /// and the answer to losing that race is to pick another one.
        /// </summary>
        private const int PortAttempts = 10;

        private readonly HttpListener _listener;

        /// <summary>
        /// The serve loop, or <c>null</c> before <see cref="Start"/> has begun it. Begun there rather than in the
        /// constructor: <see cref="Start"/> is the factory, and constructing an object should not also start work.
        /// </summary>
        private Task? _serving;

        /// <summary>The address to point a client at, with the trailing slash a base address needs.</summary>
        public string BaseAddress { get; }

        private SignInPageHost(HttpListener listener, string baseAddress)
        {
            _listener = listener;
            BaseAddress = baseAddress;
        }

        /// <summary>
        /// Starts the host on a free loopback port.
        /// </summary>
        /// <returns>The running host, which stops when disposed.</returns>
        public static SignInPageHost Start()
        {
            for (int attempt = 1; ; attempt++)
            {
                string baseAddress = $"http://localhost:{FindFreePort()}/";
                HttpListener listener = new HttpListener();
                listener.Prefixes.Add(baseAddress);

                try
                {
                    listener.Start();
                }
                catch (HttpListenerException) when (attempt < PortAttempts)
                {
                    // Something else claimed the port between finding it and starting here. Pick another.
                    listener.Close();
                    continue;
                }

                SignInPageHost host = new SignInPageHost(listener, baseAddress);
                host.BeginServing();

                return host;
            }
        }

        /// <summary>
        /// Starts the serve loop.
        /// </summary>
        private void BeginServing()
        {
            _serving = Task.Run(ServeAsync);
        }

        /// <summary>
        /// Answers requests until the listener is stopped.
        /// </summary>
        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = await _listener.GetContextAsync();
                    await RespondAsync(context);
                }
                catch (Exception) when (!_listener.IsListening)
                {
                    // Disposal is what ends the loop: stopping the listener faults whatever the loop was in the
                    // middle of, whether that was waiting for a request or answering one. A failure while the
                    // listener is still listening is not that, and is left to fault the task so that Dispose
                    // reports it rather than a later test failing for reasons it cannot see.
                    return;
                }
            }
        }

        /// <summary>
        /// Answers one request: the sign-in page on the sign-in path, and a redirect to it on anything else.
        /// </summary>
        /// <param name="context">The request to answer.</param>
        private static async Task RespondAsync(HttpListenerContext context)
        {
            if (context.Request.Url?.AbsolutePath.EndsWith(SignInPath, StringComparison.Ordinal) == true)
            {
                byte[] page = System.Text.Encoding.UTF8.GetBytes(SignInPageHtml);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.OutputStream.WriteAsync(page);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.Found;
                context.Response.RedirectLocation = $"/{SignInPath}";
            }

            context.Response.Close();
        }

        /// <summary>
        /// Finds a loopback port nothing is listening on.
        /// </summary>
        /// <returns>The port number, which anything on the machine may claim before the caller does.</returns>
        private static int FindFreePort()
        {
            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();

            try
            {
                return ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            finally
            {
                probe.Stop();
            }
        }

        /// <summary>
        /// Stops the host and releases its port.
        /// </summary>
        public void Dispose()
        {
            try
            {
                _listener.Stop();
                _serving?.Wait(TimeSpan.FromSeconds(5));
            }
            finally
            {
                // In a finally because a serve loop that faulted is exactly when the listener would otherwise be
                // left holding its port: the throw would leave this line unreached.
                _listener.Close();
            }
        }
    }
}
