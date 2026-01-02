using Microsoft.Extensions.Logging;
using ServiceCatalogService.Api.Events.Tenant;
using ServiceCatalogService.Api.Services.Interfaces;
using ServiceCatalogService.Database.Entities;
using ServiceCatalogService.Database;

namespace ServiceCatalogService.Api.Services;

public class TenantEventService(
    ILogger<TenantEventService> logger,
    ServiceCatalogDbContext dbContext) : ITenantEventService
{
    public async Task HandleTenantCreatedEventAsync(TenantCreatedEvent tenantEvent)
    {
        logger.LogInformation("Handling tenant created event for tenant ID: {TenantId}", tenantEvent.TenantId);

        try
        {
            var existingTenant = await dbContext.Tenants.FindAsync(tenantEvent.TenantId);
            if (existingTenant != null)
            {
                logger.LogWarning("Tenant with ID {TenantId} already exists, skipping creation", tenantEvent.TenantId);
                return;
            }

            var tenant = new Tenant
            {
                Id = tenantEvent.TenantId,
                BusinessName = tenantEvent.BusinessName,
                Address = tenantEvent.Address ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Successfully created tenant {TenantId} in service catalog database", tenantEvent.TenantId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling tenant created event for tenant ID: {TenantId}", tenantEvent.TenantId);
            throw;
        }
    }

    public async Task HandleTenantUpdatedEventAsync(TenantUpdatedEvent tenantEvent)
    {
        logger.LogInformation("Handling tenant updated event for tenant ID: {TenantId}", tenantEvent.TenantId);

        try
        {
            var existingTenant = await dbContext.Tenants.FindAsync(tenantEvent.TenantId);
            if (existingTenant == null)
            {
                logger.LogWarning("Tenant with ID {TenantId} not found for update", tenantEvent.TenantId);
                return;
            }

            existingTenant.BusinessName = tenantEvent.BusinessName;
            existingTenant.Address = tenantEvent.Address ?? string.Empty;
            existingTenant.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();
            logger.LogInformation("Successfully updated tenant {TenantId} in service catalog database", tenantEvent.TenantId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling tenant updated event for tenant ID: {TenantId}", tenantEvent.TenantId);
            throw;
        }
    }
}
