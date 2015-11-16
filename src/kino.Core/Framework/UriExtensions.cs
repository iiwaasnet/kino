using System;

namespace kino.Core.Framework
{
    public static class UriExtensions
    {
        public static string ToSocketAddress(this Uri uri)
            => uri.AbsoluteUri.TrimEnd('/');
    }
}