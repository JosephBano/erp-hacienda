namespace Hato.Modules.Delivery.Domain.Enums;

public static class MobileBuildStatuses
{
    public const string Requested = "Requested";
    public const string Queued = "Queued";
    public const string Building = "Building";
    public const string Verifying = "Verifying";
    public const string Candidate = "Candidate";
    public const string Published = "Published";
    public const string Failed = "Failed";
    public const string Withdrawn = "Withdrawn";

    public static bool IsTerminal(string status) =>
        status is Published or Failed or Withdrawn;
}
