using ServiceCatalogService.Api.Events;

namespace ServiceCatalogService.Api.Services.Interfaces;

public interface IKafkaProducerService
{
    Task PublishServiceCatalogEventAsync(BaseEvent serviceCatalogEvent, CancellationToken cancellationToken = default);
}
