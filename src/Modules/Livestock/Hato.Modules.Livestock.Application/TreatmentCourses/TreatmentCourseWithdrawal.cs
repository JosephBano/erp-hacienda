using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;

namespace Hato.Modules.Livestock.Application.TreatmentCourses;

/// <summary>
/// Shared withdrawal-period creation for <see cref="CreateTreatmentCourseCommand"/> and
/// <see cref="AddTreatmentCourseApplicationCommand"/> (task 5): the period is always
/// dated from the application that triggered it — which, for a multi-day course, is
/// always the *latest* one seen so far, because each new application adds its own
/// period rather than mutating a prior one (Art. 1: nothing already persisted is
/// edited). Querying "is this animal under withdrawal today" already unions every row,
/// so the window naturally tracks the last application without any row being rewritten.
///
/// <para>
/// Group-subject courses do not produce a <see cref="WithdrawalPeriod"/>: the entity
/// requires a single <see cref="WithdrawalPeriod.AnimalId"/> and group-level withdrawal
/// tracking does not exist yet in this codebase (same limitation
/// <c>RecordAnimalEventCommand</c> already has for individual events) — out of scope
/// for this sub-branch (see "Lo que NO incluye" — group withdrawal is not part of
/// 3.5a.2-B's task list).
/// </para>
/// </summary>
internal static class TreatmentCourseWithdrawal
{
    public static void AddWithdrawalPeriods(
        ILivestockDbContext dbContext,
        Guid treatmentCourseId,
        Guid? animalId,
        DateTimeOffset appliedAt,
        int? milkWithdrawalDays,
        int? meatWithdrawalDays)
    {
        if (animalId is not { } animal) return;

        var startDate = DateOnly.FromDateTime(appliedAt.UtcDateTime);

        if (milkWithdrawalDays is > 0 && meatWithdrawalDays is > 0)
        {
            var maxDays = Math.Max(milkWithdrawalDays.Value, meatWithdrawalDays.Value);
            dbContext.WithdrawalPeriods.Add(WithdrawalPeriod.ForTreatmentCourse(
                animal, treatmentCourseId, WithdrawalTarget.Both, startDate, startDate.AddDays(maxDays)));
        }
        else if (milkWithdrawalDays is > 0)
        {
            dbContext.WithdrawalPeriods.Add(WithdrawalPeriod.ForTreatmentCourse(
                animal, treatmentCourseId, WithdrawalTarget.Milk, startDate, startDate.AddDays(milkWithdrawalDays.Value)));
        }
        else if (meatWithdrawalDays is > 0)
        {
            dbContext.WithdrawalPeriods.Add(WithdrawalPeriod.ForTreatmentCourse(
                animal, treatmentCourseId, WithdrawalTarget.Meat, startDate, startDate.AddDays(meatWithdrawalDays.Value)));
        }
    }
}
