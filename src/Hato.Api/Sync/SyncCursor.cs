using System.Globalization;

namespace Hato.Api.Sync;

/// <summary>
/// Position of a client inside the server's change stream (ADR-0008).
///
/// A cursor is a timestamp *plus* an id. The id is not decoration: two rows written in
/// the same millisecond are indistinguishable by time alone, so a timestamp-only cursor
/// must choose between re-sending them forever (if it uses <c>&gt;=</c>) or dropping the
/// ones it did not reach (if it uses <c>&gt;</c>). Ordering by <c>(timestamp, id)</c> and
/// comparing lexicographically on that pair makes the stream a total order, which is what
/// lets the protocol promise "no pierde ni repite".
/// </summary>
public readonly record struct SyncCursor(DateTimeOffset Timestamp, Guid Id)
{
    private const char Separator = '|';

    /// <summary>Start of time: delivers the client its full initial snapshot.</summary>
    public static SyncCursor Beginning => new(DateTimeOffset.MinValue, Guid.Empty);

    public static SyncCursor Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Beginning;
        }

        var separatorIndex = raw.LastIndexOf(Separator);
        if (separatorIndex < 0)
        {
            // Tolerated for forward compatibility with a client holding a timestamp-only
            // cursor: it may re-receive rows from that instant, but never skips any.
            return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var only)
                ? new SyncCursor(only, Guid.Empty)
                : Beginning;
        }

        var timePart = raw[..separatorIndex];
        var idPart = raw[(separatorIndex + 1)..];

        if (!DateTimeOffset.TryParse(timePart, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var timestamp))
        {
            return Beginning;
        }

        return new SyncCursor(timestamp, Guid.TryParse(idPart, out var id) ? id : Guid.Empty);
    }

    public string Format() =>
        Timestamp == DateTimeOffset.MinValue && Id == Guid.Empty
            ? string.Empty
            : $"{Timestamp.ToUniversalTime():O}{Separator}{Id}";

    public bool IsBefore(SyncCursor other) =>
        Timestamp < other.Timestamp || (Timestamp == other.Timestamp && Id.CompareTo(other.Id) < 0);
}
