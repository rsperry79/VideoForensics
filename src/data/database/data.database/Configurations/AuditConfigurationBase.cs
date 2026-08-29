using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>
    /// Marker base class for audit trail entity configurations.
    /// Ensures consistent configuration patterns across all audit trail entities (AccessAuditLog, ExportAuditRecord, etc).
    /// Each subclass implements IEntityTypeConfiguration for its specific audit entity type.
    /// </summary>
    public abstract class AuditConfigurationBase
    {
        /// <summary>Consistent property configuration for user/actor fields (256 char limit).</summary>
        protected const int ActorMaxLength = 256;

        /// <summary>Consistent property configuration for description/notes fields (2000 char limit).</summary>
        protected const int DescriptionMaxLength = 2000;

        /// <summary>Consistent property configuration for action/type fields (256 char limit).</summary>
        protected const int ActionMaxLength = 256;
    }
}
