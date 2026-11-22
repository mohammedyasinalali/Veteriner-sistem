namespace VetKlinik.Web.Domain;

public class AccessToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public required string Name { get; set; }

    // Anahtarın kendisi değil, yalnızca SHA-256 özeti (hex) saklanır.
    public required string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
