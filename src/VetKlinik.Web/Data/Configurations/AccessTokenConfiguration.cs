using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Data.Configurations;

public class AccessTokenConfiguration : IEntityTypeConfiguration<AccessToken>
{
    public void Configure(EntityTypeBuilder<AccessToken> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(100);
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsFixedLength().IsUnicode(false);
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
