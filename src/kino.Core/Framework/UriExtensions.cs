using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace kino.Core.Framework
{
    public static class UriExtensions
    {
        private static readonly Regex WildcardTcpAddress;
        private static readonly char[] AddressPortSeparator = {':'};

        static UriExtensions()
            => WildcardTcpAddress = new Regex(@"tcp:\/\/\*:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static IEnumerable<Uri> GetAddressRange(this string uri)
        {
            var addressParts = uri.Split(AddressPortSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (addressParts.Length != 3)
            {
                throw new FormatException(uri);
            }
            var host = $"{addressParts[0]}:{addressParts[1]}";

            return GetPortRange(addressParts[2]).Select(p => $"{host}:{p}".ParseAddress());
        }

        public static string ToSocketAddress(this Uri uri)
            => uri.AbsoluteUri.TrimEnd('/');

        public static Uri ParseAddress(this string uri)
        {
            var tmp = WildcardTcpAddress.Match(uri).Success
                          ? ExpandWildcardUri(uri, WildcardTcpAddress.Match(uri))
                          : BuildIpAddressUri(uri);

            if (tmp.IsLoopback)
            {
                throw new Exception($"Uri [{uri}] should not resolve to loopback [{tmp.AbsoluteUri}]! " +
                                    $"Check that host file has no entry mapping name to a loopback address.");
            }

            return tmp;
        }

        private static Uri BuildIpAddressUri(this string uri)
            => new Uri(uri).BuildIpAddressUri();

        public static Uri BuildIpAddressUri(this Uri uri)
        {
            if (NotIpAddressOrLoopback())
            {
                var ipAddress = GetHostIpAddress(uri.IsLoopback
                                                     ? Environment.MachineName
                                                     : uri.Host);
                return new Uri($"{uri.Scheme}://{ipAddress}:{uri.Port}");
            }

            return uri;

            bool NotIpAddressOrLoopback()
                => !IPAddress.TryParse(uri.Host, out var _) || uri.IsLoopback;
        }

        private static Uri ExpandWildcardUri(string uri, Capture match)
        {
            var ipAddress = GetHostIpAddress(Environment.MachineName);
            uri = (string.IsNullOrWhiteSpace(ipAddress))
                      ? uri
                      : uri.Replace(match.Value, $"tcp://{ipAddress}:");

            return new Uri(uri);
        }

        private static IEnumerable<int> GetPortRange(string portRange)
        {
            var ports = portRange.Split("-".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                                 .Select(p => int.Parse(p.Trim()));
            var firstPort = ports.First();
            var lastPort = ports.Skip(1).FirstOrDefault();

            return Enumerable.Range(firstPort, Math.Abs(lastPort - firstPort) + 1);
        }

        private static string GetHostIpAddress(string host)
            => Dns.GetHostEntry(host)
                  .AddressList
                  .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                 ?.ToString();
    }
}