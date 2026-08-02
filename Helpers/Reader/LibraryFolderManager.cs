using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Shinobu.Helpers.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Shinobu.Helpers.Reader
{
    public static class LibraryFolderManager
    {
        private const string LibraryFolderKey = "LibraryFolder";

        public static string GetLibraryFolderPath()
        {
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue(LibraryFolderKey, out object? value) && value is string path && !string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            settings.Values[LibraryFolderKey] = defaultPath;
            return defaultPath;
        }

        public static async Task EnsureLibraryFolderExistsAsync()
        {
            Directory.CreateDirectory(GetLibraryFolderPath());
            await Task.CompletedTask;
        }

        public static IEnumerable<string> GetSupportedFiles()
        {
            string folderPath = GetLibraryFolderPath();
            if (!Directory.Exists(folderPath))
            {
                return [];
            }

            return Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(path => SupportedFileTypes.Extensions.ContainsKey(Path.GetExtension(path).ToLowerInvariant()))
                .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static async Task CopyFilesToLibraryAsync(IEnumerable<string> sourcePaths)
        {
            string destinationFolder = GetLibraryFolderPath();
            Directory.CreateDirectory(destinationFolder);

            foreach (string sourcePath in sourcePaths)
            {
                await CopyFileToLibraryAsync(sourcePath);
            }
        }

        public static async Task CopyStorageFilesToLibraryAsync(IEnumerable<StorageFile> files)
        {
            await CopyFilesToLibraryAsync(files.Select(f => f.Path));
        }

        public static async Task CopyFileToLibraryAsync(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return;
            }

            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (!SupportedFileTypes.Extensions.ContainsKey(ext))
            {
                return;
            }

            string destinationFolder = GetLibraryFolderPath();
            Directory.CreateDirectory(destinationFolder);

            string fileName = Path.GetFileName(sourcePath);
            string destinationPath = GetUniqueDestinationPath(destinationFolder, fileName);

            await Task.Run(() => File.Copy(sourcePath, destinationPath, overwrite: false));
        }

        public static async Task CopyFileToLibraryAsync(StorageFile file)
        {
            await CopyFileToLibraryAsync(file.Path);
        }

        public static async Task<List<string>> PickAndCopyFilesAsync(Window window)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

            foreach (var ext in SupportedFileTypes.Extensions.Keys)
            {
                picker.FileTypeFilter.Add(ext);
            }

            var files = await picker.PickMultipleFilesAsync();
            if (files is null || files.Count == 0)
            {
                return [];
            }

            await CopyStorageFilesToLibraryAsync(files);
            return [.. files.Select(f => f.Path)];
        }

        public static async Task<List<string>> PickAndCopyFolderAsync(Window window)
        {
            var picker = new FolderPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
            {
                return [];
            }

            var files = await GetSupportedFilesAsync(folder);
            await CopyStorageFilesToLibraryAsync(files);
            return [.. files.Select(f => f.Path)];
        }

        private static async Task<List<StorageFile>> GetSupportedFilesAsync(StorageFolder folder)
        {
            var files = new List<StorageFile>();
            var items = await folder.GetItemsAsync();
            foreach (var item in items)
            {
                if (item is StorageFile file && SupportedFileTypes.Extensions.ContainsKey(file.FileType.ToLower()))
                {
                    files.Add(file);
                }
                else if (item is StorageFolder subfolder)
                {
                    files.AddRange(await GetSupportedFilesAsync(subfolder));
                }
            }
            return files;
        }

        private static string GetUniqueDestinationPath(string folderPath, string fileName)
        {
            string destinationPath = Path.Combine(folderPath, fileName);
            if (!File.Exists(destinationPath))
            {
                return destinationPath;
            }

            string directory = Path.GetDirectoryName(destinationPath) ?? folderPath;
            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            int counter = 1;

            while (true)
            {
                string candidate = Path.Combine(directory, $"{name} ({counter}){ext}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
                counter++;
            }
        }
    }
}


