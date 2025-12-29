using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceCatalogService.Database.Entities;

namespace ServiceCatalogService.Database.Configurations;

public class ServiceConfiguration: IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services");
  
        builder.HasKey(s => s.Id);
        
        builder.Property(t => t.Id)
            .HasDefaultValueSql("gen_random_uuid()");
        
        builder.Property(s => s.Name).IsRequired().HasMaxLength(255);
  
        builder.Property(s => s.Description).HasMaxLength(1000);

        builder.Property(s => s.TenantId).IsRequired();
        
        builder.Property(s => s.Price).HasPrecision(10, 2);
        
        builder.Property(s => s.DurationMinutes).HasDefaultValue(30);
        
        builder.Property(s => s.IsActive).HasDefaultValue(true);
        
        builder.Property(s => s.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .ValueGeneratedOnAdd();

        builder.Property(s => s.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .ValueGeneratedOnAdd();
        
        // Relations
        builder.HasOne(s => s.Category)
            .WithMany()
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.Tenant)
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Constraints
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Service_Positive_Price",
            "\"Price\" >= 0"
        ));
            
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Service_Positive_Duration",
            "\"DurationMinutes\" > 0"
        ));

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Service_Reasonable_Duration",
            "\"DurationMinutes\" <= 480"
        ));
    }
}