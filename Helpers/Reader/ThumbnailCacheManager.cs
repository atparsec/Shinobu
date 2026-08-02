using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Shinobu.Helpers.Reader
{
    public static class ThumbnailCacheManager
    {
        private const string CacheFolderName = "thumbnail-cache";
        private const int ThumbnailSize = 256;

        public static async Task<ImageSource?> GetThumbnailAsync(string filePath)
        {
            try
            {
                string cachePath = GetCacheFilePath(filePath);
                if (File.Exists(cachePath))
                {
                    return await LoadImageAsync(cachePath);
                }

                StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(filePath);
                using StorageItemThumbnail? thumbnail = await sourceFile.GetThumbnailAsync(ThumbnailMode.SingleItem, ThumbnailSize, ThumbnailOptions.UseCurrentScale);
                if (thumbnail == null)
                {
                    return null;
                }

                await SaveThumbnailAsync(thumbnail, cachePath);
                return await LoadImageAsync(cachePath);
            }
            catch
            {
                return null;
            }
        }

        private static string GetCacheFilePath(string filePath)
        {
            string cacheFolder = Path.Combine(ApplicationData.Current.LocalFolder.Path, CacheFolderName);
            Directory.CreateDirectory(cacheFolder);

            if (!File.Exists(filePath))
            {
                return Path.Combine(cacheFolder, HashKey(filePath) + ".png");
            }

            FileInfo info = new(filePath);
            string key = string.Join("|", Path.GetFullPath(filePath).ToLowerInvariant(), info.Length.ToString(CultureInfo.InvariantCulture), info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            return Path.Combine(cacheFolder, HashKey(key) + ".png");
        }

        private static string HashKey(string value)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static async Task SaveThumbnailAsync(StorageItemThumbnail thumbnail, string cachePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath) ?? Path.Combine(ApplicationData.Current.LocalFolder.Path, CacheFolderName));

            using var source = thumbnail.AsStreamForRead();
            using FileStream destination = new(cachePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await source.CopyToAsync(destination);
        }

        private static async Task<ImageSource?> LoadImageAsync(string path)
        {
            try
            {
                var image = new BitmapImage();
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                await image.SetSourceAsync(stream.AsRandomAccessStream());
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}

