using System;
using System.IO;

namespace App2
{
    internal static class RepositoryPaths
    {
        public static string? TryFindRepositoryRoot()
        {
            var dir = AppContext.BaseDirectory;
            for (var i = 0; i < 12 && !string.IsNullOrEmpty(dir); i++)
            {
                if (File.Exists(Path.Combine(dir, "backend", "api.py")))
                {
                    return dir;
                }

                dir = Directory.GetParent(dir)?.FullName;
            }

            return null;
        }

        public static string? TryGetCookiesFilePath()
        {
            var root = TryFindRepositoryRoot();
            return root == null ? null : Path.Combine(root, "data", "cookies.json");
        }

        public static string? TryGetBackendDirectory()
        {
            var root = TryFindRepositoryRoot();
            return root == null ? null : Path.Combine(root, "backend");
        }
    }
}