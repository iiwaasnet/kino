using System;

namespace rawf.Framework
{
    public static class UriExtensions
    {
        public static string ToSocketAddress(this Uri uri)
        {
            return uri.AbsoluteUri.TrimEnd('/');
        }
    }
}