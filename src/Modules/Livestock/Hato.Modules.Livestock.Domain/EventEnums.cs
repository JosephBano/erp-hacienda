namespace Hato.Modules.Livestock.Domain;

public enum EventType
{
    Weighing,
    Treatment,
    Vaccination,
    Diagnosis,
    Movement,
    Disposal,
    Correction
}

public enum DisposalType
{
    Sale,
    Death,
    Culling,
    Stolen
}

public enum WithdrawalTarget
{
    Milk,
    Meat,
    Both
}
