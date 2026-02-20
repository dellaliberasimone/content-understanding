using System.Text.Json.Serialization;

namespace ContentUnderstanding.Client.Models;

/// <summary>
/// Represents the schema definition for fields to extract from content.
/// </summary>
public class FieldSchema
{
    [JsonPropertyName("fields")]
    public Dictionary<string, FieldDefinition> Fields { get; set; } = new();
}

/// <summary>
/// Defines a single field for extraction including its type, description, and optional
/// nested structure. Supports primitive types, objects, arrays, and special extraction
/// methods such as classification and generation.
/// </summary>
public class FieldDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Optional extraction method (e.g., "classify", "generate").
    /// </summary>
    [JsonPropertyName("method")]
    public string? Method { get; set; }

    /// <summary>
    /// Valid values for classification fields (used with method "classify").
    /// </summary>
    [JsonPropertyName("enum")]
    public List<string>? EnumValues { get; set; }

    /// <summary>
    /// Schema for items when <see cref="Type"/> is "array".
    /// </summary>
    [JsonPropertyName("items")]
    public FieldDefinition? Items { get; set; }

    /// <summary>
    /// Property definitions when <see cref="Type"/> is "object".
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, FieldDefinition>? Properties { get; set; }
}
