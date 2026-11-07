namespace Autonogrammer.Cowork.Artifacts.Rendering;


/// <summary>RFC 4180 reader with hard caps. Cells stay text; formula-like prefixes are marked.</summary>
public static class CsvReader
{
    /// <summary>One text cell from a capped CSV preview. Formula-like cells are marked and never evaluated.</summary>
    /// <param name="Text">The cell text.</param>
    /// <param name="FormulaLike">Whether the cell starts with a spreadsheet formula prefix.</param>
    internal readonly record struct CsvCell(string Text, bool FormulaLike);

    /// <summary>A capped CSV preview or a typed refusal. Nothing is truncated except the row window, which is counted.</summary>
    internal sealed class CsvReadResult
    {
        private CsvReadResult(bool isSuccess, string? code, IReadOnlyList<IReadOnlyList<CsvCell>> rows, int remainderRowCount, string? notice)
        {
            IsSuccess = isSuccess;
            Code = code;
            Rows = rows;
            RemainderRowCount = remainderRowCount;
            Notice = notice;
        }

        /// <summary>Gets a value indicating whether the preview was produced.</summary>
        public bool IsSuccess { get; }

        /// <summary>Gets the refusal code when <see cref="IsSuccess"/> is false.</summary>
        public string? Code { get; }

        /// <summary>Gets the stored rows. Empty when refused.</summary>
        public IReadOnlyList<IReadOnlyList<CsvCell>> Rows { get; }

        /// <summary>Gets how many rows were counted past the stored window.</summary>
        public int RemainderRowCount { get; }

        /// <summary>Gets the row-window notice, or null when every row fit.</summary>
        public string? Notice { get; }

        internal static CsvReadResult Ok(IReadOnlyList<IReadOnlyList<CsvCell>> rows, int remainderRowCount, string? notice) =>
            new(true, null, rows, remainderRowCount, notice);

        internal static CsvReadResult Fail(string code) =>
            new(false, code, [], 0, null);
    }

    /// <summary>Maximum stored rows.</summary>
    public const int MaxRows = 2000;

    /// <summary>Maximum columns in one row.</summary>
    public const int MaxColumns = 50;

    /// <summary>Maximum characters in one cell.</summary>
    public const int MaxCellCharacters = 2000;

    /// <summary>Maximum UTF-8 input length, in bytes.</summary>
    public const int MaxInputBytes = 1024 * 1024;

    /// <summary>Notice used when the row window is shorter than the file.</summary>
    public const string RowWindowNotice = "Showing the first 2000 rows";

    /// <summary>Parses UTF-8 CSV bytes. A leading UTF-8 BOM is ignored.</summary>
    /// <param name="utf8">The file bytes.</param>
    /// <returns>A preview, or a refusal that carries no partial table.</returns>
    internal static CsvReadResult Read(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length > MaxInputBytes)
        {
            return CsvReadResult.Fail("input_too_large");
        }

        var text = Decode(utf8);
        if (text is null)
        {
            return CsvReadResult.Fail("encoding");
        }

        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        return Parse(text);
    }

    private static string? Decode(ReadOnlySpan<byte> utf8)
    {
        try
        {
            var encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return encoding.GetString(utf8);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static CsvReadResult Parse(string text)
    {
        var rows = new List<IReadOnlyList<CsvCell>>();
        var row = new List<CsvCell>();
        var field = new System.Text.StringBuilder();
        var quoted = false;
        var remainder = 0;
        var storing = true;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            var failure = quoted
                ? ConsumeQuoted(text, ref index, field, ref quoted)
                : ConsumePlain(text, ref index, character, rows, ref row, field, ref quoted, ref storing, ref remainder);
            if (failure is not null)
            {
                return CsvReadResult.Fail(failure);
            }
        }

        if (quoted)
        {
            return CsvReadResult.Fail("unclosed_quote");
        }

        if (field.Length > 0 || row.Count > 0 || rows.Count == 0 && text.Length > 0)
        {
            if (!PushField(row, field))
            {
                return CsvReadResult.Fail("too_many_columns");
            }

            if (!Commit(rows, row, ref storing, ref remainder))
            {
                return CsvReadResult.Fail("too_many_rows");
            }
        }

        var notice = remainder > 0 ? RowWindowNotice : null;
        return CsvReadResult.Ok(rows, remainder, notice);
    }

    private static bool Append(System.Text.StringBuilder field)
    {
        if (field.Length >= MaxCellCharacters)
        {
            return false;
        }

        return true;
    }

    private static bool Append(System.Text.StringBuilder field, char character)
    {
        if (!Append(field))
        {
            return false;
        }

        field.Append(character);
        return true;
    }

    private static bool PushField(List<CsvCell> row, System.Text.StringBuilder field)
    {
        if (row.Count >= MaxColumns)
        {
            return false;
        }

        var text = field.ToString();
        field.Clear();
        row.Add(new CsvCell(text, IsFormulaLike(text)));
        return true;
    }

    private static bool Commit(List<IReadOnlyList<CsvCell>> rows, List<CsvCell> row, ref bool storing, ref int remainder)
    {
        if (storing)
        {
            if (rows.Count >= MaxRows)
            {
                storing = false;
                remainder++;
                return true;
            }

            rows.Add(row);
            return true;
        }

        remainder++;
        return true;
    }

    private static bool IsFormulaLike(string text) =>
        text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r';
    private static string? ConsumeQuoted(string text, ref int index, System.Text.StringBuilder field, ref bool quoted)
    {
        var character = text[index];
        if (character != '"')
        {
            return Append(field, character) ? null : "cell_too_long";
        }

        if (index + 1 < text.Length && text[index + 1] == '"')
        {
            index++;
            return Append(field, '"') ? null : "cell_too_long";
        }

        quoted = false;
        return null;
    }

    private static string? ConsumePlain(
        string text,
        ref int index,
        char character,
        List<IReadOnlyList<CsvCell>> rows,
        ref List<CsvCell> row,
        System.Text.StringBuilder field,
        ref bool quoted,
        ref bool storing,
        ref int remainder)
    {
        if (character == '"')
        {
            quoted = true;
            return null;
        }

        if (character == ',')
        {
            return PushField(row, field) ? null : "too_many_columns";
        }

        if (character is not '\n' and not '\r')
        {
            return Append(field, character) ? null : "cell_too_long";
        }

        if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
        {
            index++;
        }

        if (!PushField(row, field))
        {
            return "too_many_columns";
        }

        if (!Commit(rows, row, ref storing, ref remainder))
        {
            return "too_many_rows";
        }

        row = [];
        return null;
    }

}
