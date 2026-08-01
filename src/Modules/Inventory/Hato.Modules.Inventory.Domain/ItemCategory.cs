using System.Text.Json.Serialization;

namespace Hato.Modules.Inventory.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ItemCategory
{
    Medicine,
    Feed,
    Supply,
    Product
}
