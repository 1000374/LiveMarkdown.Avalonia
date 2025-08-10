using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using Markdig.Syntax;

namespace LiveMarkdown.Avalonia;

/// <summary>
/// Represents a Markdown text block that can be rendered and interacted with.
/// This class extends <see cref="SelectableTextBlock"/> to fix its selection bugs.
/// </summary>
public class MarkdownTextBlock : SelectableTextBlock
{
    public SourceSpan SourceSpan { get; internal set; }

    public string ActualText
    {
        get
        {
            if (Inlines is not { Count: > 0 } inlines) return Text ?? string.Empty;

            var stringBuilder = new StringBuilder();
            foreach (var inline in inlines) AppendText(inline);
            return stringBuilder.ToString();

            void AppendText(Inline inline)
            {
                switch (inline)
                {
                    case Run run:
                    {
                        stringBuilder.Append(run.Text);
                        break;
                    }
                    case Span span:
                    {
                        foreach (var childInline in span.Inlines) AppendText(childInline);
                        break;
                    }
                    case LineBreak:
                    {
                        stringBuilder.Append(Environment.NewLine);
                        break;
                    }
                    case InlineUIContainer { Child: { } logicalChild }:
                    {
                        AppendLogicalText(logicalChild);
                        break;
                    }
                }
            }

            void AppendLogicalText(ILogical logical)
            {
                if (logical is MarkdownTextBlock markdownTextBlock)
                {
                    stringBuilder.Append(markdownTextBlock.ActualText);
                    return; // markdownTextBlock.ActualText will handle its own inlines
                }

                foreach (var child in logical.LogicalChildren) AppendLogicalText(child);
            }
        }
    }

    public new string SelectedText
    {
        get
        {
            var selectionStart = SelectionStart;
            var selectionEnd = SelectionEnd;
            var actualText = ActualText;
            var start = Math.Max(Math.Min(selectionStart, selectionEnd), 0);
            var end = Math.Min(Math.Max(selectionStart, selectionEnd), actualText.Length);
            return actualText[start..end];
        }
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        if (ContextFlyout is not { IsOpen: true } &&
            ContextMenu is not { IsOpen: true })
        {
            ClearSelection();
        }
    }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        var selectionStart = SelectionStart;
        var selectionEnd = SelectionEnd;
        var selectionBrush = SelectionBrush;

        if (selectionStart != selectionEnd && selectionBrush != null)
        {
            var start = Math.Min(selectionStart, selectionEnd);
            var length = Math.Max(selectionStart, selectionEnd) - start;

            using (context.PushTransform(Matrix.CreateTranslation(origin)))
            {
                foreach (var rect in TextLayoutHitTestTextRange(start, length))
                {
                    context.FillRectangle(selectionBrush, PixelRect.FromRect(rect, 1).ToRect(1));
                }
            }
        }

        base.RenderTextLayout(context, origin);
    }

    private static readonly FieldInfo TextLinesFieldInfo = typeof(TextLayout)
            .GetField("_textLines", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find the _textLines field in SelectableTextBlock.");

    public int TextLayoutHitTestPoint(in Point p)
    {
        var textPosition = TextLayout.HitTestPoint(p).TextPosition;
        if (Inlines is not { Count: > 0 } inlines) return textPosition;

        var accumulatedLength = 0;
        foreach (var inline in inlines)
        {
            FixTextPosition(inline);
            if (accumulatedLength >= textPosition) break;
        }

        return textPosition;

        void FixTextPosition(Inline inline)
        {
            switch (inline)
            {
                case Run run:
                {
                    accumulatedLength += run.Text?.Length ?? 0;
                    break;
                }
                case Span span:
                {
                    foreach (var childInline in span.Inlines)
                    {
                        FixTextPosition(childInline);
                        if (accumulatedLength >= textPosition) break;
                    }
                    break;
                }
                case LineBreak:
                {
                    accumulatedLength++;
                    break;
                }
                case InlineUIContainer { Child: { } logicalChild }:
                {
                    FixLogicalText(logicalChild);
                    break;
                }
            }
        }

        void FixLogicalText(ILogical logical)
        {
            // example:
            // This is a [link] inside the MarkdownTextBlock.
            // When originally hit testing the point on the `the` character
            // original text position would treat the link as a single character
            // so it will be 20
            // but actual text position should be 24

            if (accumulatedLength >= textPosition) return;

            if (logical is MarkdownTextBlock markdownTextBlock)
            {
                var actualLength = markdownTextBlock.ActualText.Length;
                accumulatedLength += actualLength - 1;
                textPosition += actualLength - 1;
                return; // markdownTextBlock.ActualText will handle its own inlines
            }

            foreach (var child in logical.LogicalChildren)
            {
                FixLogicalText(child);
                if (accumulatedLength >= textPosition) break;
            }
        }
    }

    public IEnumerable<Rect> TextLayoutHitTestTextRange(int start, int length)
    {
        if (start + length <= 0) yield break;

        var currentY = 0d;
        var textLines = (TextLine[])TextLinesFieldInfo.GetValue(TextLayout)!;
        foreach (var textLine in textLines)
        {
            // Current line isn't covered.
            if (textLine.FirstTextSourceIndex + textLine.Length <= start)
            {
                currentY += textLine.Height;
                continue;
            }

            var textBounds = textLine.GetTextBounds(start, length);
            if (textBounds.Count > 0)
            {
                Rect? last = null;
                foreach (var bounds in textBounds)
                {
                    if (last.HasValue &&
#pragma warning disable CS0618 // MathUtilities is obsolete, but still works
                        MathUtilities.AreClose(last.Value.Right, bounds.Rectangle.Left) &&
                        MathUtilities.AreClose(last.Value.Top, currentY))
#pragma warning restore CS0618 // MathUtilities is obsolete, but still works
                    {
                        last = last.Value.WithWidth(last.Value.Width + bounds.Rectangle.Width);
                    }
                    else
                    {
                        if (last.HasValue) yield return last.Value;
                        last = bounds.Rectangle.WithY(currentY);
                    }

                    foreach (var runBounds in bounds.TextRunBounds)
                    {
                        start += runBounds.Length;
                        length -= runBounds.Length;
                    }
                }

                if (last.HasValue) yield return last.Value;
            }

            if (textLine.FirstTextSourceIndex + textLine.Length >= start + length) break;
            currentY += textLine.Height;
        }
    }

    // public override IReadOnlyList<TextBounds> TextLineGetTextBounds(TextLine textLine, int firstTextSourceIndex, int textLength)
    // {
    //     if (_indexedTextRuns is null || _indexedTextRuns.Count == 0)
    //     {
    //         return Array.Empty<TextBounds>();
    //     }
    //
    //     var result = new List<TextBounds>();
    //
    //     var currentPosition = textLine.FirstTextSourceIndex;
    //     var remainingLength = textLength;
    //
    //     TextBounds? lastBounds = null;
    //
    //     static FlowDirection GetDirection(TextRun textRun, FlowDirection currentDirection)
    //     {
    //         if (textRun is ShapedTextRun shapedTextRun)
    //         {
    //             return shapedTextRun.ShapedBuffer.IsLeftToRight ?
    //                 FlowDirection.LeftToRight :
    //                 FlowDirection.RightToLeft;
    //         }
    //
    //         return currentDirection;
    //     }
    //
    //     IndexedTextRun FindIndexedRun()
    //     {
    //         var i = 0;
    //
    //         IndexedTextRun currentIndexedRun = _indexedTextRuns[i];
    //
    //         while (currentIndexedRun.TextSourceCharacterIndex != currentPosition)
    //         {
    //             if (i + 1 == _indexedTextRuns.Count)
    //             {
    //                 break;
    //             }
    //
    //             i++;
    //
    //             currentIndexedRun = _indexedTextRuns[i];
    //         }
    //
    //         return currentIndexedRun;
    //     }
    //
    //     double GetPreceedingDistance(int firstIndex)
    //     {
    //         var distance = 0.0;
    //
    //         for (var i = 0; i < firstIndex; i++)
    //         {
    //             var currentRun = _textRuns[i];
    //
    //             if (currentRun is DrawableTextRun drawableTextRun)
    //             {
    //                 distance += drawableTextRun.Size.Width;
    //             }
    //         }
    //
    //         return distance;
    //     }
    //
    //     bool TryMergeWithLastBounds(TextBounds currentBounds)
    //     {
    //         if (currentBounds.FlowDirection != lastBounds.FlowDirection)
    //         {
    //             return false;
    //         }
    //
    //         if (currentBounds.Rectangle.Left == lastBounds.Rectangle.Right)
    //         {
    //             foreach (var runBounds in currentBounds.TextRunBounds)
    //             {
    //                 lastBounds.TextRunBounds.Add(runBounds);
    //             }
    //
    //             lastBounds.Rectangle = lastBounds.Rectangle.Union(currentBounds.Rectangle);
    //
    //             return true;
    //         }
    //
    //         if (currentBounds.Rectangle.Right == lastBounds.Rectangle.Left)
    //         {
    //             for (int i = 0; i < currentBounds.TextRunBounds.Count; i++)
    //             {
    //                 lastBounds.TextRunBounds.Insert(i, currentBounds.TextRunBounds[i]);
    //             }
    //
    //             lastBounds.Rectangle = lastBounds.Rectangle.Union(currentBounds.Rectangle);
    //
    //             return true;
    //         }
    //
    //         return false;
    //     }
    //
    //     while (remainingLength > 0 && currentPosition < textLine.FirstTextSourceIndex + textLine.Length)
    //     {
    //         var currentIndexedRun = FindIndexedRun();
    //
    //         if (currentIndexedRun == null)
    //         {
    //             break;
    //         }
    //
    //         var directionalWidth = 0.0;
    //         var firstRunIndex = currentIndexedRun.RunIndex;
    //         var lastRunIndex = firstRunIndex;
    //         var currentTextRun = currentIndexedRun.TextRun;
    //
    //         if (currentTextRun == null)
    //         {
    //             break;
    //         }
    //
    //         var currentDirection = GetDirection(currentTextRun, _resolvedFlowDirection);
    //
    //         if (currentIndexedRun.TextSourceCharacterIndex + currentTextRun.Length <= firstTextSourceIndex)
    //         {
    //             currentPosition += currentTextRun.Length;
    //
    //             continue;
    //         }
    //
    //         var currentX = textLine.Start + GetPreceedingDistance(currentIndexedRun.RunIndex);
    //
    //         if (currentTextRun is DrawableTextRun currentDrawable)
    //         {
    //             directionalWidth = currentDrawable.Size.Width;
    //         }
    //
    //         int coveredLength;
    //         TextBounds? currentBounds;
    //
    //         switch (currentDirection)
    //         {
    //             case FlowDirection.RightToLeft:
    //             {
    //                 currentBounds = GetTextRunBoundsRightToLeft(
    //                     firstRunIndex,
    //                     lastRunIndex,
    //                     currentX + directionalWidth,
    //                     firstTextSourceIndex,
    //                     currentPosition,
    //                     remainingLength,
    //                     out coveredLength,
    //                     out currentPosition);
    //
    //                 break;
    //             }
    //             default:
    //             {
    //                 currentBounds = GetTextBoundsLeftToRight(
    //                     firstRunIndex,
    //                     lastRunIndex,
    //                     currentX,
    //                     firstTextSourceIndex,
    //                     currentPosition,
    //                     remainingLength,
    //                     out coveredLength,
    //                     out currentPosition);
    //
    //                 break;
    //             }
    //         }
    //
    //         if (coveredLength == 0)
    //         {
    //             //This should never happen
    //             break;
    //         }
    //
    //         if (lastBounds != null && TryMergeWithLastBounds(currentBounds, lastBounds))
    //         {
    //             currentBounds = lastBounds;
    //
    //             result[result.Count - 1] = currentBounds;
    //         }
    //         else
    //         {
    //             result.Add(currentBounds);
    //         }
    //
    //         lastBounds = currentBounds;
    //
    //         remainingLength -= coveredLength;
    //     }
    //
    //     result.Sort(TextBoundsComparer);
    //
    //     return result;
    // }

    public new void SelectAll()
    {
        SetCurrentValue(SelectionStartProperty, 0);
        SetCurrentValue(SelectionEndProperty, ActualText.Length);
    }
}