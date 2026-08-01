using Hato.Modules.Livestock.Contracts;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Infrastructure.CrossModule;

/// <summary>
/// Runs the recursive pedigree walk against this module's own schema only (Art. 6):
/// Breeding calls this contract instead of querying livestock.animals directly.
/// </summary>
public class AnimalGenealogyReader(LivestockDbContext dbContext) : IAnimalGenealogyReader
{
    public async Task<IReadOnlyList<AnimalAncestorDto>> GetAncestryAsync(Guid animalId, int maxGenerations, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            WITH RECURSIVE pedigree_tree AS (
                SELECT a.id AS animal_id, a.sex, a.mother_id, a.father_animal_id, a.father_straw_id,
                       0 AS generation_level, 'Self' AS role
                FROM livestock.animals a
                WHERE a.id = @targetId AND a.deleted_at IS NULL

                UNION ALL

                SELECT m.id, m.sex, m.mother_id, m.father_animal_id, m.father_straw_id,
                       pt.generation_level + 1, 'Mother'
                FROM pedigree_tree pt
                JOIN livestock.animals m ON pt.mother_id = m.id AND m.deleted_at IS NULL
                WHERE pt.generation_level < @maxGen

                UNION ALL

                SELECT f.id, f.sex, f.mother_id, f.father_animal_id, f.father_straw_id,
                       pt.generation_level + 1, 'Father'
                FROM pedigree_tree pt
                JOIN livestock.animals f ON pt.father_animal_id = f.id AND f.deleted_at IS NULL
                WHERE pt.generation_level < @maxGen
            )
            SELECT pt.animal_id, pt.sex, pt.generation_level, pt.role, pt.mother_id, pt.father_animal_id, pt.father_straw_id,
                   (SELECT i.value FROM livestock.animal_identifiers i
                    WHERE i.animal_id = pt.animal_id AND i.type = 'FarmTag' AND i.valid_to IS NULL
                    LIMIT 1) AS farm_tag
            FROM pedigree_tree pt
            ORDER BY pt.generation_level, pt.role;
            """;

        var targetParam = command.CreateParameter();
        targetParam.ParameterName = "@targetId";
        targetParam.Value = animalId;
        command.Parameters.Add(targetParam);

        var maxGenParam = command.CreateParameter();
        maxGenParam.ParameterName = "@maxGen";
        maxGenParam.Value = maxGenerations;
        command.Parameters.Add(maxGenParam);

        var result = new List<AnimalAncestorDto>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new AnimalAncestorDto(
                AnimalId: reader.GetGuid(0),
                Sex: reader.GetString(1),
                GenerationLevel: reader.GetInt32(2),
                Role: reader.GetString(3),
                MotherId: reader.IsDBNull(4) ? null : reader.GetGuid(4),
                FatherAnimalId: reader.IsDBNull(5) ? null : reader.GetGuid(5),
                FatherStrawId: reader.IsDBNull(6) ? null : reader.GetGuid(6),
                FarmTag: reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return result;
    }
}
