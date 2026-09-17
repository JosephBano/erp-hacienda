using Hato.Modules.People.Infrastructure.Persistence;
using Hato.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Modules.People.IntegrationTests;

/// <summary>
/// Regression: confirms that <c>20260811214054_SeedInventoryFeedStagesManage</c> is
/// actually applied by <c>Database.MigrateAsync()</c>. The original PR shipped the
/// .cs without its .Designer.cs and without updating the model snapshot, which made
/// EF Core ignore the migration at runtime — the permission never reached
/// <c>people.permissions</c>, and any non-admin caller would have been rejected.
/// </summary>
public class SeedInventoryFeedStagesManageMigrationTests(PeopleApiFactory factory) : IClassFixture<PeopleApiFactory>
{
    [Fact]
    public async Task Migration_IsAppliedAndSeedsPermission_ForAdmin()
    {
        // Trigger the fixture startup so migrations are applied.
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PeopleDbContext>();

        // 1. The migration itself runs as part of MigrateAsync.
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20260811214054_SeedInventoryFeedStagesManage", applied);

        // 2. The permission row exists with the expected code.
        var permission = await db.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == "inventory.feed-stages.manage");
        Assert.NotNull(permission);
        Assert.Equal("Gestionar Etapas de Alimento", permission!.Name);

        // 3. The admin role has it.
        var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var adminHasIt = await db.RolePermissions
            .AsNoTracking()
            .AnyAsync(rp => rp.RoleId == adminId && rp.PermissionId == permission.Id);
        Assert.True(adminHasIt);
    }
}
