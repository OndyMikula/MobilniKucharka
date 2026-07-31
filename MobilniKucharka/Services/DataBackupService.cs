using System.IO.Compression;

namespace MobilniKucharka.Services
{
    public static class DataBackupService
    {
        // Interní adresáře .NET/Android tooling (např. fast-deploy override), nikdy ne uživatelská data
        private static readonly string[] ExcludedFolderNames = [".__override__"];

        private static bool IsExcludedPath(string relativePath)
        {
            var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return segments.Any(s =>
                ExcludedFolderNames.Contains(s, StringComparer.OrdinalIgnoreCase) ||
                (s.StartsWith('.') && s.Length > 1));
        }

        public static async Task<string> ExportAsync(IProgress<double>? progress = null)
        {
            string sourceDir = FileSystem.AppDataDirectory;
            string exportPath = Path.Combine(FileSystem.CacheDirectory, $"MobilniKucharka_zaloha_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

            if (File.Exists(exportPath)) File.Delete(exportPath);

            var allFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            var files = allFiles
                .Where(f => !IsExcludedPath(Path.GetRelativePath(sourceDir, f)))
                .ToList();

            int total = Math.Max(files.Count, 1);
            int processed = 0;

            await Task.Run(() =>
            {
                using var zip = ZipFile.Open(exportPath, ZipArchiveMode.Create);
                foreach (var file in files)
                {
                    string relativePath = Path.GetRelativePath(sourceDir, file);
                    try
                    {
                        zip.CreateEntryFromFile(file, relativePath);
                    }
                    catch
                    {
                        // soubor dočasně uzamčený/nedostupný -> přeskočíme, ať export neshodíme kvůli jednomu souboru
                    }

                    processed++;
                    progress?.Report((double)processed / total);
                }
            });

            try
            {
                using var verify = ZipFile.OpenRead(exportPath);
                _ = verify.Entries.Count;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Export vytvořil poškozený soubor zálohy: {ex.Message}", ex);
            }

            return exportPath;
        }

        public static async Task ImportAsync(string zipFilePath, IProgress<double>? progress = null)
        {
            string targetDir = FileSystem.AppDataDirectory;

            await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(zipFilePath);
                int total = Math.Max(zip.Entries.Count, 1);
                int processed = 0;

                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name) || IsExcludedPath(entry.FullName))
                    {
                        processed++;
                        continue;
                    }

                    string destPath = Path.Combine(targetDir, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                    try
                    {
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                    catch
                    {
                        // soubor uzamčený systémem/tooling -> přeskočíme (nemělo by jít o uživatelská data)
                    }

                    processed++;
                    progress?.Report((double)processed / total);
                }
            });
        }
    }
}