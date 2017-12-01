using System.IO;

namespace AdultEmby.Plugins.Base
{
    internal static class Extensions
    {
        internal static string ToStringFromStream(this Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
