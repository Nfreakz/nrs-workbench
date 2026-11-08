using System.Globalization;
using System.Text;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.Core.Services;

/// <summary>
/// Minimal RFC 4180 CSV formatting: comma separated, CRLF rows, UTF-8 with BOM so
/// Excel opens accented text correctly. Fields containing a comma, a quote, a CR or
/// an LF are quoted and embedded quotes are doubled.
/// </summary>
public static class CsvWriter
{
    public static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static string Escape(string? field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        var needsQuotes = field.IndexOfAny([',', '"', '\r', '\n']) >= 0;
        if (!needsQuotes) return field;
        return "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public static string FormatLine(IEnumerable<string?> fields) =>
        string.Join(",", fields.Select(Escape));

    public static string Build(IEnumerable<string?> header, IEnumerable<IEnumerable<string?>> rows)
    {
        var builder = new StringBuilder();
        builder.Append(FormatLine(header)).Append("\r\n");
        foreach (var row in rows) builder.Append(FormatLine(row)).Append("\r\n");
        return builder.ToString();
    }
}

/// <summary>Flattens job history records into CSV rows. Labels are supplied by the caller so the output follows the UI language.</summary>
public static class JobRunCsvExport
{
    public const int ColumnCount = 7;

    public static IEnumerable<string?> ToRow(JobRunRecord record, Func<JobRunOutcome, string> outcomeLabel) =>
    [
        record.RunnerAlias,
        record.JobName,
        record.GitHubTarget,
        outcomeLabel(record.Outcome),
        record.StartedAt.ToLocalTime().ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture),
        Math.Max(0, (long)Math.Round(record.Duration.TotalSeconds)).ToString(CultureInfo.InvariantCulture),
        record.SourceFile
    ];

    public static string Build(IEnumerable<JobRunRecord> records, IReadOnlyList<string> header, Func<JobRunOutcome, string> outcomeLabel)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(outcomeLabel);
        if (header.Count != ColumnCount)
            throw new ArgumentException($"The CSV header must contain {ColumnCount} columns.", nameof(header));
        return CsvWriter.Build(header, records.Select(record => ToRow(record, outcomeLabel)));
    }
}
