namespace Hato.Modules.Delivery.Domain.Enums;

public static class MobileReleaseStatuses
{
    public const string Candidate = "Candidate";
    public const string Published = "Published";
    public const string Withdrawn = "Withdrawn";

    public static bool CanPublish(string currentStatus) =>
        currentStatus is Candidate;

    public static bool CanWithdraw(string currentStatus) =>
        currentStatus is Published;
}
