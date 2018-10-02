namespace kino.Core.Framework
{
    public static class SyntacticSugar
    {
        public static T As<T>(this object value) where T : class
            => value as T;

        public static T Cast<T>(this object value) where T : class
            => (T) value;
    }
}