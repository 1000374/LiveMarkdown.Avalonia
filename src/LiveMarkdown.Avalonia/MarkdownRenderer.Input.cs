#pragma warning disable CS0618 // MathUtilities is Obsolete

using System.Collections;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Utilities;

namespace LiveMarkdown.Avalonia;

public partial class MarkdownRenderer
{
    public string SelectedText
    {
        get
        {
            if (selectionBlocks.Count <= 0) return string.Empty;

            var copyStringBuilder = new StringBuilder();
            Rect? previousBounds = null;
            foreach (var block in selectionBlocks)
            {
                var bounds = TranslateBoundsToGlobal(block);
                if (previousBounds is not null && bounds.Y >= previousBounds.Value.Bottom) copyStringBuilder.AppendLine();
                copyStringBuilder.Append(block.SelectedText);
                previousBounds = bounds;
            }

            return copyStringBuilder.ToString();
        }
    }

    private static SelectableTextBlock? GetSelectableTextBlock(PointerEventArgs e)
    {
        var element = e.Source as StyledElement;
        while (element != null)
        {
            switch (element)
            {
                case SelectableTextBlock stb:
                    return stb;
                case MarkdownRenderer:
                    return null;
                default:
                    element = element.Parent;
                    break;
            }
        }
        return null;
    }

    private static IEnumerable<SelectableTextBlock> DfsWhile(SelectableTextBlock current, Predicate<SelectableTextBlock> condition, bool reversed)
    {
        if (reversed)
        {
            var node = GetPreviousElement(current);
            while (node != null)
            {
                if (node is SelectableTextBlock stb)
                {
                    if (!condition(stb)) yield break;
                    yield return stb;
                }

                node = GetPreviousElement(node);
            }
        }
        else
        {
            var node = GetNextElement(current);
            while (node != null)
            {
                if (node is SelectableTextBlock stb)
                {
                    if (!condition(stb)) yield break;
                    yield return stb;
                }

                node = GetNextElement(node);
            }
        }

        static IList GetChildren(StyledElement element)
        {
            return element switch
            {
                Panel panel => panel.Children,
                Decorator { Child: { } child } => new[] { child },
                ContentControl { Content: StyledElement child } => new[] { child },
                Span span => span.Inlines,
                _ => Array.Empty<StyledElement>()
            };
        }

        StyledElement? GetNextElement(StyledElement element)
        {
            var children = GetChildren(element);
            if (children.Count > 0)
                return Cast(children[0]);
            while (element.Parent != null)
            {
                var siblings = GetChildren(element.Parent);
                var idx = siblings.IndexOf(element);
                if (idx < siblings.Count - 1)
                    return Cast(siblings[idx + 1]);
                element = element.Parent;
            }
            return null;
        }

        StyledElement? GetPreviousElement(StyledElement element)
        {
            if (element.Parent == null) return null;
            var siblings = GetChildren(element.Parent);
            var idx = siblings.IndexOf(element);
            if (idx <= 0) return element.Parent;

            var node = Cast(siblings[idx - 1]);
            var children = GetChildren(node);
            while (children.Count > 0)
            {
                node = Cast(children[^1]);
                children = GetChildren(node);
            }
            return node;
        }

        static StyledElement Cast(object? obj) =>
            obj as StyledElement ?? throw new InvalidCastException($"Expected StyledElement, got {obj?.GetType().Name ?? "null"}");
    }

    private SelectableTextBlock? selectionStartBlock;
    private Rect startBlockGlobalBounds;
    private int startBlockSelectionStart;
    private readonly List<SelectableTextBlock> selectionBlocks = [];

    /// <summary>
    /// Translates the bounds of a visual element to global(this) coordinates.
    /// </summary>
    /// <param name="visual"></param>
    /// <returns></returns>
    private Rect TranslateBoundsToGlobal(Visual visual)
    {
        var bounds = visual.Bounds;
        var topLeft = visual.TranslatePoint(new Point(bounds.X, bounds.Y), this) ?? new Point(0, 0);
        return new Rect(topLeft.X, topLeft.Y, bounds.Width, bounds.Height); // We can assume that the bounds are not rotated or scaled
    }

    private SelectableTextBlock? FindSelectableTextBlockAtPoint(PointerEventArgs e, Point point)
    {
        var result = GetSelectableTextBlock(e);
        if (result is not null) return result;

        SelectableTextBlock? previous = null;
        Rect previousBounds = default;
        foreach (var current in documentNode.Control.GetLogicalDescendants().OfType<SelectableTextBlock>())
        {
            var bounds = TranslateBoundsToGlobal(current);

            // 1. check if `point` is inside the bounds of the current SelectableTextBlock
            if (bounds.Contains(point)) return current;

            // 2. check if `point` is between the bounds of the previous and current SelectableTextBlock
            if (previous is not null &&
                previousBounds.X <= point.X && previousBounds.Y <= point.Y &&
                bounds.Right >= point.X && bounds.Bottom >= point.Y)
            {
                return previous;
            }

            previous = current;
        }

        return null;
    }

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();

        var clickInfo = e.GetCurrentPoint(this);

        if (clickInfo.Properties.IsLeftButtonPressed)
        {
            foreach (var selectionBlock in selectionBlocks) selectionBlock.ClearSelection();
            selectionBlocks.Clear();
        }

        selectionStartBlock = FindSelectableTextBlockAtPoint(e, clickInfo.Position);
        if (selectionStartBlock is null)
        {
            // if no SelectableTextBlock was found, we do not handle the event
            return;
        }

        startBlockGlobalBounds = TranslateBoundsToGlobal(selectionStartBlock);
        selectionBlocks.Add(selectionStartBlock);

        var text = selectionStartBlock.Inlines is { Count: > 0 } inline ? inline.Text : selectionStartBlock.Text;
        if (text != null && clickInfo.Properties.IsLeftButtonPressed)
        {
            var padding = selectionStartBlock.Padding;
            var point = e.GetPosition(selectionStartBlock) - new Point(padding.Left, padding.Top);
            var clickToSelect = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            var wordSelectionStart = MathUtilities.Clamp(startBlockSelectionStart, 0, text.Length);

            var hit = selectionStartBlock.TextLayout.HitTestPoint(point);
            var textPosition = hit.TextPosition;

            switch (e.ClickCount)
            {
                case 1:
                {
                    if (clickToSelect)
                    {
                        var previousWord = StringUtils.PreviousWord(text, textPosition);

                        if (textPosition > wordSelectionStart)
                        {
                            SetCurrentValue(SelectableTextBlock.SelectionEndProperty, StringUtils.NextWord(text, textPosition));
                        }

                        if (textPosition < wordSelectionStart || previousWord == wordSelectionStart)
                        {
                            SetCurrentValue(SelectableTextBlock.SelectionStartProperty, previousWord);
                        }
                    }
                    else
                    {
                        selectionStartBlock.SetCurrentValue(SelectableTextBlock.SelectionStartProperty, textPosition);
                        selectionStartBlock.SetCurrentValue(SelectableTextBlock.SelectionEndProperty, textPosition);
                        startBlockSelectionStart = textPosition;
                    }

                    break;
                }
                case 2:
                {
                    if (!StringUtils.IsStartOfWord(text, textPosition))
                    {
                        selectionStartBlock.SetCurrentValue(SelectableTextBlock.SelectionStartProperty, StringUtils.PreviousWord(text, textPosition));
                    }

                    startBlockSelectionStart = wordSelectionStart;

                    if (!StringUtils.IsEndOfWord(text, textPosition))
                    {
                        selectionStartBlock.SetCurrentValue(SelectableTextBlock.SelectionEndProperty, StringUtils.NextWord(text, textPosition));
                    }

                    break;
                }
                case 3:
                {
                    // select all
                    selectionStartBlock.SetCurrentValue(SelectableTextBlock.SelectionStartProperty, 0);
                    selectionStartBlock.SetCurrentValue(SelectableTextBlock.SelectionEndProperty, text.Length);
                    startBlockSelectionStart = wordSelectionStart;
                    break;
                }
            }
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        // selection should not change during pointer move if the user right clicks
        var clickInfo = e.GetCurrentPoint(this);
        if (selectionStartBlock is null || !Equals(e.Pointer.Captured, this) || !clickInfo.Properties.IsLeftButtonPressed) return;

        var selectionEndBlock = FindSelectableTextBlockAtPoint(e, clickInfo.Position);
        if (selectionEndBlock is null) return;

        var text = (selectionEndBlock.Inlines is { Count: > 0 } inline ? inline.Text : selectionEndBlock.Text) ?? "";
        var padding = selectionEndBlock.Padding;

        var point = e.GetPosition(selectionEndBlock) - new Point(padding.Left, padding.Top);

        point = new Point(
            MathUtilities.Clamp(point.X, 0, Math.Max(selectionEndBlock.TextLayout.WidthIncludingTrailingWhitespace, 0)),
            MathUtilities.Clamp(point.Y, 0, Math.Max(selectionEndBlock.TextLayout.Height, 0)));

        var hit = selectionEndBlock.TextLayout.HitTestPoint(point);
        var textPosition = hit.TextPosition;

        if (Equals(selectionEndBlock, selectionStartBlock))
        {
            // We are selecting inside the same `SelectableTextBlock`
            var selectionStart = Math.Min(startBlockSelectionStart, textPosition);
            var selectionEnd = Math.Max(startBlockSelectionStart, textPosition);
            selectionStartBlock.SetCurrentValue(SelectableTextBlock.SelectionStartProperty, selectionStart);
            selectionStartBlock.SetCurrentValue(SelectableTextBlock.SelectionEndProperty, selectionEnd);

            // remove all blocks after the current index
            for (var i = selectionBlocks.Count - 1; i > 0; i--)
            {
                selectionBlocks[i].ClearSelection();
                selectionBlocks.RemoveAt(i);
            }
        }
        else
        {
            // Not in the same `SelectableTextBlock`, we need to get the range that we want to select

            // 1. determine the direction of selection
            //
            //     reversed      |     reversed      |     reversed
            // ------------------+-------------------+-------------------
            //     reversed      |   pointerDownSTB  |
            // ------------------+-------------------+-------------------
            //                   |                   |
            var reversed =
                clickInfo.Position.Y < startBlockGlobalBounds.Y ||
                clickInfo.Position.X < startBlockGlobalBounds.X &&
                clickInfo.Position.Y <= startBlockGlobalBounds.Bottom;

            int selectionStart, selectionEnd;
            if (reversed)
            {
                selectionStart = 0;
                selectionEnd = startBlockSelectionStart;
            }
            else
            {
                selectionStart = startBlockSelectionStart;
                var startText = selectionStartBlock.Inlines is { Count: > 0 } startInline ? startInline.Text : selectionStartBlock.Text;
                selectionEnd = startText?.Length ?? 0;
            }
            selectionStartBlock.SetCurrentValue(SelectableTextBlock.SelectionStartProperty, selectionStart);
            selectionStartBlock.SetCurrentValue(SelectableTextBlock.SelectionEndProperty, selectionEnd);

            // 2. set conditions for the DFS traversal
            bool Condition(SelectableTextBlock current)
            {
                var currentBounds = TranslateBoundsToGlobal(current);
                if (currentBounds.Bottom <= clickInfo.Position.Y) return true;
                return currentBounds.Y <= clickInfo.Position.Y && currentBounds.Left <= clickInfo.Position.X;
            }

            bool ReversedCondition(SelectableTextBlock current)
            {
                var currentBounds = TranslateBoundsToGlobal(current);
                if (currentBounds.Y >= clickInfo.Position.Y) return true;
                return currentBounds.Bottom >= clickInfo.Position.Y && currentBounds.Right >= clickInfo.Position.X;
            }

            Predicate<SelectableTextBlock> condition = reversed ? ReversedCondition : Condition;

            // 3. Enumerate the blocks from the selection start block to the selection end block
            var index = 0;
            foreach (var block in DfsWhile(selectionStartBlock, condition, reversed))
            {
                index++; // starting from 1, because we already added the selection start block

                // Updates the `selectionBlocks`
                if (selectionBlocks.Count > index && !ReferenceEquals(selectionBlocks[index], block))
                {
                    // `selectionBlocks` is not empty and the current block is different from the one in the list
                    // we need to remove the old blocks after index
                    for (var i = selectionBlocks.Count - 1; i >= index; i--)
                    {
                        selectionBlocks[i].ClearSelection();
                        selectionBlocks.RemoveAt(i);
                    }
                }

                // After removing the old blocks, we can add the current block if count is less than or equal to index
                // or the current block is not already in the list
                if (selectionBlocks.Count <= index)
                {
                    selectionBlocks.Add(block);
                }

                if (ReferenceEquals(block, selectionEndBlock))
                {
                    if (reversed)
                    {
                        block.SetCurrentValue(SelectableTextBlock.SelectionStartProperty, textPosition);
                        block.SetCurrentValue(SelectableTextBlock.SelectionEndProperty, text.Length);
                    }
                    else
                    {
                        block.SetCurrentValue(SelectableTextBlock.SelectionStartProperty, 0);
                        block.SetCurrentValue(SelectableTextBlock.SelectionEndProperty, textPosition);
                    }
                }
                else
                {
                    // If we are not at the end block, select all text in the block
                    SelectAll(block);
                }
            }

            // remove all blocks after the current index
            for (var i = selectionBlocks.Count - 1; i > index; i--)
            {
                selectionBlocks[i].ClearSelection();
                selectionBlocks.RemoveAt(i);
            }
        }

        e.Handled = true;
    }

    private void HandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!Equals(e.Pointer.Captured, this)) return;

        e.Pointer.Capture(null);
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        var keymap = Application.Current!.PlatformSettings!.HotkeyConfiguration;

        bool Match(List<KeyGesture> gestures) => gestures.Any(g => g.Matches(e));

        if (Match(keymap.Copy))
        {
            Copy();
            e.Handled = true;
        }
        else if (Match(keymap.SelectAll))
        {
            foreach (var block in this.GetLogicalDescendants().OfType<SelectableTextBlock>()) SelectAll(block);
            e.Handled = true;
        }
    }

    public async void Copy()
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not { Clipboard: { } clipboard }) return;

            var text = SelectedText;
            if (string.IsNullOrEmpty(text)) return;

            await clipboard.SetTextAsync(text);
        }
        catch
        {
            // ignore any exceptions during copy operation
        }
    }

    /// <summary>
    /// SelectableTextBlock.SelectAll is wrong.
    /// </summary>
    /// <param name="block"></param>
    private static void SelectAll(SelectableTextBlock block)
    {
        block.SetCurrentValue(SelectableTextBlock.SelectionStartProperty, 0);
        var blockText = block.Inlines is { Count: > 0 } startInline ? startInline.Text : block.Text;
        block.SetCurrentValue(SelectableTextBlock.SelectionEndProperty, blockText?.Length ?? 0);
    }
}

#pragma warning restore CS0618 // MathUtilities is Obsolete