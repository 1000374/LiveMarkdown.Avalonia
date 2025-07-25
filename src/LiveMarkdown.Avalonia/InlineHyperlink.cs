// @author https://github.com/DearVa
// @author https://github.com/AuroraZiling

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LiveMarkdown.Avalonia;

public class InlineHyperlink : InlineUIContainer
{
    public object? Content
    {
        get => button.Content;
        set => button.Content = value;
    }

    public static readonly DirectProperty<InlineHyperlink, Uri?> HRefProperty = AvaloniaProperty.RegisterDirect<InlineHyperlink, Uri?>(
        nameof(HRef), o => o.HRef, (o, v) => o.HRef = v);

    public Uri? HRef
    {
        get;
        set
        {
            if (!SetAndRaise(HRefProperty, ref field, value)) return;
            UpdatePseudoClasses();
        }
    }

    private readonly Button button;

    public InlineHyperlink()
    {
        button = new Button
        {
            Classes = { "InlineHyperlink" },
            Cursor = new Cursor(StandardCursorType.Hand),
            [!ToolTip.TipProperty] = this[!HRefProperty]
        };
        button.Click += HandleButtonClick;

        Child = button;
        UpdatePseudoClasses();
    }

    private void HandleButtonClick(object? sender, RoutedEventArgs e)
    {
        if (HRef is null) return;

    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":disabled", HRef is null);
    }
}