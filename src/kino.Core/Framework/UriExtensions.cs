using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace kino.Core.Framework
{
    public static class UriExtensions
    {
        private static readonly Regex wildcardTcpAddress;

        static UriExtensions()
        {
            wildcardTcpAddress = new Regex(@"tcp:\/\/\*:", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public static string ToSocketAddress(this Uri uri)
            => uri.AbsoluteUri.TrimEnd('/');

        public static Uri TryParseAddress(this string uri)
        {
            var match = wildcardTcpAddress.Match(uri);
            if (match.Success)
            {
                var ipAddress = GetMachineIPAddress();
            }

            return new Uri(uri);
        }

        private static string GetMachineIPAddress()
            => Dns.GetHostEntry(Environment.MachineName).AddressList.FirstOrDefault(nic => !nic.IsIPv6SiteLocal)?.MapToIPv6();
    }
}