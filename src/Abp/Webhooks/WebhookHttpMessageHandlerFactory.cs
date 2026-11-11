using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Abp.Webhooks
{
    /// <summary>
    /// Creates the <see cref="HttpMessageHandler"/> used to send webhooks.
    /// When <see cref="IWebhooksConfiguration.IsPrivateNetworkProtectionEnabled"/> is true, the handler refuses to
    /// connect to loopback, private, link-local and other non-public addresses. The check runs on the resolved
    /// addresses of every connection (including redirects), so it can not be bypassed with hostnames or DNS rebinding.
    /// </summary>
    public static class WebhookHttpMessageHandlerFactory
    {
        public const string PrivateNetworkAddressNotAllowedMessage = "Sending webhooks to private network addresses is not allowed.";

        public static HttpMessageHandler Create(IWebhooksConfiguration webhooksConfiguration)
        {
            if (!webhooksConfiguration.IsPrivateNetworkProtectionEnabled)
            {
                return new HttpClientHandler();
            }

            return new SocketsHttpHandler
            {
                // A proxy would make the connect callback validate the proxy address instead of the webhook host
                UseProxy = false,
                ConnectCallback = ConnectToPublicAddressAsync
            };
        }

        /// <summary>
        /// Returns false for addresses that are not reachable on the public internet,
        /// such as loopback, private (RFC 1918), link-local, carrier-grade NAT, multicast and reserved ranges.
        /// </summary>
        public static bool IsPublicAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            if (IPAddress.IsLoopback(address))
            {
                return false;
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return IsPublicIPv6Address(address);
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                return IsPublicIPv4Address(address.GetAddressBytes());
            }

            return false;
        }

        private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
            if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
            {
                throw new HttpRequestException(PrivateNetworkAddressNotAllowedMessage);
            }

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                // Connect to the validated addresses, never resolve the host again
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private static bool IsPublicIPv6Address(IPAddress address)
        {
            if (address.Equals(IPAddress.IPv6Any) ||
                address.IsIPv6LinkLocal ||
                address.IsIPv6SiteLocal ||
                address.IsIPv6UniqueLocal ||
                address.IsIPv6Multicast)
            {
                return false;
            }

            var bytes = address.GetAddressBytes();

            // NAT64 (64:ff9b::/96) embeds an IPv4 address in the last 4 bytes
            if (bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xff && bytes[3] == 0x9b &&
                bytes.Skip(4).Take(8).All(b => b == 0))
            {
                return IsPublicIPv4Address(bytes.Skip(12).ToArray());
            }

            // 6to4 (2002::/16) embeds an IPv4 address in bytes 2-5
            if (bytes[0] == 0x20 && bytes[1] == 0x02)
            {
                return IsPublicIPv4Address(bytes.Skip(2).Take(4).ToArray());
            }

            return true;
        }

        private static bool IsPublicIPv4Address(byte[] bytes)
        {
            switch (bytes[0])
            {
                case 0: // 0.0.0.0/8 "this network"
                case 10: // 10.0.0.0/8 private
                case 127: // 127.0.0.0/8 loopback
                    return false;
                case 100 when (bytes[1] & 0xC0) == 64: // 100.64.0.0/10 carrier-grade NAT
                case 169 when bytes[1] == 254: // 169.254.0.0/16 link-local (cloud metadata endpoints)
                case 172 when (bytes[1] & 0xF0) == 16: // 172.16.0.0/12 private
                case 192 when bytes[1] == 168: // 192.168.0.0/16 private
                case 192 when bytes[1] == 0 && bytes[2] == 0: // 192.0.0.0/24 IETF protocol assignments
                case 198 when (bytes[1] & 0xFE) == 18: // 198.18.0.0/15 benchmarking
                    return false;
            }

            // 224.0.0.0/4 multicast, 240.0.0.0/4 reserved and 255.255.255.255 broadcast
            return bytes[0] < 224;
        }
    }
}
