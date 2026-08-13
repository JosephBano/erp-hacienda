namespace Hato.Modules.People.Domain;

/// <summary>
/// N:N relationship entity between User and Role in database (ADR-0007 + Art. 8).
/// </summary>
public class UserRole
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid? GroupId { get; private set; }

    public User User { get; internal set; } = null!;
    public Role Role { get; internal set; } = null!;

    private UserRole() { }

    public UserRole(Guid userId, Guid roleId, Guid? groupId = null)
    {
        UserId = userId;
        RoleId = roleId;
        GroupId = groupId;
    }

    public UserRole(User user, Role role, Guid? groupId = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(role);
        UserId = user.Id;
        RoleId = role.Id;
        User = user;
        Role = role;
        GroupId = groupId;
    }
}

/// <summary>
/// Known system role codes for seeding and core policies (ADR-0007).
/// </summary>
public static class SystemRoles
{
    public const string Admin = "admin";
    public const string Registrar = "registrar";
    public const string Veterinarian = "veterinarian";
}

/// <summary>
/// System permissions constants per module (ADR-0007).
/// </summary>
public static class SystemPermissions
{
    // People
    public const string PeopleUsersManage = "people.users.manage";
    public const string PeopleRolesManage = "people.roles.manage";
    public const string PeopleUsersRead = "people.users.read";

    // Livestock
    public const string LivestockAnimalsRead = "livestock.animals.read";
    public const string LivestockAnimalsWrite = "livestock.animals.write";
    public const string LivestockCategoriesManage = "livestock.categories.manage";
    public const string LivestockBreedsManage = "livestock.breeds.manage";
    public const string LivestockSpeciesManage = "livestock.species.manage";
    public const string LivestockTreatmentsConfigure = "livestock.treatments.configure";

    // Production
    public const string ProductionMilkingRecord = "production.milking.record";
    public const string ProductionMilkingRead = "production.milking.read";

    // Inventory
    public const string InventoryItemsManage = "inventory.items.manage";
    public const string InventoryItemsRead = "inventory.items.read";
    public const string InventoryFeedStagesManage = "inventory.feed-stages.manage";
    // ADR-0026 Decisión 5: dedicated permission for the InventoryReception flow, so a
    // future "registrador de compras" role can create receptions without gaining
    // inventory-items.manage (which would let them mutate the catalogue too).
    public const string InventoryReceptionsManage = "inventory.receptions.manage";

    // Breeding
    public const string BreedingEventsRecord = "breeding.events.record";
    public const string BreedingEventsRead = "breeding.events.read";

    // Tasks
    public const string TasksManage = "tasks.manage";
    public const string TasksRead = "tasks.read";

    // Settings / farm-modules (ADR-0019). The on/off decision for each module is
    // owned by the product owner, not the catalogue of data, so it lives behind
    // a permission that does not depend on which module the toggle controls.
    public const string SettingsFarmModulesRead = "settings.farm-modules.read";
    public const string SettingsFarmModulesManage = "settings.farm-modules.manage";
}
