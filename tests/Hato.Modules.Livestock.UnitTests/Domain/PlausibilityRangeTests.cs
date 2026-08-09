using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

/// <summary>
/// PlausibilityRange entity tests (ADR-0022).
///
/// The entity validates that the four bounds are coherent when all are present:
/// absolute_min &lt;= plausible_min &lt;= plausible_max &lt;= absolute_max.
/// The magnitude must be a lower-snake-case identifier (Art. 8: data, not enum).
/// Soft delete flips IsActive; the unique constraint (species_id, category_id,
/// magnitude) is enforced by the database, not the entity (see configuration).
/// </summary>
public class PlausibilityRangeTests
{
    private readonly Guid _speciesId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidBounds_StoresThem()
    {
        var range = PlausibilityRange.Create(
            _speciesId,
            categoryId: null,
            magnitude: "weight_kg",
            plausibleMin: 0.5m,
            plausibleMax: 250m,
            absoluteMin: 0.1m,
            absoluteMax: 400m);

        Assert.Equal(_speciesId, range.SpeciesId);
        Assert.Null(range.CategoryId);
        Assert.Equal("weight_kg", range.Magnitude);
        Assert.Equal(0.5m, range.PlausibleMin);
        Assert.Equal(250m, range.PlausibleMax);
        Assert.Equal(0.1m, range.AbsoluteMin);
        Assert.Equal(400m, range.AbsoluteMax);
        Assert.True(range.IsActive);
    }

    [Fact]
    public void Create_WithEmptySpeciesId_Throws()
    {
        Assert.Throws<DomainException>(() => PlausibilityRange.Create(
            Guid.Empty,
            categoryId: null,
            magnitude: "weight_kg",
            plausibleMin: 0.5m,
            plausibleMax: 250m,
            absoluteMin: 0.1m,
            absoluteMax: 400m));
    }

    [Fact]
    public void Create_WithInvalidMagnitude_Throws()
    {
        // Uppercase
        Assert.Throws<DomainException>(() => PlausibilityRange.Create(
            _speciesId, null, "Weight_Kg", 0.5m, 250m, 0.1m, 400m));

        // Spaces
        Assert.Throws<DomainException>(() => PlausibilityRange.Create(
            _speciesId, null, "weight kg", 0.5m, 250m, 0.1m, 400m));

        // Empty
        Assert.Throws<DomainException>(() => PlausibilityRange.Create(
            _speciesId, null, "  ", 0.5m, 250m, 0.1m, 400m));

        // Too long
        Assert.Throws<DomainException>(() => PlausibilityRange.Create(
            _speciesId, null, new string('a', 51), 0.5m, 250m, 0.1m, 400m));
    }

    [Fact]
    public void Create_WithBoundsInverted_Throws()
    {
        // plausible_max < plausible_min
        Assert.Throws<DomainException>(() => PlausibilityRange.Create(
            _speciesId, null, "weight_kg", 100m, 50m, 0m, 500m));

        // absolute_max < absolute_min
        Assert.Throws<DomainException>(() => PlausibilityRange.Create(
            _speciesId, null, "weight_kg", 0m, 500m, 200m, 100m));

        // plausible_min < absolute_min
        Assert.Throws<DomainException>(() => PlausibilityRange.Create(
            _speciesId, null, "weight_kg", 0m, 500m, 100m, 600m));

        // absolute_max < plausible_max
        Assert.Throws<DomainException>(() => PlausibilityRange.Create(
            _speciesId, null, "weight_kg", 0m, 600m, 0m, 500m));
    }

    [Fact]
    public void Create_WithoutBounds_Allowed()
    {
        // ADR-0022 sec.3: no bounds = sentinel meaning "no range configured".
        // The evaluator (fail-open) handles this case explicitly.
        var range = PlausibilityRange.Create(
            _speciesId, null, "weight_kg", null, null, null, null);

        Assert.Null(range.PlausibleMin);
        Assert.Null(range.PlausibleMax);
        Assert.Null(range.AbsoluteMin);
        Assert.Null(range.AbsoluteMax);
        Assert.True(range.IsActive);
    }

    [Fact]
    public void Create_WithOnlySomeBounds_Allows()
    {
        // Realistic cases: only plausibles, only absolutes, only one bound.
        var onlyPlausibles = PlausibilityRange.Create(
            _speciesId, null, "milk_liters", 1m, 50m, null, null);
        Assert.Null(onlyPlausibles.AbsoluteMin);
        Assert.Null(onlyPlausibles.AbsoluteMax);

        var onlyAbsolutes = PlausibilityRange.Create(
            _speciesId, null, "weight_kg", null, null, 0.1m, 500m);
        Assert.Null(onlyAbsolutes.PlausibleMin);
        Assert.Null(onlyAbsolutes.PlausibleMax);
    }

    [Fact]
    public void Create_WithCategory_StoresIt()
    {
        var range = PlausibilityRange.Create(
            _speciesId, _categoryId, "weight_kg", 1m, 50m, 0.1m, 100m);

        Assert.Equal(_categoryId, range.CategoryId);
    }

    [Fact]
    public void Deactivate_ThenDeactivateAgain_Throws()
    {
        var range = PlausibilityRange.Create(
            _speciesId, null, "weight_kg", 0.5m, 250m, 0.1m, 400m);

        range.Deactivate();

        Assert.False(range.IsActive);
        Assert.Throws<DomainException>(() => range.Deactivate());
    }

    [Fact]
    public void Activate_OnAlreadyActiveRange_Throws()
    {
        var range = PlausibilityRange.Create(
            _speciesId, null, "weight_kg", 0.5m, 250m, 0.1m, 400m);

        Assert.Throws<DomainException>(() => range.Activate());
    }

    [Fact]
    public void Activate_AfterDeactivate_RestoresIt()
    {
        var range = PlausibilityRange.Create(
            _speciesId, null, "weight_kg", 0.5m, 250m, 0.1m, 400m);

        range.Deactivate();
        range.Activate();

        Assert.True(range.IsActive);
    }

    [Fact]
    public void UpdateBounds_WithValidNewBounds_UpdatesThem()
    {
        var range = PlausibilityRange.Create(
            _speciesId, null, "weight_kg", 0.5m, 250m, 0.1m, 400m);

        range.UpdateBounds(1m, 300m, 0.1m, 450m);

        Assert.Equal(1m, range.PlausibleMin);
        Assert.Equal(300m, range.PlausibleMax);
        Assert.Equal(0.1m, range.AbsoluteMin);
        Assert.Equal(450m, range.AbsoluteMax);
    }

    [Fact]
    public void UpdateBounds_WithInvertedOrder_Throws()
    {
        var range = PlausibilityRange.Create(
            _speciesId, null, "weight_kg", 0.5m, 250m, 0.1m, 400m);

        Assert.Throws<DomainException>(() => range.UpdateBounds(300m, 1m, 0.1m, 450m));
    }
}
