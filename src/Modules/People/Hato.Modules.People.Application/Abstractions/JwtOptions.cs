namespace Hato.Modules.People.Application.Abstractions;

/// <summary>Configuration for JWT issuance/validation, bound from the "Jwt" section.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Hato";
    public string Audience { get; set; } = "HatoClients";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 480;
}
