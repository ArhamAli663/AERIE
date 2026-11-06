namespace AERIE.Domain.Common;

/// <summary>
/// Base entity for all domain objects. Every persisted entity has an Id, creation/update audit timestamps,
/// and an optional soft-delete flag.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
