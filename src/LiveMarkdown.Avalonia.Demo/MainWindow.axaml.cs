using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Windows.Input;

namespace LiveMarkdown.Avalonia.Demo;

public partial class MainWindow : Window
{
    public AvaloniaList<string> MarkdownList { get; } = [];
    public ICommand HyperlinkCommand { get; }
    public string? SelectedMarkdown
    {
        set => _ = RenderMarkdownAsync(value);
    }

    public ObservableStringBuilder MarkdownBuilder { get; } = new();

    private CancellationTokenSource? cancellationTokenSource;

    public MainWindow()
    {
        HyperlinkCommand = new RelayCommand<InlineHyperlinkClickedEventArgs>(OnHyperlinkCommandExecuted);
        DataContext = this;
        InitializeComponent();
        
        _ = new AutoScrollHelper(RawMarkdownTextBlockScrollViewer);
        _ = new AutoScrollHelper(MarkdownRendererScrollViewer);
    }
    private void OnHyperlinkCommandExecuted(InlineHyperlinkClickedEventArgs args)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if (launcher is { }&& args?.HRef is { } url)
        {
            launcher.LaunchUriAsync(url);
        }
    }
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        var markdownFolderPath = Path.Combine(AppContext.BaseDirectory, "samples");
        foreach (var markdownFilePath in Directory.EnumerateFiles(markdownFolderPath, "*.md"))
        {
            MarkdownList.Add(Path.GetFileNameWithoutExtension(markdownFilePath));
        }
    }

    private async Task RenderMarkdownAsync(string? markdownFileName)
    {
        try
        {
            if (cancellationTokenSource is not null) await cancellationTokenSource.CancelAsync();

            if (markdownFileName is null) return;
            var markdownFilePath = Path.Combine(AppContext.BaseDirectory, "samples", markdownFileName + ".md");
            if (!File.Exists(markdownFilePath)) return;

            ClearMarkdown();

            cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var markdownText = await File.ReadAllTextAsync(markdownFilePath, cancellationToken);

            var cursor = 0;
            while (!cancellationToken.IsCancellationRequested && cursor < markdownText.Length)
            {
                var speed = (int)RenderSpeedSlider.Value;
                var newText = markdownText.Substring(cursor, Math.Min(speed, markdownText.Length - cursor));
                cursor += speed;

                RawMarkdownTextBlock.Text += newText;
                MarkdownBuilder.Append(newText);

                await Task.Delay(100, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error rendering markdown: {ex.Message}");
        }
    }

    private void ClearMarkdown()
    {
        RawMarkdownTextBlock.Text = string.Empty;
        MarkdownBuilder.Clear();
    }

    private void HandleClearButtonClick(object? sender, RoutedEventArgs e) => ClearMarkdown();
}

public class AutoScrollHelper
{
    private bool isAtEnd = true;

    public AutoScrollHelper(ScrollViewer scrollViewer)
    {
        scrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
    }

    private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;

        if (e.Property != ScrollViewer.OffsetProperty &&
            e.Property != ScrollViewer.ViewportProperty &&
            e.Property != ScrollViewer.ExtentProperty) return;

        if (e.Property == ScrollViewer.OffsetProperty)
        {
            isAtEnd = ((Vector)e.NewValue!).Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height;
        }

        if (isAtEnd)
        {
            scrollViewer.Offset = new Vector(scrollViewer.Offset.X, double.PositiveInfinity);
        }
    }
}