namespace AIDevelopmentEasy.Core.Analysis;

/// <summary>
/// Helpers for computing 1-based line ranges (start line, end line) from source content.
/// Used by codebase analyzers to record where each type/class lives in a file for code modification.
/// </summary>
public static class LineRangeHelper
{
    /// <summary>Returns 1-based line number for the given character index in content.</summary>
    public static int LineFromIndex(string content, int index)
    {
        if (string.IsNullOrEmpty(content) || index <= 0) return 1;
        int line = 1;
        for (int i = 0; i < index && i < content.Length; i++)
            if (content[i] == '\n') line++;
        return line;
    }

    /// <summary>Finds the index of the closing brace matching the opening brace at openBraceIndex. Skips strings and comments. Returns -1 if not found.</summary>
    public static int FindMatchingBrace(string content, int openBraceIndex)
    {
        int depth = 1;
        for (int i = openBraceIndex + 1; i < content.Length; i++)
        {
            char c = content[i];
            if (c == '"' || c == '\'')
            {
                char q = c;
                i++;
                while (i < content.Length && (content[i] != q || (i > 0 && content[i - 1] == '\\'))) i++;
                continue;
            }
            if (c == '/' && i + 1 < content.Length && content[i + 1] == '/')
            {
                while (i < content.Length && content[i] != '\n') i++;
                continue;
            }
            if (c == '/' && i + 1 < content.Length && content[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < content.Length && (content[i] != '*' || content[i + 1] != '/')) i++;
                i++;
                continue;
            }
            if (c == '{') depth++;
            else if (c == '}') { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    /// <summary>Returns (startLine, endLine) for a type that starts at declarationIndex and whose body starts with '{'. Uses matching brace for end. End line = start line if no brace found.</summary>
    public static (int startLine, int endLine) GetBraceTypeLineRange(string content, int declarationIndex)
    {
        int startLine = LineFromIndex(content, declarationIndex);
        int openBrace = content.IndexOf('{', declarationIndex);
        if (openBrace < 0) return (startLine, startLine);
        int closeBrace = FindMatchingBrace(content, openBrace);
        if (closeBrace < 0) return (startLine, startLine);
        int endLine = LineFromIndex(content, closeBrace);
        return (startLine, endLine);
    }
}
