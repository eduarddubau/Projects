using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

public class User : IdentityUser<Guid>, IAuditEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// True once the user's personal data has been irreversibly scrubbed (GDPR erasure).
    /// The row is retained so audit foreign keys (CreatedBy/UpdatedBy) stay valid, but the
    /// account is hidden from the trash and can no longer be restored.
    /// </summary>
    public bool IsAnonymized { get; set; }

    /// <summary>
    /// When the user's data was erased — a write-once compliance timestamp for proving
    /// when a data-erasure (GDPR) request was fulfilled. Null until anonymized.
    /// </summary>
    public DateTime? AnonymizedAt { get; set; }

    [ForeignKey("CreatedBy")]
    public virtual User? Creator { get; set; }

    [ForeignKey("UpdatedBy")]
    public virtual User? Updater { get; set; }
}