using System.Text.RegularExpressions;

namespace WixFolderHarvester.Helpers
{
    internal static class PathHelper
    {
        public static string ConvertToUnixPath(string path)
        {
            return Regex.Replace(path, @"\\", @"/");
        }

        public static string GetRelativePath(string absolutePath, string baseDirectory)
        {
            return absolutePath.Substring(baseDirectory.Length + 1);
        }
    }
}
