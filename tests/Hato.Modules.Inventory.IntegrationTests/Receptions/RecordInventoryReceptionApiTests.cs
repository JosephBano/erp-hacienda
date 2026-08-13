using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Inventory.Application.Items;
using Hato.Modules.Inventory.Domain;
using Hato.Modules.Inventory.Infrastructure.Persistence;
using Hato.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Modules.Inventory.IntegrationTests.Receptions;

/// <summary>
/// ADR-0026 end-to-end coverage: the new <c>POST /api/v1/inventory/items/{itemId}/receptions</c>
/// endpoint must accept the operator-declared reception payload, persist it on the
/// batch row, surface the same fields through <c>GET /batches</c>, and — because the
/// TestAuthHandler runs as Admin — succeed under the new <c>InventoryReceptionsManage</c>
/// policy. Authorization-specific negatives (401 / 403) live in
/// <see cref="RecordInventoryReceptionAuthApiTests"/>.
/// </summary>
public class RecordInventoryReceptionApiTests(InventoryApiFactory factory) : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostReception_WithBaseUnit_RoundTripsThroughPostgresAndSurfacesOnGet()
    {
        // 1. Create the inventory item (kg is the base unit).
        var itemId = await CreateFeedItemAsync("Balanceado Ordeño 18%");

        // 2. Record a reception: base unit => factor 1, quantity survives verbatim.
        // receivedAt must be >= item.CreatedAt — set it to "now" so the domain
        // invariant `receivedAt < CreatedAt` does not trip right after item creation.
        var receivedAt = DateTimeOffset.UtcNow.ToString("o");
        var resp = await _client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/receptions", new
        {
            batchNumber = "LOT-2026-08-A",
            quantity = 500m,
            unit = "kg",
            costPerUnit = 0.42m,
            expirationDate = new DateOnly(2026, 12, 31),
            receivedAt,
            supplierLabel = "Agropecuaria XYZ S.A.",
            invoiceReference = "F-001-002345",
            notes = "Llegó en camión propio",
            recordedById = (Guid?)null,
            recordedByLabel = "mayordomo 1",
        });

        resp.EnsureSuccessStatusCode();
        var created = await resp.Content.ReadFromJsonAsync<CreatedId>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);

        // 3. The same batch must show up on GET /batches with all reception fields.
        var batches = await _client.GetFromJsonAsync<List<InventoryBatchDto>>(
            $"/api/v1/inventory/items/{itemId}/batches");
        var batch = Assert.Single(batches!);
        Assert.Equal(created.Id, batch.Id);
        Assert.Equal("LOT-2026-08-A", batch.BatchNumber);
        Assert.Equal(500m, batch.Quantity);
        Assert.Equal("Agropecuaria XYZ S.A.", batch.SupplierLabel);
        Assert.Equal("F-001-002345", batch.InvoiceReference);
        Assert.Equal("Llegó en camión propio", batch.Notes);
        Assert.Equal("mayordomo 1", batch.RecordedByLabel);
    }

    [Fact]
    public async Task PostReception_WithConversionUnit_AppliesFactor()
    {
        var itemId = await CreateFeedItemAsync("Balanceado Cerdo");

        // First, register a unit conversion 1 saco40kg = 40 kg.
        var conv = await _client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/unit-conversions", new
        {
            fromUnit = "saco40kg",
            toUnit = "kg",
            factor = 40m,
        });
        conv.EnsureSuccessStatusCode();

        // 40 sacos → 1600 kg (rounded to 3 decimals with bankers' rounding).
        // receivedAt = now so we don't trip the "receivedAt < item.CreatedAt" invariant.
        var resp = await _client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/receptions", new
        {
            batchNumber = "LOT-2026-08-B",
            quantity = 40m,
            unit = "saco40kg",
            costPerUnit = 17m,
            receivedAt = DateTimeOffset.UtcNow.ToString("o"),
        });
        resp.EnsureSuccessStatusCode();

        var batches = await _client.GetFromJsonAsync<List<InventoryBatchDto>>(
            $"/api/v1/inventory/items/{itemId}/batches");
        var batch = Assert.Single(batches!);
        Assert.Equal(1600m, batch.Quantity); // 40 * 40 = 1600
    }

    [Fact]
    public async Task PostReception_UnknownUnitConversion_ReturnsBadRequest()
    {
        var itemId = await CreateFeedItemAsync("Concentrado Trigo");

        // No UnitConversion registered for "bulto50kg" → "kg".
        var resp = await _client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/receptions", new
        {
            batchNumber = "LOT-2026-08-C",
            quantity = 10m,
            unit = "bulto50kg",
            costPerUnit = 25m,
            receivedAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"),
        });

        // DomainException surfaces as 400 ProblemDetails via the validation pipeline.
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PostReception_ItemNotFound_ReturnsBadRequest()
    {
        var resp = await _client.PostAsJsonAsync(
            $"/api/v1/inventory/items/{Guid.NewGuid()}/receptions", new
            {
                batchNumber = "LOT-2026-08-D",
                quantity = 1m,
                unit = "kg",
                costPerUnit = 0.5m,
                receivedAt = DateTimeOffset.UtcNow.ToString("o"),
            });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PostReception_FutureReceivedAt_ReturnsBadRequest()
    {
        var itemId = await CreateFeedItemAsync("Sales Minerales");

        var resp = await _client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/receptions", new
        {
            batchNumber = "LOT-2026-08-E",
            quantity = 10m,
            unit = "kg",
            costPerUnit = 0.5m,
            // Future date: the validator must reject it before the domain method runs.
            receivedAt = DateTimeOffset.UtcNow.AddMinutes(10).ToString("o"),
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PostReception_QuantityZero_ReturnsBadRequest()
    {
        var itemId = await CreateFeedItemAsync("Preiniciador");

        var resp = await _client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/receptions", new
        {
            batchNumber = "LOT-2026-08-F",
            quantity = 0m,
            unit = "kg",
            costPerUnit = 0.5m,
            receivedAt = DateTimeOffset.UtcNow.ToString("o"),
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PostReception_OverwritesRecordedByIdAndLabelFromJwtSubject()
    {
        // AL-01 (security audit #94): the handler must persist the authenticated
        // subject as the batch author, never the values supplied by the client.
        // This test sends an attacker-controlled RecordedById and an empty
        // RecordedByLabel and asserts that the persisted row carries the JWT
        // subject's id and full name. (When the operator types a non-empty label
        // the handler preserves it — see the dedicated test below for that
        // branch.)
        var itemId = await CreateFeedItemAsync("AL01 Override");

        var attackerUserId = Guid.NewGuid();

        var resp = await _client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/receptions", new
        {
            batchNumber = "LOT-AUTH-001",
            quantity = 50m,
            unit = "kg",
            costPerUnit = 0.5m,
            receivedAt = DateTimeOffset.UtcNow.ToString("o"),
            supplierLabel = "Proveedor X",
            recordedById = (Guid?)attackerUserId,
            recordedByLabel = (string?)null,
        });
        resp.EnsureSuccessStatusCode();
        var created = await resp.Content.ReadFromJsonAsync<CreatedId>();
        Assert.NotNull(created);

        // The DTO only surfaces RecordedByLabel; query the row directly to read
        // the RecordedById the handler actually persisted.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var row = await db.InventoryBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == created!.Id);

        Assert.NotNull(row);
        Assert.NotEqual(attackerUserId, row!.RecordedById);            // not the spoofed value
        Assert.Equal(TestAuthHandler.AdminUserId, row.RecordedById);    // JWT subject
        Assert.Equal(TestAuthHandler.AdminFullName, row.RecordedByLabel); // JWT fallback
    }

    [Fact]
    public async Task PostReception_PreservesOperatorTypedRecordedByLabelWhenSupplied()
    {
        // Counterpart to AL01_OverrideFromJwt: when the operator types a
        // non-empty RecordedByLabel (legitimate "mayordomo 1" UX), the handler
        // preserves it. The handler still overrides RecordedById with the JWT
        // subject — only the label is allowed to follow the request.
        var itemId = await CreateFeedItemAsync("AL01 Preserve");

        var resp = await _client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/receptions", new
        {
            batchNumber = "LOT-AUTH-002",
            quantity = 30m,
            unit = "kg",
            costPerUnit = 0.5m,
            receivedAt = DateTimeOffset.UtcNow.ToString("o"),
            supplierLabel = "Proveedor Y",
            recordedById = (Guid?)Guid.NewGuid(),
            recordedByLabel = "mayordomo 1",
        });
        resp.EnsureSuccessStatusCode();
        var created = await resp.Content.ReadFromJsonAsync<CreatedId>();
        Assert.NotNull(created);

        var batches = await _client.GetFromJsonAsync<List<InventoryBatchDto>>(
            $"/api/v1/inventory/items/{itemId}/batches");
        var batch = Assert.Single(batches!);
        Assert.Equal("mayordomo 1", batch.RecordedByLabel);
    }

    private async Task<Guid> CreateFeedItemAsync(string name)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = $"{name}-{Guid.NewGuid():N}",
            category = ItemCategory.Feed,
            unit = "kg",
            minStock = 200m,
        });
        resp.EnsureSuccessStatusCode();
        var created = await resp.Content.ReadFromJsonAsync<CreatedId>();
        return created!.Id;
    }

    private sealed record CreatedId(Guid Id);
}
