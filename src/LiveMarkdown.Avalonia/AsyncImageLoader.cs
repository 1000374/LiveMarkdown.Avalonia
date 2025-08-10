using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg;
using Avalonia.Threading;
using Svg.Model;

namespace LiveMarkdown.Avalonia;

/// <summary>
/// Asynchronously loads images from a given source URL and caches them.
/// Supports both SVG and bitmap images.
/// </summary>
public class AsyncImageLoader
{
    /// <summary>
    /// Attached property for the image source URL.
    /// </summary>
    public static readonly AttachedProperty<string?> SourceProperty =
        AvaloniaProperty.RegisterAttached<AsyncImageLoader, Image, string?>("Source");

    /// <summary>
    /// Sets the source URL for the image.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="value"></param>
    public static void SetSource(Image obj, string? value) => obj.SetValue(SourceProperty, value);

    /// <summary>
    /// Gets the source URL for the image.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static string? GetSource(Image obj) => obj.GetValue(SourceProperty);

    /// <summary>
    /// Attached property for the SVG CSS styles.
    /// This only works before the image is loaded.
    /// </summary>
    public static readonly AttachedProperty<string?> SvgCssProperty =
        AvaloniaProperty.RegisterAttached<AsyncImageLoader, Image, string?>("SvgCss");

    /// <summary>
    /// Sets the CSS styles for the SVG image.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="value"></param>
    public static void SetSvgCss(Image obj, string? value) => obj.SetValue(SvgCssProperty, value);

    /// <summary>
    /// Gets the CSS styles for the SVG image.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static string? GetSvgCss(Image obj) => obj.GetValue(SvgCssProperty);

    public static HttpClient HttpClient { get; set; } = new();

    public static AsyncImageLoaderCache Cache { get; set; } = new RamBasedAsyncImageLoaderCache();

    private readonly static Dictionary<Image, (Task task, CancellationTokenSource cts)> ImageLoadTasks = new();

    static AsyncImageLoader()
    {
        SourceProperty.Changed.AddClassHandler<Image>(HandleSourceChanged);
    }

    private static void HandleSourceChanged(Image sender, AvaloniaPropertyChangedEventArgs args)
    {
        // This method is always called on the UI thread, so we can safely access the UI elements.

        if (ImageLoadTasks.TryGetValue(sender, out var pair))
        {
            pair.cts.Cancel(); // Cancel the previous loading task if it exists
            ImageLoadTasks.Remove(sender);
        }

        var newSource = args.NewValue as string;
        if (string.IsNullOrEmpty(newSource))
        {
            sender.Source = null; // Clear the image source if the new value is null or empty
            return;
        }

        if (Cache.GetImage(newSource!) is { } cachedImage)
        {
            sender.Source = cachedImage; // Use the cached image if available
            return;
        }

        var css = sender.GetValue(SvgCssProperty);
        if (string.IsNullOrEmpty(css))
        {
            var fontSize = sender.GetValue(TextElement.FontSizeProperty);
            var fontFamily = sender.GetValue(TextElement.FontFamilyProperty).ToString();
            if (fontFamily == "$Default") fontFamily = "Arial"; // Fallback to Arial if the default is not set
            var color = sender.GetValue(TextElement.ForegroundProperty) switch
            {
                SolidColorBrush solidColorBrush => solidColorBrush.Color,
                _ => Colors.White // Default color if not set
            };
            css = $":nth-child(0) {{ font-size: {fontSize}px; font-family: {fontFamily}; color: #{color.R:X2}{color.G:X2}{color.B:X2}; }}";
        }

        var newPair = CreateLoadPair(sender, newSource!, css);
        ImageLoadTasks.Add(sender, newPair);
    }

    private static (Task task, CancellationTokenSource cts) CreateLoadPair(Image image, string source, string? css)
    {
        var cts = new CancellationTokenSource();
        var task = Task.Run(
                async () =>
                {
                    try
                    {
                        using var response = await HttpClient.GetAsync(source, cts.Token);
                        response.EnsureSuccessStatusCode();
                        using var stream = await response.Content.ReadAsStreamAsync();

                        var buffer = new byte[16];
                        // check if the stream is svg
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                        if (bytesRead == 0)
                        {
                            return null;
                        }

                        stream.Seek(0, SeekOrigin.Begin); // Reset the stream position

                        var isBinary = buffer.Take(bytesRead).Any(b => b == 0); // check for null bytes, which indicate binary data
                        if (isBinary)
                        {
                            // If the stream is binary, treat it as a Bitmap
                            return (object)WriteableBitmap.Decode(stream);
                        }

                        return SvgSource.Load(stream, new SvgParameters { Css = css });
                    }
                    catch (OperationCanceledException)
                    {
                        // Task was cancelled, do nothing
                        throw;
                    }
                    catch
                    {
                        // Handle other exceptions as needed
                        return null; // Clear the image source on error
                    }
                },
                cts.Token)
            .ContinueWith(
                t =>
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        if (ImageLoadTasks.TryGetValue(image, out var pair) && pair.cts == cts)
                        {
                            ImageLoadTasks.Remove(image); // Remove the task from the dictionary
                        }

                        if (t.Exception is not null) return; // Operation was cancelled or failed, do nothing

                        IImage? result = t.Result switch
                        {
                            Bitmap bitmap => bitmap,
                            SvgSource svgSource => new SvgImage { Source = svgSource },
                            _ => null // Clear the image source if the result is not a valid image
                        };

                        if (result is not null) Cache.SetImage(source, result);

                        image.Source = result;
                    });
                },
                cts.Token);

        return (task, cts);
    }
}

public abstract class AsyncImageLoaderCache
{
    public abstract IImage? GetImage(string source);

    public abstract void SetImage(string source, IImage image);
}

public class RamBasedAsyncImageLoaderCache : AsyncImageLoaderCache
{
    private readonly Dictionary<string, WeakReference<IImage>> _cache = new();

    private int _checkThreshold = 16;

    public override IImage? GetImage(string source)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(source, out var weakRef) && weakRef.TryGetTarget(out var image))
            {
                return image;
            }

            return null;
        }
    }

    public override void SetImage(string source, IImage image)
    {
        lock (_cache)
        {
            _cache[source] = new WeakReference<IImage>(image);

            if (_cache.Count <= _checkThreshold) return;

            // Clean up weak references that are no longer alive
            var keysToRemove = _cache.Where(kvp => !kvp.Value.TryGetTarget(out _)).Select(kvp => kvp.Key).ToList();
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }

            if (_cache.Count > _checkThreshold) _checkThreshold *= 2;
            else if (_cache.Count < _checkThreshold / 4) _checkThreshold /= 2;
        }
    }
}