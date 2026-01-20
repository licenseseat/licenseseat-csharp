using System.Text.Json.Serialization;

namespace LicenseSeat;

/// <summary>
/// Represents a product in the LicenseSeat system.
/// </summary>
public sealed class Product
{
    /// <summary>
    /// Gets or sets the unique slug identifying this product.
    /// </summary>
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the product.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
