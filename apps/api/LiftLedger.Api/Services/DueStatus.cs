using LiftLedger.Api.Domain;

namespace LiftLedger.Api.Services;

public static class DueStatus
{
    public const string Overdue = "Overdue";
    public const string DueSoon = "Due soon";
    public const string Scheduled = "Scheduled";
    public const string NotSet = "Not set";

    public const int DueSoonDays = 30;

    public static string For(DateOnly? nextDue, DateOnly? today = null)
    {
        if (nextDue is null)
        {
            return NotSet;
        }

        var current = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (nextDue.Value < current)
        {
            return Overdue;
        }

        if (nextDue.Value <= current.AddDays(DueSoonDays))
        {
            return DueSoon;
        }

        return Scheduled;
    }
}
