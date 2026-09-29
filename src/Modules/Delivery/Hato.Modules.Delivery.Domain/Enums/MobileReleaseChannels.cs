namespace Hato.Modules.Delivery.Domain.Enums;

public static class MobileReleaseChannels
{
    public const string Stage = "stage";
    public const string Prod = "prod";

    public static readonly string[] All = [Stage, Prod];

    public static bool IsValid(string channel) =>
        channel is Stage or Prod;
}
