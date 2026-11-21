using System.Net;
using System.Net.Sockets;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Keeps the server's fetch of a client ID metadata document, whose URL an anonymous caller chooses, from reaching
    /// anything but the public internet: the connection is made only to an address this class calls public, checked on the
    /// address actually connected to, so a host name that resolves to a private address (or is re-pointed at one between
    /// lookups) is refused too.
    /// </summary>
    internal static class OAuthPublicAddress
    {
        /// <summary>
        /// Whether an address is a public unicast address: not unspecified, loopback, private (RFC 1918, unique local),
        /// shared (RFC 6598), link-local, multicast, broadcast, documentation, benchmarking or reserved. IPv4-mapped,
        /// NAT64 and 6to4 IPv6 addresses are judged by the IPv4 address they carry.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>Whether it is public.</returns>
        public static bool IsPublic(IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                return IsPublicIPv4(address.GetAddressBytes());
            }

            if (address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                return false;
            }

            if (address.IsIPv4MappedToIPv6)
            {
                return IsPublicIPv4(address.MapToIPv4().GetAddressBytes());
            }

            byte[] bytes = address.GetAddressBytes();

            // NAT64 (64:ff9b::/96) and 6to4 (2002::/16) reach an IPv4 address.
            if (bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xff && bytes[3] == 0x9b && bytes.Skip(4).Take(8).All(value => value == 0))
            {
                return IsPublicIPv4(bytes[12..16]);
            }

            if (bytes[0] == 0x20 && bytes[1] == 0x02)
            {
                return IsPublicIPv4(bytes[2..6]);
            }

            // Global unicast is 2000::/3; everything else (::, ::1, fc00::/7, fe80::/10, ff00::/8, ...) is not public.
            if ((bytes[0] & 0xe0) != 0x20)
            {
                return false;
            }

            // Documentation (2001:db8::/32), ORCHIDv2 and other IETF special purpose ranges inside 2001::/23, and Teredo
            // (2001::/32), which reaches an IPv4 address it obscures.
            if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] < 0x02)
            {
                return false;
            }

            if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Connects to the first public address a host resolves to, refusing when it resolves to none. Used as the fetch
        /// handler's connect callback.
        /// </summary>
        /// <param name="context">The connection's host and port.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The connected stream.</returns>
        /// <exception cref="HttpRequestException">The host resolves to no public address.</exception>
        public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            DnsEndPoint endPoint = context.DnsEndPoint;
            IPAddress[] addresses = IPAddress.TryParse(endPoint.Host, out IPAddress? literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(endPoint.Host, cancellationToken);

            IPAddress? target = addresses.FirstOrDefault(IsPublic);
            if (target == null)
            {
                throw new HttpRequestException($"The client ID metadata document host '{endPoint.Host}' does not resolve to a public address.");
            }

            Socket socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(target, endPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private static bool IsPublicIPv4(byte[] bytes)
        {
            byte first = bytes[0];
            byte second = bytes[1];

            return !(first == 0                                        // 0.0.0.0/8, this network
                || first == 10                                         // 10.0.0.0/8, private
                || (first == 100 && second >= 64 && second <= 127)     // 100.64.0.0/10, shared address space
                || first == 127                                        // 127.0.0.0/8, loopback
                || (first == 169 && second == 254)                     // 169.254.0.0/16, link-local (cloud metadata)
                || (first == 172 && second >= 16 && second <= 31)      // 172.16.0.0/12, private
                || (first == 192 && second == 0 && bytes[2] == 0)      // 192.0.0.0/24, IETF protocol assignments
                || (first == 192 && second == 0 && bytes[2] == 2)      // 192.0.2.0/24, documentation
                || (first == 192 && second == 88 && bytes[2] == 99)    // 192.88.99.0/24, 6to4 relay
                || (first == 192 && second == 168)                     // 192.168.0.0/16, private
                || (first == 198 && (second == 18 || second == 19))    // 198.18.0.0/15, benchmarking
                || (first == 198 && second == 51 && bytes[2] == 100)   // 198.51.100.0/24, documentation
                || (first == 203 && second == 0 && bytes[2] == 113)    // 203.0.113.0/24, documentation
                || first >= 224);                                      // multicast, reserved and broadcast
        }
    }
}
