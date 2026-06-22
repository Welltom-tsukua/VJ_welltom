using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed partial class BeatLinkOverlayClient : MonoBehaviour
{
    private void InitializeMediaBrowser()
    {
        if (!string.IsNullOrEmpty(mediaRootPath))
        {
            ConfigureMediaRootWatcher(mediaRootPath);
            RefreshMediaFolderCache();
            mediaRootScanStarted = false;
            return;
        }

        SetMediaRoot(ChooseDefaultMediaRoot());
    }

    private static string ChooseDefaultMediaRoot()
    {
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (!string.IsNullOrEmpty(videos) && Directory.Exists(videos))
        {
            return videos;
        }

        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(user) && Directory.Exists(user))
        {
            return user;
        }

        return Application.dataPath;
    }

    private void SetMediaRoot(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            mediaBrowserError = "Root folder not found.";
            return;
        }

        mediaRootPath = Path.GetFullPath(path);
        mediaCurrentFolder = mediaRootPath;
        mediaRootInput = mediaRootPath;
        cachedMediaFolder = null;
        mediaPageIndex = 0;
        mediaBrowserError = null;
        selectedBrowserMediaPaths.Clear();
        mediaSelectionAnchorIndex = -1;
        mediaFolderChildrenCache.Clear();
        mediaFolderHasChildrenCache.Clear();
        mediaFolderSnapshotCache.Clear();
        ResetVideoThumbnailCache();
        ConfigureMediaRootWatcher(mediaRootPath);
        EnsureDefaultMediaPointerList();
        selectedMediaPointerListIndex = 0;
        mediaRootScanStarted = false;
        StartRootVideoScan(mediaRootPath);
        SaveMediaLibraryCache();
    }

    private void EnsureDefaultMediaPointerList()
    {
        if (mediaPointerLists.Count > 0)
        {
            return;
        }

        mediaPointerLists.Add(new MediaPointerListRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "All Imported"
        });
    }

    private void RebuildImportedMediaCacheFromSnapshots()
    {
        cachedFolders.Clear();
        cachedVideos.Clear();
        cachedImages.Clear();

        var allVideos = new List<string>();
        var allImages = new List<string>();
        foreach (var pair in mediaFolderSnapshotCache)
        {
            var entry = pair.Value;
            if (entry == null)
            {
                continue;
            }

            if (entry.Videos != null)
            {
                allVideos.AddRange(entry.Videos);
            }
            if (entry.Images != null)
            {
                allImages.AddRange(entry.Images);
            }
        }

        allVideos.Sort(StringComparer.OrdinalIgnoreCase);
        allImages.Sort(StringComparer.OrdinalIgnoreCase);
        DeduplicateInPlace(allVideos);
        DeduplicateInPlace(allImages);
        cachedVideos.AddRange(allVideos);
        cachedImages.AddRange(allImages);
        browserPreviewSlotsDirty = true;
    }

    private static void DeduplicateInPlace(List<string> paths)
    {
        if (paths == null || paths.Count <= 1)
        {
            return;
        }

        var write = 1;
        for (var read = 1; read < paths.Count; read++)
        {
            if (!string.Equals(paths[read - 1], paths[read], StringComparison.OrdinalIgnoreCase))
            {
                paths[write++] = paths[read];
            }
        }

        if (write < paths.Count)
        {
            paths.RemoveRange(write, paths.Count - write);
        }
    }

    private List<string> DisplayedVideos()
    {
        var source = selectedMediaPointerListIndex <= 0 ? cachedVideos : ResolvePointerListMedia(selectedMediaPointerListIndex, true);
        var filtered = new List<string>(source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            var path = source[i];
            if (MatchesVideoBrowserFilter(path))
            {
                filtered.Add(path);
            }
        }
        return filtered;
    }

    private List<string> DisplayedImages()
    {
        return selectedMediaPointerListIndex <= 0 ? new List<string>(cachedImages) : ResolvePointerListMedia(selectedMediaPointerListIndex, false);
    }

    private List<string> ResolvePointerListMedia(int listIndex, bool videos)
    {
        var resolved = new List<string>();
        if (listIndex < 0 || listIndex >= mediaPointerLists.Count)
        {
            return resolved;
        }

        var list = mediaPointerLists[listIndex];
        if (list == null || list.Paths == null)
        {
            return resolved;
        }

        for (var i = 0; i < list.Paths.Count; i++)
        {
            var path = list.Paths[i];
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                continue;
            }

            if (videos ? IsVideoFile(path) : IsImageFile(path))
            {
                resolved.Add(path);
            }
        }

        resolved.Sort(StringComparer.OrdinalIgnoreCase);
        DeduplicateInPlace(resolved);
        return resolved;
    }

    private void CreateMediaPointerList()
    {
        EnsureDefaultMediaPointerList();
        var number = mediaPointerLists.Count;
        mediaPointerLists.Add(new MediaPointerListRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "List " + number.ToString(CultureInfo.InvariantCulture)
        });
        selectedMediaPointerListIndex = mediaPointerLists.Count - 1;
        SaveMediaLibraryCache();
    }

    private void DeleteSelectedMediaPointerList()
    {
        if (selectedMediaPointerListIndex <= 0 || selectedMediaPointerListIndex >= mediaPointerLists.Count)
        {
            return;
        }

        mediaPointerLists.RemoveAt(selectedMediaPointerListIndex);
        selectedMediaPointerListIndex = 0;
        SaveMediaLibraryCache();
    }

    private void AddMediaPathsToPointerList(int listIndex, List<string> paths)
    {
        if (listIndex <= 0 || listIndex >= mediaPointerLists.Count || paths == null || paths.Count == 0)
        {
            return;
        }

        var list = mediaPointerLists[listIndex];
        if (list.Paths == null)
        {
            list.Paths = new List<string>();
        }

        var existing = new HashSet<string>(list.Paths, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (existing.Add(path))
            {
                list.Paths.Add(path);
            }
        }

        list.Paths.Sort(StringComparer.OrdinalIgnoreCase);
        SaveMediaLibraryCache();
    }

    private void SetMediaCurrentFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return;
        }

        mediaCurrentFolder = Path.GetFullPath(path);
        cachedMediaFolder = null;
        mediaPageIndex = 0;
        ExpandMediaFolderPath(mediaCurrentFolder);
        RefreshMediaFolderCache();
        SaveMediaLibraryCache();
    }

    private void RefreshMediaFolderCache()
    {
        if (string.Equals(cachedMediaFolder, mediaCurrentFolder, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        MediaFolderSnapshotRecord cachedRecord;
        if (!string.IsNullOrEmpty(mediaCurrentFolder) && mediaFolderSnapshotCache.TryGetValue(mediaCurrentFolder, out cachedRecord) && cachedRecord != null)
        {
            cachedFolders.Clear();
            cachedVideos.Clear();
            cachedImages.Clear();
            cachedMediaFolder = mediaCurrentFolder;
            mediaBrowserError = null;
            if (cachedRecord.ChildFolders != null)
            {
                cachedFolders.AddRange(cachedRecord.ChildFolders);
            }
            if (cachedRecord.Videos != null)
            {
                cachedVideos.AddRange(cachedRecord.Videos);
            }
            if (cachedRecord.Images != null)
            {
                cachedImages.AddRange(cachedRecord.Images);
            }
            browserPreviewSlotsDirty = true;
            return;
        }

        StartMediaFolderScan(mediaCurrentFolder);
    }

    private static bool IsVideoFile(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".mov", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMp4File(string path)
    {
        return string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMovFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".mov", StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesVideoBrowserFilter(string path)
    {
        switch (videoBrowserFilter)
        {
            case VideoBrowserFilter.Mp4Only:
                return IsMp4File(path);
            case VideoBrowserFilter.MovOnly:
                return IsMovFile(path);
            default:
                return IsVideoFile(path);
        }
    }

    private List<string> FilteredVideos()
    {
        var filtered = new List<string>(cachedVideos.Count);
        for (var i = 0; i < cachedVideos.Count; i++)
        {
            var path = cachedVideos[i];
            if (MatchesVideoBrowserFilter(path))
            {
                filtered.Add(path);
            }
        }
        return filtered;
    }

    private static bool IsImageFile(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        for (var i = 0; i < SupportedImageExtensions.Length; i++)
        {
            if (string.Equals(extension, SupportedImageExtensions[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateBrowserPreviewSlots()
    {
        if (browserPreviewSlots == null)
        {
            return;
        }

        for (var i = 0; i < browserPreviewSlots.Length; i++)
        {
            DestroyBrowserSlot(browserPreviewSlots[i]);
        }
        browserPreviewSlots = null;
    }

    private void UpdateBrowserPreviewSlotsIfNeeded()
    {
        if (!browserPreviewSlotsDirty || !IsVideoBrowserVisible())
        {
            return;
        }

        browserPreviewSlotsDirty = false;
        UpdateBrowserPreviewSlots();
    }

    private void StartMediaFolderScan(string folder)
    {
        lock (mediaScanLock)
        {
            if (string.IsNullOrEmpty(folder))
            {
                cachedMediaFolder = folder;
                mediaBrowserError = "Folder not found.";
                return;
            }

            if (mediaScanInFlight && string.Equals(mediaScanFolder, folder, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            mediaScanFolder = folder;
            mediaScanInFlight = true;
            mediaBrowserError = "Scanning folder...";
            var scanFolder = folder;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var result = ScanMediaFolder(scanFolder);
                lock (mediaScanLock)
                {
                    pendingMediaFolderScan = result;
                    mediaScanInFlight = false;
                }
            });
        }
    }

    private static MediaFolderScanResult ScanMediaFolder(string folder)
    {
        var result = new MediaFolderScanResult
        {
            Folder = folder,
            Folders = new List<string>(),
            Videos = new List<string>(),
            Images = new List<string>()
        };

        try
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                result.Error = "Folder not found.";
                return result;
            }

            result.Folders.AddRange(Directory.GetDirectories(folder));
            result.Folders.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.GetFiles(folder))
            {
                if (IsVideoFile(file))
                {
                    result.Videos.Add(file);
                }
                else if (IsImageFile(file))
                {
                    result.Images.Add(file);
                }
            }

            result.Videos.Sort(StringComparer.OrdinalIgnoreCase);
            result.Images.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    private void ApplyPendingMediaFolderScan()
    {
        MediaFolderScanResult result = null;
        lock (mediaScanLock)
        {
            if (pendingMediaFolderScan != null)
            {
                result = pendingMediaFolderScan;
                pendingMediaFolderScan = null;
            }
        }

        if (result == null || !string.Equals(result.Folder, mediaCurrentFolder, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        cachedFolders.Clear();
        cachedVideos.Clear();
        cachedImages.Clear();
        cachedMediaFolder = result.Folder;
        mediaBrowserError = result.Error;
        if (result.Folders != null)
        {
            cachedFolders.AddRange(result.Folders);
        }
        if (result.Videos != null)
        {
            cachedVideos.AddRange(result.Videos);
        }
        if (result.Images != null)
        {
            cachedImages.AddRange(result.Images);
        }
        mediaFolderSnapshotCache[result.Folder] = new MediaFolderSnapshotRecord
        {
            Folder = result.Folder,
            ChildFolders = result.Folders == null ? new List<string>() : new List<string>(result.Folders),
            Videos = result.Videos == null ? new List<string>() : new List<string>(result.Videos),
            Images = result.Images == null ? new List<string>() : new List<string>(result.Images)
        };
        mediaFolderChildrenCache[result.Folder] = result.Folders == null ? Array.Empty<string>() : result.Folders.ToArray();
        mediaFolderHasChildrenCache[result.Folder] = result.Folders != null && result.Folders.Count > 0;
        browserPreviewSlotsDirty = true;
        SaveMediaLibraryCache();
    }

    private void StartRootVideoScan(string root)
    {
        lock (mediaScanLock)
        {
            if (string.IsNullOrEmpty(root) || rootThumbnailScanInFlight)
            {
                return;
            }

            rootThumbnailScanInFlight = true;
            var scanRoot = root;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var result = ScanRootVideos(scanRoot);
                lock (mediaScanLock)
                {
                    pendingRootVideoScan = result;
                    rootThumbnailScanInFlight = false;
                }
            });
        }
    }

    private static MediaLibraryScanResult ScanRootVideos(string root)
    {
        var result = new MediaLibraryScanResult
        {
            Root = root,
            Entries = new List<MediaFolderSnapshotRecord>()
        };

        try
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return result;
            }

            var pending = new Queue<string>();
            pending.Enqueue(root);
            while (pending.Count > 0)
            {
                var folder = pending.Dequeue();
                string[] childFolders;
                try
                {
                    childFolders = Directory.GetDirectories(folder);
                    Array.Sort(childFolders, StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    continue;
                }

                for (var i = 0; i < childFolders.Length; i++)
                {
                    pending.Enqueue(childFolders[i]);
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(folder);
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    continue;
                }

                var videos = new List<string>();
                var images = new List<string>();
                for (var i = 0; i < files.Length; i++)
                {
                    if (IsVideoFile(files[i]))
                    {
                        videos.Add(files[i]);
                    }
                    else if (IsImageFile(files[i]))
                    {
                        images.Add(files[i]);
                    }
                }

                result.Entries.Add(new MediaFolderSnapshotRecord
                {
                    Folder = folder,
                    ChildFolders = new List<string>(childFolders),
                    Videos = videos,
                    Images = images
                });
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    private void ApplyPendingRootVideoScan()
    {
        MediaLibraryScanResult result = null;
        lock (mediaScanLock)
        {
            if (pendingRootVideoScan != null)
            {
                result = pendingRootVideoScan;
                pendingRootVideoScan = null;
            }
        }

        if (result == null || !string.Equals(result.Root, mediaRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        mediaFolderSnapshotCache.Clear();
        mediaFolderChildrenCache.Clear();
        mediaFolderHasChildrenCache.Clear();
        if (result.Entries != null)
        {
            for (var i = 0; i < result.Entries.Count; i++)
            {
                var entry = result.Entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Folder))
                {
                    continue;
                }

                entry.Normalize();
                mediaFolderSnapshotCache[entry.Folder] = entry;
                var childFolders = entry.ChildFolders == null ? Array.Empty<string>() : entry.ChildFolders.ToArray();
                mediaFolderChildrenCache[entry.Folder] = childFolders;
                mediaFolderHasChildrenCache[entry.Folder] = childFolders.Length > 0;
            }
        }

        RebuildImportedMediaCacheFromSnapshots();
        SaveMediaLibraryCache();
    }

    private void LoadMediaLibraryCache()
    {
        if (string.IsNullOrEmpty(mediaLibraryCachePath) || !File.Exists(mediaLibraryCachePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(mediaLibraryCachePath, Encoding.UTF8);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var cacheFile = JsonUtility.FromJson<MediaLibraryCacheFile>(json);
            if (cacheFile == null || string.IsNullOrEmpty(cacheFile.RootPath))
            {
                return;
            }

            var root = Path.GetFullPath(cacheFile.RootPath);
            if (!Directory.Exists(root))
            {
                return;
            }

            mediaFolderSnapshotCache.Clear();
            mediaFolderChildrenCache.Clear();
            mediaFolderHasChildrenCache.Clear();

            if (cacheFile.Entries != null)
            {
                for (var i = 0; i < cacheFile.Entries.Count; i++)
                {
                    var entry = cacheFile.Entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.Folder))
                    {
                        continue;
                    }

                    entry.Normalize();
                    mediaFolderSnapshotCache[entry.Folder] = entry;
                    var childFolders = entry.ChildFolders == null ? Array.Empty<string>() : entry.ChildFolders.ToArray();
                    mediaFolderChildrenCache[entry.Folder] = childFolders;
                    mediaFolderHasChildrenCache[entry.Folder] = childFolders.Length > 0;
                }
            }

            mediaRootPath = root;
            mediaRootInput = root;
            mediaCurrentFolder = root;
            if (!string.IsNullOrEmpty(cacheFile.CurrentFolder))
            {
                var current = Path.GetFullPath(cacheFile.CurrentFolder);
                if (current.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    mediaCurrentFolder = current;
                }
            }

            cachedMediaFolder = null;
            mediaPageIndex = 0;
            if (cacheFile.PointerLists != null && cacheFile.PointerLists.Count > 0)
            {
                mediaPointerLists.Clear();
                for (var i = 0; i < cacheFile.PointerLists.Count; i++)
                {
                    var list = cacheFile.PointerLists[i];
                    if (list != null)
                    {
                        list.Normalize();
                        mediaPointerLists.Add(list);
                    }
                }
            }
            EnsureDefaultMediaPointerList();
            selectedMediaPointerListIndex = Mathf.Clamp(cacheFile.SelectedPointerListIndex, 0, Mathf.Max(0, mediaPointerLists.Count - 1));
            RebuildImportedMediaCacheFromSnapshots();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Media library cache load failed: " + ex.Message);
        }
    }

    private void SaveMediaLibraryCache()
    {
        if (string.IsNullOrEmpty(mediaLibraryCachePath) || string.IsNullOrEmpty(mediaRootPath))
        {
            return;
        }

        var cacheFile = new MediaLibraryCacheFile
        {
            RootPath = mediaRootPath,
            CurrentFolder = mediaCurrentFolder,
            SavedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Entries = new List<MediaFolderSnapshotRecord>(mediaFolderSnapshotCache.Count),
            PointerLists = new List<MediaPointerListRecord>(mediaPointerLists.Count),
            SelectedPointerListIndex = selectedMediaPointerListIndex
        };

        foreach (var pair in mediaFolderSnapshotCache)
        {
            if (pair.Value == null)
            {
                continue;
            }
            cacheFile.Entries.Add(pair.Value.Clone());
        }
        for (var i = 0; i < mediaPointerLists.Count; i++)
        {
            if (mediaPointerLists[i] != null)
            {
                cacheFile.PointerLists.Add(mediaPointerLists[i].Clone());
            }
        }
        cacheFile.SortEntries();

        var version = Interlocked.Increment(ref mediaLibraryCacheSaveVersion);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var json = JsonUtility.ToJson(cacheFile, true);
                var directory = Path.GetDirectoryName(mediaLibraryCachePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                lock (mediaLibraryCacheFileLock)
                {
                    if (version != mediaLibraryCacheSaveVersion)
                    {
                        return;
                    }

                    var tempPath = mediaLibraryCachePath + ".tmp";
                    File.WriteAllText(tempPath, json, Encoding.UTF8);
                    if (File.Exists(mediaLibraryCachePath))
                    {
                        File.Copy(tempPath, mediaLibraryCachePath, true);
                        File.Delete(tempPath);
                    }
                    else
                    {
                        File.Move(tempPath, mediaLibraryCachePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Media library cache save failed: " + ex.Message);
            }
        });
    }

    private void LoadGeneratorPresets()
    {
        generatorPresets.Clear();
        if (string.IsNullOrEmpty(generatorPresetConfigPath) || !File.Exists(generatorPresetConfigPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(generatorPresetConfigPath, Encoding.UTF8);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var library = JsonUtility.FromJson<GeneratorPresetLibraryFile>(json);
            if (library == null || library.Presets == null)
            {
                return;
            }

            for (var i = 0; i < library.Presets.Count; i++)
            {
                var preset = library.Presets[i];
                if (preset == null || string.IsNullOrEmpty(preset.Id))
                {
                    continue;
                }

                preset.ThumbnailTexture = LoadGeneratorPresetThumbnailTexture(preset.ThumbnailFileName);
                if (preset.State == null)
                {
                    preset.State = new GeneratorPresetState();
                }
                generatorPresets.Add(preset);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Generator preset load failed: " + ex.Message);
        }
    }

    private void SaveGeneratorPresets()
    {
        if (string.IsNullOrEmpty(generatorPresetConfigPath))
        {
            return;
        }

        try
        {
            var library = new GeneratorPresetLibraryFile
            {
                Presets = new List<GeneratorPresetRecord>(generatorPresets.Count)
            };

            for (var i = 0; i < generatorPresets.Count; i++)
            {
                var preset = generatorPresets[i];
                if (preset == null || string.IsNullOrEmpty(preset.Id))
                {
                    continue;
                }

                library.Presets.Add(new GeneratorPresetRecord
                {
                    Id = preset.Id,
                    Name = preset.Name,
                    ThumbnailFileName = preset.ThumbnailFileName,
                    SavedAtUtc = preset.SavedAtUtc,
                    State = preset.State
                });
            }

            var directory = Path.GetDirectoryName(generatorPresetConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(generatorPresetConfigPath, JsonUtility.ToJson(library, true), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Generator preset save failed: " + ex.Message);
        }
    }

    private string GeneratorPresetThumbnailPath(string thumbnailFileName)
    {
        if (string.IsNullOrEmpty(generatorPresetThumbnailRoot) || string.IsNullOrEmpty(thumbnailFileName))
        {
            return null;
        }

        return Path.Combine(generatorPresetThumbnailRoot, thumbnailFileName);
    }

    private Texture2D LoadGeneratorPresetThumbnailTexture(string thumbnailFileName)
    {
        try
        {
            var path = GeneratorPresetThumbnailPath(thumbnailFileName);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            var bytes = File.ReadAllBytes(path);
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }
        catch
        {
            return null;
        }
    }

    private void SaveGeneratorPresetThumbnail(string thumbnailFileName, Texture2D texture)
    {
        if (string.IsNullOrEmpty(generatorPresetThumbnailRoot) || string.IsNullOrEmpty(thumbnailFileName) || texture == null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(generatorPresetThumbnailRoot);
            var path = GeneratorPresetThumbnailPath(thumbnailFileName);
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Generator preset thumbnail save failed: " + ex.Message);
        }
    }

    private void UpdateMediaRootWatchRequests()
    {
        if (!IsMediaBrowserPanelVisible())
        {
            return;
        }

        if (!mediaRootRescanRequested || Time.unscaledTime < nextMediaRootRescanTime)
        {
            return;
        }

        mediaRootRescanRequested = false;
        nextMediaRootRescanTime = Time.unscaledTime + 2f;
        StartRootVideoScan(mediaRootPath);
        mediaRootScanStarted = true;
        cachedMediaFolder = null;
        RefreshMediaFolderCache();
    }

    private void EnsureMediaBrowserBackgroundWork()
    {
        if (!IsMediaBrowserPanelVisible())
        {
            return;
        }

        if (!mediaRootScanStarted && !string.IsNullOrEmpty(mediaRootPath))
        {
            StartRootVideoScan(mediaRootPath);
            mediaRootScanStarted = true;
        }
    }

    private bool IsMediaBrowserPanelVisible()
    {
        return activeScreen == 0 && !lowerPanelShowsEffects;
    }

    private void ConfigureMediaRootWatcher(string root)
    {
        if (mediaRootWatcher != null)
        {
            try
            {
                mediaRootWatcher.EnableRaisingEvents = false;
                mediaRootWatcher.Created -= HandleMediaRootFilesystemChanged;
                mediaRootWatcher.Deleted -= HandleMediaRootFilesystemChanged;
                mediaRootWatcher.Renamed -= HandleMediaRootFilesystemRenamed;
                mediaRootWatcher.Dispose();
            }
            catch
            {
            }
            mediaRootWatcher = null;
        }

        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            return;
        }

        try
        {
            mediaRootWatcher = new FileSystemWatcher(root);
            mediaRootWatcher.IncludeSubdirectories = true;
            mediaRootWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName;
            mediaRootWatcher.Created += HandleMediaRootFilesystemChanged;
            mediaRootWatcher.Deleted += HandleMediaRootFilesystemChanged;
            mediaRootWatcher.Renamed += HandleMediaRootFilesystemRenamed;
            mediaRootWatcher.EnableRaisingEvents = true;
        }
        catch
        {
            mediaRootWatcher = null;
        }
    }

    private void HandleMediaRootFilesystemChanged(object sender, FileSystemEventArgs args)
    {
        mediaRootRescanRequested = true;
    }

    private void HandleMediaRootFilesystemRenamed(object sender, RenamedEventArgs args)
    {
        mediaRootRescanRequested = true;
    }

    private void EnsureBrowserPreviewSlots(int count)
    {
        count = Mathf.Max(0, count);
        if (browserPreviewSlots != null && browserPreviewSlots.Length == count)
        {
            return;
        }

        var previous = browserPreviewSlots;
        browserPreviewSlots = new BrowserVideoSlot[count];
        for (var i = 0; i < count; i++)
        {
            browserPreviewSlots[i] = new BrowserVideoSlot();
        }

        if (previous == null)
        {
            return;
        }

        var copyCount = Math.Min(previous.Length, browserPreviewSlots.Length);
        for (var i = 0; i < copyCount; i++)
        {
            var oldSlot = previous[i];
            var newSlot = browserPreviewSlots[i];
            if (oldSlot == null || newSlot == null)
            {
                continue;
            }

            newSlot.Path = oldSlot.Path;
            newSlot.Texture = oldSlot.Texture;
            newSlot.ThumbnailReady = oldSlot.ThumbnailReady;
            newSlot.ThumbnailError = oldSlot.ThumbnailError;
            oldSlot.Texture = null;
        }

        for (var i = 0; i < previous.Length; i++)
        {
            DestroyBrowserSlot(previous[i]);
        }
    }

    private void AssignBrowserSlot(BrowserVideoSlot slot, string path)
    {
        if (slot == null)
        {
            return;
        }
        if (string.Equals(slot.Path, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        slot.Path = path;
        slot.ThumbnailReady = false;
        slot.ThumbnailError = null;
        if (slot.Texture != null)
        {
            Destroy(slot.Texture);
            slot.Texture = null;
        }

        VideoThumbnailCacheEntry cached;
        if (!string.IsNullOrEmpty(path) && videoThumbnailCache.TryGetValue(path, out cached))
        {
            slot.Texture = cached.Texture;
            slot.ThumbnailReady = cached.Ready;
            slot.ThumbnailError = cached.Error;
        }
        else if (!string.IsNullOrEmpty(path))
        {
            if (TryLoadVideoThumbnailCache(path, out cached))
            {
                videoThumbnailCache[path] = cached;
                slot.Texture = cached.Texture;
                slot.ThumbnailReady = cached.Ready;
                slot.ThumbnailError = cached.Error;
            }
            else if (IsVideoBrowserVisible())
            {
                EnqueueVideoThumbnail(path);
            }
        }
    }

    private bool IsVideoBrowserVisible()
    {
        return activeScreen == 0 && !lowerPanelShowsEffects && mediaBrowserMode == MediaBrowserMode.Videos;
    }

    private void ResetVideoThumbnailCache()
    {
        foreach (var entry in videoThumbnailCache.Values)
        {
            if (entry != null && entry.Texture != null)
            {
                Destroy(entry.Texture);
            }
        }
        videoThumbnailCache.Clear();
        videoThumbnailPreloadQueue.Clear();
        queuedVideoThumbnailPaths.Clear();
        pendingRootVideoScan = null;
        rootThumbnailScanInFlight = false;
        visibleBrowserVideoPaths.Clear();
    }

    private void EnqueueVideoThumbnail(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        if (videoThumbnailCache.ContainsKey(path) || !queuedVideoThumbnailPaths.Add(path))
        {
            return;
        }

        videoThumbnailPreloadQueue.Enqueue(path);
    }

    private void UpdateStaticBrowserThumbnails()
    {
        if (Time.unscaledTime < nextThumbnailWorkTime)
        {
            return;
        }

        var showBrowserVideos = IsVideoBrowserVisible() && visibleBrowserVideoPaths.Count > 0;
        if (!showBrowserVideos && videoThumbnailPreloadQueue.Count == 0)
        {
            return;
        }

        var criticalPlayback = HasCriticalSourcePlayback();
        if (showBrowserVideos)
        {
            for (var i = 0; i < visibleBrowserVideoPaths.Count; i++)
            {
                var path = visibleBrowserVideoPaths[i];
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                VideoThumbnailCacheEntry cached;
                if (videoThumbnailCache.TryGetValue(path, out cached))
                {
                    continue;
                }

                if (TryGenerateVideoThumbnail(path, criticalPlayback))
                {
                    nextThumbnailWorkTime = Time.unscaledTime + (criticalPlayback ? 0.6f : 0.08f);
                    return;
                }
            }
        }

        if (videoThumbnailPreloadQueue.Count > 0)
        {
            string path = null;
            while (videoThumbnailPreloadQueue.Count > 0 && string.IsNullOrEmpty(path))
            {
                var candidate = videoThumbnailPreloadQueue.Dequeue();
                queuedVideoThumbnailPaths.Remove(candidate);
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate) && !videoThumbnailCache.ContainsKey(candidate))
                {
                    path = candidate;
                }
            }

            if (!string.IsNullOrEmpty(path) && TryGenerateVideoThumbnail(path, criticalPlayback))
            {
                nextThumbnailWorkTime = Time.unscaledTime + (criticalPlayback ? 1.0f : 0.12f);
            }
        }
    }

    private bool TryGenerateVideoThumbnail(string path, bool lowPriority)
    {
        if (string.IsNullOrEmpty(path) || videoThumbnailCache.ContainsKey(path))
        {
            return false;
        }

        var entry = new VideoThumbnailCacheEntry();
        try
        {
            var thumbnail = NativeThumbnailExtractor.Extract(path, lowPriority ? 160 : 320, lowPriority ? 90 : 180);
            if (thumbnail != null && thumbnail.Pixels != null && thumbnail.Pixels.Length > 0)
            {
                entry.Texture = new Texture2D(thumbnail.Width, thumbnail.Height, TextureFormat.RGBA32, false);
                entry.Texture.LoadRawTextureData(thumbnail.Pixels);
                entry.Texture.Apply(false, true);
                entry.Texture.filterMode = FilterMode.Bilinear;
                entry.Texture.wrapMode = TextureWrapMode.Clamp;
                SaveVideoThumbnailCache(path, entry.Texture);
            }
            entry.Ready = true;
        }
        catch (Exception ex)
        {
            entry.Ready = true;
            entry.Error = ex.Message;
        }

        videoThumbnailCache[path] = entry;
        return true;
    }

    private bool TryLoadVideoThumbnailCache(string path, out VideoThumbnailCacheEntry entry)
    {
        entry = null;
        try
        {
            var cachePath = VideoThumbnailCachePath(path);
            if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath))
            {
                return false;
            }

            var bytes = File.ReadAllBytes(cachePath);
            if (bytes == null || bytes.Length == 0)
            {
                return false;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return false;
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            entry = new VideoThumbnailCacheEntry
            {
                Texture = texture,
                Ready = true
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SaveVideoThumbnailCache(string path, Texture2D texture)
    {
        if (texture == null || string.IsNullOrEmpty(path) || string.IsNullOrEmpty(mediaThumbnailCacheRoot))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(mediaThumbnailCacheRoot);
            var cachePath = VideoThumbnailCachePath(path);
            if (string.IsNullOrEmpty(cachePath))
            {
                return;
            }

            File.WriteAllBytes(cachePath, texture.EncodeToPNG());
        }
        catch
        {
        }
    }

    private string VideoThumbnailCachePath(string path)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(mediaThumbnailCacheRoot) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var info = new FileInfo(path);
            var key = StableTextHash(path + "|" + info.Length.ToString(CultureInfo.InvariantCulture) + "|" + info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)).ToString("x8", CultureInfo.InvariantCulture);
            return Path.Combine(mediaThumbnailCacheRoot, key + ".png");
        }
        catch
        {
            return null;
        }
    }

    private bool HasCriticalSourcePlayback()
    {
        if (vjLayers == null)
        {
            return false;
        }

        for (var i = SourceStartIndex; i < MainEffectStartIndex; i++)
        {
            var layer = vjLayers[i];
            if (layer == null)
            {
                continue;
            }

            if (layer.SourceKind == LayerSourceKind.VideoFile && layer.Player != null && layer.Player.isPrepared)
            {
                if (layer.UsesKlakHapVideo)
                {
                    return true;
                }

                if (layer.Player.isPlaying || layer.Player.frame > 0 || layer.Player.time > 0.0)
                {
                    return true;
                }
            }

            if (layer.SourceKind == LayerSourceKind.YouTube && layer.Player != null && layer.Player.isPrepared)
            {
                if (layer.Player.isPlaying || layer.Player.frame > 0 || layer.Player.time > 0.0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void MaintainCriticalVideoPlayback()
    {
        if (vjLayers == null)
        {
            return;
        }

        for (var i = SourceStartIndex; i < MainEffectStartIndex; i++)
        {
            var layer = vjLayers[i];
            if (layer == null || layer.SourceKind != LayerSourceKind.VideoFile)
            {
                continue;
            }

            if (layer.UsesKlakHapVideo && layer.KlakHapPlayer != null)
            {
                var length = ReflectionDouble(layer.KlakHapPlayer, "streamDuration");
                var current = ReflectionDouble(layer.KlakHapPlayer, "time");
                if (length > 0.01 && current >= Math.Max(0.0, length - 0.10))
                {
                    try
                    {
                        SetReflectionValue(layer.KlakHapPlayer, "loop", true);
                        SetReflectionValue(layer.KlakHapPlayer, "speed", 1f);
                        SetReflectionValue(layer.KlakHapPlayer, "time", 0f);
                        SetReflectionValue(layer.KlakHapPlayer, "enabled", true);
                    }
                    catch
                    {
                    }
                }
                continue;
            }

            if (layer.Player == null)
            {
                continue;
            }

            var player = layer.Player;
            if (!player.isPrepared || player.isPlaying || !player.isLooping || string.IsNullOrEmpty(layer.Path) || !IsMp4File(layer.Path))
            {
                continue;
            }

            var nearLoopPoint = player.length > 0.01 && player.time >= Math.Max(0.0, player.length - 0.18);
            var endedFrames = player.frameCount > 0 && player.frame >= (long)Math.Max(0, player.frameCount - 2);
            if (!nearLoopPoint && !endedFrames)
            {
                continue;
            }

            try
            {
                player.time = 0.0;
                player.Play();
            }
            catch
            {
            }
        }
    }

    private void ExpandMediaFolderPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            var current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current) && Directory.Exists(current))
            {
                expandedMediaFolders.Add(current);
                var parent = Directory.GetParent(current);
                if (parent == null)
                {
                    break;
                }
                current = parent.FullName;
            }
        }
        catch
        {
        }
    }

}
