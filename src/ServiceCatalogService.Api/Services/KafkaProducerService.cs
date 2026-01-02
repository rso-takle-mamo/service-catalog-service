using Confluent.Kafka;
using Microsoft.Extensions.Options;
using System.Text.Json;
using ServiceCatalogService.Api.Configuration;
using ServiceCatalogService.Api.Events;
using ServiceCatalogService.Api.Services.Interfaces;

namespace ServiceCatalogService.Api.Services;

public class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly KafkaSettings _kafkaSettings;
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(
        IOptions<KafkaSettings> kafkaSettings,
        ILogger<KafkaProducerService> logger)
    {
        _kafkaSettings = kafkaSettings.Value;
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            ClientId = _kafkaSettings.ClientId,
            Acks = KafkaConstants.ParseAcks(_kafkaSettings.Acks),
            EnableIdempotence = _kafkaSettings.EnableIdempotence,
            MessageTimeoutMs = _kafkaSettings.MessageTimeoutMs,
            RequestTimeoutMs = _kafkaSettings.RequestTimeoutMs
        };

        _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
    }

    public async Task PublishServiceCatalogEventAsync(BaseEvent serviceCatalogEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new Message<Null, string>
            {
                Value = JsonSerializer.Serialize(serviceCatalogEvent, serviceCatalogEvent.GetType(), KafkaConstants.JsonSerializerOptions)
            };

            var deliveryResult = await _producer.ProduceAsync(
                _kafkaSettings.ServiceCatalogEventsTopic,
                message,
                cancellationToken);

            var entityId = serviceCatalogEvent switch
            {
                Events.Service.ServiceEvent serviceEvt => serviceEvt.ServiceId.ToString(),
                Events.Category.CategoryEvent categoryEvt => categoryEvt.CategoryId.ToString(),
                _ => "N/A"
            };

            _logger.LogInformation(
                "Service catalog event {EventType} published to topic {Topic} partition {Partition} at offset {Offset} [EventId: {EventId}, EntityId: {EntityId}]",
                serviceCatalogEvent.EventType,
                _kafkaSettings.ServiceCatalogEventsTopic,
                deliveryResult.Partition,
                deliveryResult.Offset,
                serviceCatalogEvent.EventId,
                entityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish service catalog event {EventType} [EventId: {EventId}]", serviceCatalogEvent.EventType, serviceCatalogEvent.EventId);
            throw;  // Fail fast - bubble up to caller
        }
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}
