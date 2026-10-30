using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Ferry;

/// <summary>A picture of a file: a real preview for images and videos, its icon otherwise.</summary>
internal sealed record Thumbnail(BitmapImage Image, bool IsPreview);

/// <summary>File Explorer's thumbnails, remembered for as long as Ferry runs.</summary>
internal sealed class Thumbnails
{
    private const uint Size = 256;
    private readonly Dictionary<string, Thumbnail> cache = new(StringComparer.OrdinalIgnoreCase);

    public Thumbnail? Cached(string path) => cache.GetValueOrDefault(path);

    public async Task<Thumbnail?> LoadAsync(string path)
    {
        if (cache.TryGetValue(path, out var cached))
        {
            return cached;
        }
        try
        {
            IStorageItemProperties item = Directory.Exists(path)
                ? await StorageFolder.GetFolderFromPathAsync(path)
                : await StorageFile.GetFileFromPathAsync(path);
            using var picture = await item.GetThumbnailAsync(ThumbnailMode.SingleItem, Size, ThumbnailOptions.ResizeThumbnail);
            if (picture is null)
            {
                return null;
            }
            var image = new BitmapImage();
            await image.SetSourceAsync(picture);
            return cache[path] = new Thumbnail(image, picture.Type == ThumbnailType.Image);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
