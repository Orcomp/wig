﻿namespace wig
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Spectre.Console;
    using ZstdNet;

    public static class Archiver
    {
        public static async Task CompressAsync(string path, int compressionLevel, bool overwrite, bool subfolder, string destination, bool remove, ProgressTask task)
        {
            using var options = new CompressionOptions(compressionLevel);
            using var compressor = new Compressor(options);
            var attr = File.GetAttributes(path);

            if (attr.HasFlag(FileAttributes.Directory))
            {
                var size = FileHelper.GetDirectorySize(path, subfolder);
                task.MaxValue = size;
                string[] filePaths = Directory.GetFiles(path);

                foreach (var filePath in filePaths.Where(filePaths => !filePaths.EndsWith(".zs")))
                {
                    await WriteCompressedDataAsync(filePath, compressor, overwrite, destination);
                    task.Value += new FileInfo(filePath).Length;
                    RemoveOriginal(filePath, remove);
                }
                if (subfolder)
                {
                    var subfolders = new DirectoryInfo(path).GetDirectories();
                    foreach (var folder in subfolders)
                    {
                        var files = Directory.GetFiles(folder.ToString());
                        foreach (var file in files.Where(files => !files.EndsWith(".zs")))
                        {
                            await WriteCompressedDataAsync(file, compressor, overwrite, destination);
                            task.Value += new FileInfo(file).Length;
                            RemoveOriginal(file, remove);
                        }
                    }
                }

                return;
            }

            task.MaxValue = 1;
            await WriteCompressedDataAsync(path, compressor, overwrite, destination);
            task.Value += 1;
            RemoveOriginal(path, remove);
        }

        private static async Task WriteCompressedDataAsync(string path, Compressor compressor, bool overwrite, string destination)
        {
            byte[] data = await File.ReadAllBytesAsync(path);
            var compressedBytes = compressor.Wrap(data);
            if (File.Exists($"{path}.zs") && !overwrite)
            {
                AnsiConsole.WriteLine($"A compressed file with the same name {Path.GetFileNameWithoutExtension(path)} already exists. Use the -o | --overwrite parameter to force overwrite.");
                return;
            }
            await FileHelper.WriteFileAsync(compressedBytes, path, destination, ".zs");
        }

        public static async Task DecompressAsync(string path, bool overwrite, bool subfolder, string destination, ProgressTask task)
        {
            using var decompressor = new Decompressor();

            FileAttributes attr = File.GetAttributes(path);
            if (attr.HasFlag(FileAttributes.Directory))
            {
                string[] filePaths = Directory.GetFiles(path);
                var size = FileHelper.GetDirectorySize(path, subfolder, ".zs");
                task.MaxValue = size;
                foreach (var filePath in filePaths.Where(filePaths => filePaths.EndsWith(".zs")))
                {
                    await WriteDecompressedDataAsync(filePath, decompressor, overwrite, false, destination);
                    task.Value += new FileInfo(filePath).Length;
                }
                if (subfolder)
                {
                    var subfolders = new DirectoryInfo(path).GetDirectories();
                    foreach (var folder in subfolders)
                    {
                        var files = Directory.GetFiles(folder.ToString());
                        foreach (var file in files.Where(files => files.EndsWith(".zs")))
                        {
                            await WriteDecompressedDataAsync(file, decompressor, overwrite, false, destination);
                            task.Value += new FileInfo(file).Length;
                        }
                    }
                }

                return;
            }

            task.MaxValue = 1;
            await WriteDecompressedDataAsync(path, decompressor, overwrite, false, destination);
            task.Value += 1;
        }
        private static async Task WriteDecompressedDataAsync(string path, Decompressor decompressor, bool overwrite, bool remove, string destination)
        {
            byte[] compressedData = await File.ReadAllBytesAsync($"{path}");
            var decompressedBytes = decompressor.Unwrap(compressedData);
            var unpackingPath = Path.ChangeExtension(path, "");

            if (File.Exists($"{path}") && !overwrite)
            {
                AnsiConsole.WriteLine($"A decompressed file with the same name {Path.GetFileNameWithoutExtension(path)} already exists. Use the -o | --overwrite parameter to force overwrite.");
                return;
            }
            await FileHelper.WriteFileAsync(decompressedBytes, unpackingPath, destination);
        }

        public static void RemoveOriginal(string path, bool remove)
        {
            if (remove && !path.EndsWith(".zs"))
            {
                File.Delete(path);
            }
        }
    }
}
