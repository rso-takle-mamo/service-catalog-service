using ServiceCatalogService.Api.Events.Tenant;

namespace ServiceCatalogService.Api.Services.Interfaces;

public interface ITenantEventService
{
    Task HandleTenantCreatedEventAsync(TenantCreatedEvent tenantEvent);
    Task HandleTenantUpdatedEventAsync(TenantUpdatedEvent tenantEvent);
}
