namespace Hato.Modules.Livestock.Domain;

/// <summary>Types of external identifier an animal may carry (GLOSSARY.md — Identificación).</summary>
public enum IdentifierType
{
    FarmTag = 1,
    OfficialTag = 2,
    Rfid = 3,
    Name = 4,
}
