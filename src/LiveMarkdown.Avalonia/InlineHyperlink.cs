// @author https://github.com/DearVa
// @author https://github.com/AuroraZiling

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace LiveMarkdown.Avalonia;

public class InlineHyperlink : InlineUIContainer
{
    /// <summary>
    /// Gets the inlines collection of the hyperlink.
    /// </summary>
    public InlineCollection Inlines { get; }

    /// <summary>
    /// Gets or sets the image of the hyperlink. If set to null, the hyperlink will display a selectable text block instead.
    /// </summary>
    public Image? Image
    {
        get => button.Content as Image;
        set
        {
            if (value is null) button.Content = selectableTextBlock;
            else button.Content = value;
        }
    }

    public static readonly DirectProperty<InlineHyperlink, Uri?> HRefProperty = AvaloniaProperty.RegisterDirect<InlineHyperlink, Uri?>(
        nameof(HRef), o => o.HRef, (o, v) => o.HRef = v);

    /// <summary>
    /// Gets or sets the hyperlink reference (HRef) of the hyperlink. This must be called from the UI thread.
    /// If set to null, the hyperlink will be disabled and will not respond to clicks.
    /// </summary>
    public Uri? HRef
    {
        get;
        set
        {
            if (!SetAndRaise(HRefProperty, ref field, value)) return;
            UpdatePseudoClasses();
        }
    }

    private readonly SelectableTextBlock selectableTextBlock;
    private readonly Button button;

    public InlineHyperlink()
    {
        selectableTextBlock = new SelectableTextBlock
        {
            Classes = { "InlineHyperlink" }
        };
        Inlines = selectableTextBlock.Inlines ?? throw new NotSupportedException("This should never happen.");

        button = new Button
        {
            Classes = { "InlineHyperlink" },
            Content = selectableTextBlock,
            [!ToolTip.TipProperty] = this[!HRefProperty]
        };
        button.Click += HandleButtonClick;

        Child = button;
        UpdatePseudoClasses();
    }

    private void HandleButtonClick(object? sender, RoutedEventArgs e)
    {
        this.GetLogicalAncestors().OfType<MarkdownRenderer>().FirstOrDefault()?.RaiseInlineHyperlinkClicked(this);
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":disabled", HRef is null);
    }
}
