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

        static UriExtensions()
        {
            WildcardTcpAddress = new Regex(@"tcp:\/\/\*:", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public static IEnumerable<Uri> GetAddressRange(this string uri)
        {
            var addressParts = uri.Split(":".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
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
            var match = WildcardTcpAddress.Match(uri);
            if (match.Success)
            {
                var ipAddress = GetMachineIPAddress();
                uri = (string.IsNullOrWhiteSpace(ipAddress))
                          ? uri
                          : uri.Replace(match.Value, $"tcp://{ipAddress}:");
            }

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

        private static string GetMachineIPAddress()
            => Dns.GetHostEntry(Environment.MachineName)
                  .AddressList
                  .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                 ?.ToString();
    }
}