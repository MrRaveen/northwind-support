using System.ComponentModel.DataAnnotations;

namespace SupportDesk.Presentation.Security;

/// <summary>The "Jwt" configuration section. Validated at startup. The signing key is never in code.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 key. At least 32 characters. Comes from config or the Jwt__SigningKey env var.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int LifetimeMinutes { get; set; } = 60;
}