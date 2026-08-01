using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.Pedigree;

public record GetPedigreeQuery(Guid AnimalId, int MaxGenerations = 4) : IRequest<PedigreeDto?>;

public class GetPedigreeQueryHandler(IBreedingDbContext breedingDb)
    : IRequestHandler<GetPedigreeQuery, PedigreeDto?>
{
    public async Task<PedigreeDto?> Handle(GetPedigreeQuery request, CancellationToken cancellationToken)
    {
        // Query recursive pedigree via EF Core DbContext query / raw SQL or memory tree resolution.
        // We fetch animal relationships up to MaxGenerations.
        var targetAnimalId = request.AnimalId;

        // Execute recursive CTE on livestock.animals table
        var dbContext = (DbContext)breedingDb;
        var connection = dbContext.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            WITH RECURSIVE pedigree_tree AS (
                -- Base case: target animal
                SELECT 
                    a.id AS animal_id,
                    a.sex,
                    a.mother_id,
                    a.father_animal_id,
                    a.father_straw_id,
                    0 AS generation_level,
                    'Self' AS role
                FROM livestock.animals a
                WHERE a.id = @targetId AND a.deleted_at IS NULL

                UNION ALL

                -- Recursive case: mothers
                SELECT 
                    m.id AS animal_id,
                    m.sex,
                    m.mother_id,
                    m.father_animal_id,
                    m.father_straw_id,
                    pt.generation_level + 1,
                    'Mother' AS role
                FROM pedigree_tree pt
                JOIN livestock.animals m ON pt.mother_id = m.id AND m.deleted_at IS NULL
                WHERE pt.generation_level < @maxGen

                UNION ALL

                -- Recursive case: father animals
                SELECT 
                    f.id AS animal_id,
                    f.sex,
                    f.mother_id,
                    f.father_animal_id,
                    f.father_straw_id,
                    pt.generation_level + 1,
                    'Father' AS role
                FROM pedigree_tree pt
                JOIN livestock.animals f ON pt.father_animal_id = f.id AND f.deleted_at IS NULL
                WHERE pt.generation_level < @maxGen
            )
            SELECT pt.animal_id, pt.sex, pt.generation_level, pt.role, pt.mother_id, pt.father_animal_id, pt.father_straw_id, s.bull_name
            FROM pedigree_tree pt
            LEFT JOIN breeding.semen_straws s ON pt.father_straw_id = s.id
            ORDER BY pt.generation_level, pt.role;
            """;

        var paramTarget = command.CreateParameter();
        paramTarget.ParameterName = "@targetId";
        paramTarget.Value = targetAnimalId;
        command.Parameters.Add(paramTarget);

        var paramMaxGen = command.CreateParameter();
        paramMaxGen.ParameterName = "@maxGen";
        paramMaxGen.Value = request.MaxGenerations;
        command.Parameters.Add(paramMaxGen);

        var ancestors = new List<AncestorDto>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var animalId = reader.GetGuid(0);
            var sex = reader.GetString(1);
            var genLevel = reader.GetInt32(2);
            var role = reader.GetString(3);
            var motherId = reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4);
            var fatherAnimalId = reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5);
            var fatherStrawId = reader.IsDBNull(6) ? (Guid?)null : reader.GetGuid(6);
            var strawBullName = reader.IsDBNull(7) ? null : reader.GetString(7);

            ancestors.Add(new AncestorDto(
                animalId,
                null,
                sex,
                genLevel,
                role,
                motherId,
                fatherAnimalId,
                fatherStrawId,
                strawBullName
            ));
        }

        if (ancestors.Count == 0)
            return null;

        var target = ancestors.First();
        return new PedigreeDto(
            target.AnimalId,
            target.FarmTag,
            ancestors.Where(a => a.GenerationLevel > 0).ToList()
        );
    }
}
