using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;

namespace Wobble.Graphics.Sprites.Text
{
    /// <summary>
    ///     Parses the lightweight markup accepted by <see cref="SpriteTextPlusFormattable"/>.
    /// </summary>
    internal static class FormattedTextMarkupParser
    {
        public static FormattedTextMarkupResult Parse(string markup)
        {
            markup = markup ?? string.Empty;
            var result = new FormattedTextMarkupResult();
            ParseRange(markup, 0, markup.Length, result);
            return result;
        }

        private static void ParseRange(string source, int start, int end, FormattedTextMarkupResult result)
        {
            var index = start;

            while (index < end)
            {
                if (source[index] == '\\' && index + 1 < end && IsEscapable(source[index + 1]))
                {
                    result.Text.Append(source[index + 1]);
                    index += 2;
                    continue;
                }

                if (StartsWith(source, index, end, "**") &&
                    TryFindUnescaped(source, "**", index + 2, end, out var boldEnd))
                {
                    var outputStart = result.Text.Length;
                    var insertionIndex = result.BoldRanges.Count;
                    ParseRange(source, index + 2, boldEnd, result);
                    var length = result.Text.Length - outputStart;

                    if (length != 0)
                        result.BoldRanges.Insert(insertionIndex, new TextBoldRange(outputStart, length));

                    index = boldEnd + 2;
                    continue;
                }

                if (source[index] == '#' && TryReadColor(source, index, end, out var color))
                {
                    var outputStart = result.Text.Length;
                    var insertionIndex = result.ColorRanges.Count;
                    ParseRange(source, color.ContentStart, color.ContentEnd, result);
                    var length = result.Text.Length - outputStart;

                    if (length != 0)
                        result.ColorRanges.Insert(insertionIndex, new TextColorRange(outputStart, length, color.Color));

                    index = color.EndIndex;
                    continue;
                }

                if (source[index] == '[' && TryReadOpeningStyleTag(source, index, end, out var styleTag) &&
                    TryFindClosingStyleTag(source, styleTag.EndIndex, end, styleTag.Kind, out var closingStart, out var closingEnd))
                {
                    var outputStart = result.Text.Length;
                    var insertionIndex = GetStyleRangeCount(result, styleTag.Kind);
                    ParseRange(source, styleTag.EndIndex, closingStart, result);
                    var length = result.Text.Length - outputStart;

                    if (length != 0)
                        InsertStyleRange(result, styleTag, insertionIndex, outputStart, length);

                    index = closingEnd;
                    continue;
                }

                if (source[index] == '!' && index + 1 < end && source[index + 1] == '[' &&
                    TryReadLink(source, index + 1, end, out var imageLink))
                {
                    AppendLinkTarget(result, imageLink.Target);
                    index = imageLink.EndIndex;
                    continue;
                }

                if (source[index] == '[' && TryReadLink(source, index, end, out var link))
                {
                    var outputStart = result.Text.Length;
                    var target = UnescapeLinkTarget(link.Target);
                    ParseRange(source, link.LabelStart, link.LabelEnd, result);
                    var length = result.Text.Length - outputStart;

                    if (length == 0)
                    {
                        result.Text.Append(target);
                        length = target.Length;
                    }

                    if (length != 0)
                        result.LinkRanges.Add(new TextLinkRange(outputStart, length, target));

                    index = link.EndIndex;
                    continue;
                }

                result.Text.Append(source[index]);
                index++;
            }
        }

        private static void AppendLinkTarget(FormattedTextMarkupResult result, string target)
        {
            target = UnescapeLinkTarget(target);

            if (target.Length == 0)
                return;

            var outputStart = result.Text.Length;
            result.Text.Append(target);
            result.LinkRanges.Add(new TextLinkRange(outputStart, target.Length, target));
        }

        private static int GetStyleRangeCount(FormattedTextMarkupResult result, MarkupStyleKind kind)
        {
            switch (kind)
            {
                case MarkupStyleKind.Size:
                    return result.FontSizeRanges.Count;
                case MarkupStyleKind.Underline:
                    return result.UnderlineRanges.Count;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static void InsertStyleRange(FormattedTextMarkupResult result, OpeningStyleTag tag, int insertionIndex, int startIndex, int length)
        {
            switch (tag.Kind)
            {
                case MarkupStyleKind.Size:
                    result.FontSizeRanges.Insert(insertionIndex, new TextFontSizeRange(startIndex, length, tag.FontSize));
                    break;
                case MarkupStyleKind.Underline:
                    result.UnderlineRanges.Insert(insertionIndex, new TextUnderlineRange(startIndex, length));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static bool TryReadOpeningStyleTag(string source, int start, int end, out OpeningStyleTag tag)
        {
            tag = default;

            if (!TryFindUnescaped(source, "]", start + 1, end, out var bracketEnd))
                return false;

            var content = source.Substring(start + 1, bracketEnd - start - 1).Trim();

            if (content.Equals("u", StringComparison.OrdinalIgnoreCase) ||
                content.Equals("underline", StringComparison.OrdinalIgnoreCase))
            {
                tag = new OpeningStyleTag(MarkupStyleKind.Underline, bracketEnd + 1);
                return true;
            }

            const string sizePrefix = "size=";
            if (content.StartsWith(sizePrefix, StringComparison.OrdinalIgnoreCase) &&
                float.TryParse(content.Substring(sizePrefix.Length).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var fontSize) && fontSize > 0 &&
                !float.IsNaN(fontSize) && !float.IsInfinity(fontSize))
            {
                tag = new OpeningStyleTag(fontSize, bracketEnd + 1);
                return true;
            }

            return false;
        }

        private static bool TryFindClosingStyleTag(string source, int start, int end, MarkupStyleKind kind, out int closingStart, out int closingEnd)
        {
            var depth = 1;

            for (var index = start; index < end; index++)
            {
                if (source[index] != '[' || IsEscaped(source, index))
                    continue;

                if (TryReadOpeningStyleTag(source, index, end, out var opening) && opening.Kind == kind)
                {
                    depth++;
                    index = opening.EndIndex - 1;
                    continue;
                }

                if (!TryReadClosingStyleTag(source, index, end, out var closingKind, out var tagEnd) ||
                    closingKind != kind)
                    continue;

                depth--;

                if (depth == 0)
                {
                    closingStart = index;
                    closingEnd = tagEnd;
                    return true;
                }

                index = tagEnd - 1;
            }

            closingStart = -1;
            closingEnd = -1;
            return false;
        }

        private static bool TryReadClosingStyleTag(string source, int start, int end, out MarkupStyleKind kind, out int tagEnd)
        {
            kind = default;
            tagEnd = -1;

            if (!TryFindUnescaped(source, "]", start + 1, end, out var bracketEnd))
                return false;

            var content = source.Substring(start + 1, bracketEnd - start - 1).Trim();

            if (content.Equals("/size", StringComparison.OrdinalIgnoreCase))
                kind = MarkupStyleKind.Size;
            else if (content.Equals("/u", StringComparison.OrdinalIgnoreCase) ||
                     content.Equals("/underline", StringComparison.OrdinalIgnoreCase))
                kind = MarkupStyleKind.Underline;
            else
                return false;

            tagEnd = bracketEnd + 1;
            return true;
        }

        private static bool TryReadLink(string source, int start, int end, out MarkupLink link)
        {
            link = default;
            var labelEnd = -1;
            var labelDepth = 1;

            for (var index = start + 1; index < end; index++)
            {
                if (IsEscaped(source, index))
                    continue;

                if (source[index] == '[')
                    labelDepth++;
                else if (source[index] == ']')
                {
                    labelDepth--;

                    if (labelDepth == 0)
                    {
                        if (index + 1 >= end || source[index + 1] != '(')
                            return false;

                        labelEnd = index;
                        break;
                    }
                }
            }

            if (labelEnd == -1)
                return false;

            var targetStart = labelEnd + 2;
            var depth = 1;

            for (var index = targetStart; index < end; index++)
            {
                if (IsEscaped(source, index))
                    continue;

                if (source[index] == '(')
                    depth++;
                else if (source[index] == ')')
                {
                    depth--;

                    if (depth == 0)
                    {
                        link = new MarkupLink(start + 1, labelEnd, source.Substring(targetStart, index - targetStart), index + 1);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryReadColor(string source, int start, int end, out MarkupColor color)
        {
            color = default;
            var bracketStart = -1;

            for (var index = start + 1; index < end && index <= start + 9; index++)
            {
                if (source[index] == '[')
                {
                    bracketStart = index;
                    break;
                }

                if (!Uri.IsHexDigit(source[index]))
                    return false;
            }

            if (bracketStart == -1 ||
                !TryParseHexColor(source.Substring(start, bracketStart - start), out var parsedColor) ||
                !TryFindClosingBracket(source, bracketStart + 1, end, out var bracketEnd))
                return false;

            color = new MarkupColor(parsedColor, bracketStart + 1, bracketEnd, bracketEnd + 1);
            return true;
        }

        private static bool TryFindClosingBracket(string source, int start, int end, out int closingIndex)
        {
            var depth = 1;

            for (var index = start; index < end; index++)
            {
                if (IsEscaped(source, index))
                    continue;

                if (source[index] == '[')
                    depth++;
                else if (source[index] == ']')
                {
                    depth--;

                    if (depth == 0)
                    {
                        closingIndex = index;
                        return true;
                    }
                }
            }

            closingIndex = -1;
            return false;
        }

        private static string UnescapeLinkTarget(string target)
        {
            var result = new StringBuilder(target.Length);

            for (var i = 0; i < target.Length; i++)
            {
                if (target[i] == '\\' && i + 1 < target.Length &&
                    (target[i + 1] == '\\' || target[i + 1] == '(' || target[i + 1] == ')'))
                {
                    result.Append(target[++i]);
                    continue;
                }

                result.Append(target[i]);
            }

            return result.ToString();
        }

        private static bool TryParseHexColor(string value, out Color color)
        {
            color = Color.White;

            if (value.Length < 2 || value[0] != '#')
                return false;

            var hex = value.Substring(1);

            if (hex.Length == 3 || hex.Length == 4)
            {
                var expanded = new StringBuilder(hex.Length * 2);

                for (var i = 0; i < hex.Length; i++)
                    expanded.Append(hex[i], 2);

                hex = expanded.ToString();
            }

            if (hex.Length != 6 && hex.Length != 8)
                return false;

            if (!byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red) ||
                !byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green) ||
                !byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
                return false;

            var alpha = byte.MaxValue;

            if (hex.Length == 8 &&
                !byte.TryParse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out alpha))
                return false;

            color = new Color(red, green, blue, alpha);
            return true;
        }

        private static bool TryFindUnescaped(string source, string value, int start, int end, out int foundIndex)
        {
            for (var index = start; index <= end - value.Length; index++)
            {
                if (!IsEscaped(source, index) && StartsWith(source, index, end, value))
                {
                    foundIndex = index;
                    return true;
                }
            }

            foundIndex = -1;
            return false;
        }

        private static bool StartsWith(string source, int index, int end, string value)
        {
            if (index + value.Length > end)
                return false;

            for (var i = 0; i < value.Length; i++)
            {
                if (source[index + i] != value[i])
                    return false;
            }

            return true;
        }

        private static bool IsEscaped(string source, int index)
        {
            var slashCount = 0;

            for (var i = index - 1; i >= 0 && source[i] == '\\'; i--)
                slashCount++;

            return slashCount % 2 != 0;
        }

        private static bool IsEscapable(char value) =>
            value == '\\' || value == '*' || value == '#' || value == '[' || value == ']' ||
            value == '(' || value == ')';

        private enum MarkupStyleKind
        {
            Size,
            Underline
        }

        private readonly struct OpeningStyleTag
        {
            public MarkupStyleKind Kind { get; }
            public float FontSize { get; }
            public int EndIndex { get; }

            public OpeningStyleTag(MarkupStyleKind kind, int endIndex)
            {
                Kind = kind;
                FontSize = 0;
                EndIndex = endIndex;
            }

            public OpeningStyleTag(float fontSize, int endIndex)
            {
                Kind = MarkupStyleKind.Size;
                FontSize = fontSize;
                EndIndex = endIndex;
            }
        }

        private readonly struct MarkupColor
        {
            public Color Color { get; }
            public int ContentStart { get; }
            public int ContentEnd { get; }
            public int EndIndex { get; }

            public MarkupColor(Color color, int contentStart, int contentEnd, int endIndex)
            {
                Color = color;
                ContentStart = contentStart;
                ContentEnd = contentEnd;
                EndIndex = endIndex;
            }
        }

        private readonly struct MarkupLink
        {
            public int LabelStart { get; }
            public int LabelEnd { get; }
            public string Target { get; }
            public int EndIndex { get; }

            public MarkupLink(int labelStart, int labelEnd, string target, int endIndex)
            {
                LabelStart = labelStart;
                LabelEnd = labelEnd;
                Target = target;
                EndIndex = endIndex;
            }
        }
    }

    internal sealed class FormattedTextMarkupResult
    {
        public StringBuilder Text { get; } = new StringBuilder();
        public List<TextBoldRange> BoldRanges { get; } = new List<TextBoldRange>();
        public List<TextColorRange> ColorRanges { get; } = new List<TextColorRange>();
        public List<TextFontSizeRange> FontSizeRanges { get; } = new List<TextFontSizeRange>();
        public List<TextUnderlineRange> UnderlineRanges { get; } = new List<TextUnderlineRange>();
        public List<TextLinkRange> LinkRanges { get; } = new List<TextLinkRange>();
    }
}