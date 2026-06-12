using System.ComponentModel.DataAnnotations;

namespace RushOrder.API.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required(ErrorMessage = "Database:ConnectionString is required")]
    public string ConnectionString { get; init; } = string.Empty;

    public int CommandTimeout { get; init; } = 30;
}
