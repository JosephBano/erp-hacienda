namespace Hato.Modules.Delivery.Domain;

public class MobileVersionCodeSequence
{
    public string PackageName { get; private set; } = null!;
    public int LastVersionCode { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private MobileVersionCodeSequence() { }

    public static MobileVersionCodeSequence Create(string packageName, int initialCode)
    {
        return new MobileVersionCodeSequence
        {
            PackageName = packageName,
            LastVersionCode = initialCode,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    public int Next()
    {
        LastVersionCode++;
        UpdatedAt = DateTimeOffset.UtcNow;
        return LastVersionCode;
    }
}
