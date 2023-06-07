using System.IO;
using System.Linq;

namespace WixFolderHarvester.Helpers
{
    internal static class WixIgnoreHelper
    {
        public static string[] ReadIgnorePatterns(string ignoreFilePath)
        {
            var ignorePatterns = File.ReadAllLines(ignoreFilePath)
                .Select(line => line.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x)) // Remove blank lines
                .Where(x => !x.StartsWith("#"))  // Remove comments
                .ToArray();
            return ignorePatterns;
        }
    }
}
