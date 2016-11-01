using System;
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

        private static string GetMachineIPAddress()
            => Dns.GetHostEntry(Environment.MachineName)
                  .AddressList
                  .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                  ?.ToString();
    }
}