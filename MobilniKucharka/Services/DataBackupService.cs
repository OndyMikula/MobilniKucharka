using System.IO.Compression;

namespace MobilniKucharka.Services
{
    public class DataBackupService
    {
        public async Task<string> ExportAsync(IProgress<double>? progress = null)
        {
            string sourceDir = FileSystem.AppDataDirectory;
            string exportPath = Path.Combine(FileSystem.CacheDirectory, $"MobilniKucharka_zaloha_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

            if (File.Exists(exportPath)) File.Delete(exportPath);

            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            int total = Math.Max(files.Length, 1);
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

            return exportPath;
        }

        public async Task ImportAsync(string zipFilePath, IProgress<double>? progress = null)
        {
            string targetDir = FileSystem.AppDataDirectory;

            await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(zipFilePath);
                int total = Math.Max(zip.Entries.Count, 1);
                int processed = 0;

                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        processed++;
                        continue;
                    }

                    string destPath = Path.Combine(targetDir, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    entry.ExtractToFile(destPath, overwrite: true);

                    processed++;
                    progress?.Report((double)processed / total);
                }
            });
        }
    }
}