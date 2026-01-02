using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceCatalogService.Api.Configuration;
using ServiceCatalogService.Api.Events.Tenant;
using ServiceCatalogService.Api.Services.Interfaces;

namespace ServiceCatalogService.Api.Services;

public class KafkaConsumerService(
    ILogger<KafkaConsumerService> logger,
    IServiceProvider serviceProvider,
    IOptions<KafkaSettings> kafkaSettings)
    : BackgroundService
{
    private readonly KafkaSettings _kafkaSettings = kafkaSettings.Value;

    private ConsumerConfig CreateTenantConsumerConfig()
    {
        return new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = $"{_kafkaSettings.ConsumerGroupId}-tenant-events",
            AutoOffsetReset = KafkaConstants.ParseAutoOffsetReset(_kafkaSettings.AutoOffsetReset),
            EnableAutoCommit = _kafkaSettings.EnableAutoCommit
        };
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Kafka Consumer Service starting...");

        _ = Task.Run(async () =>
        {
            try
            {
                await ConsumeTenantEventsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Tenant events consumer failed unexpectedly.");
            }
        }, cancellationToken);

        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private async Task ConsumeTenantEventsAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting tenant events consumer for topic: {Topic}", _kafkaSettings.TenantEventsTopic);

        var consumerConfig = CreateTenantConsumerConfig();

        IConsumer<Ignore, string>? consumer = null;
        var retryCount = 0;
        var retryDelayMs = 2000;

        while (consumer == null && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
                consumer.Subscribe(_kafkaSettings.TenantEventsTopic);
                logger.LogInformation("Successfully connected to Kafka and subscribed to topic: {Topic}", _kafkaSettings.TenantEventsTopic);
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                logger.LogWarning(ex, "Failed to initialize Kafka consumer (attempt {RetryCount}). Retrying in {Delay}ms...", retryCount, retryDelayMs);
                consumer?.Dispose();
                consumer = null;
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(retryDelayMs, cancellationToken);
                }
            }
        }

        if (consumer == null)
        {
            logger.LogError("Failed to initialize Kafka consumer after cancellation. Stopping consumer.");
            return;
        }

        try
        {
            using (consumer)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var consumeResult = consumer.Consume(cancellationToken);
                    if (consumeResult.Message is null) continue;

                    logger.LogInformation("Received tenant event: {EventType}", consumeResult.Message.Key);

                    try
                    {
                        var jsonNode = JsonNode.Parse(consumeResult.Message.Value);
                        var eventType = jsonNode?["eventType"]?.GetValue<string>();
                        if (string.IsNullOrEmpty(eventType))
                        {
                            logger.LogWarning("Event missing eventType field");
                            continue;
                        }

                        await ProcessTenantEventAsync(eventType, consumeResult.Message.Value);
                        consumer.Commit(consumeResult);
                    }
                    catch (JsonException jsonEx)
                    {
                        logger.LogError(jsonEx, "Failed to parse tenant event JSON: {Message}", consumeResult.Message.Value);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing tenant event: {EventType}", consumeResult.Message.Key);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Tenant events consumer stopped due to cancellation");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tenant events consumer error");
        }
    }

    private async Task ProcessTenantEventAsync(string eventType, string eventJson)
    {
        using var scope = serviceProvider.CreateScope();
        var tenantEventService = scope.ServiceProvider.GetRequiredService<ITenantEventService>();

        try
        {
            switch (eventType)
            {
                case nameof(TenantCreatedEvent):
                    var tenantCreatedEvent = JsonSerializer.Deserialize<TenantCreatedEvent>(eventJson, KafkaConstants.JsonSerializerOptions);
                    if (tenantCreatedEvent != null)
                    {
                        logger.LogInformation("Processing TenantCreatedEvent for tenant {TenantId}", tenantCreatedEvent.TenantId);
                        await tenantEventService.HandleTenantCreatedEventAsync(tenantCreatedEvent);
                    }
                    break;

                case nameof(TenantUpdatedEvent):
                    var tenantUpdatedEvent = JsonSerializer.Deserialize<TenantUpdatedEvent>(eventJson, KafkaConstants.JsonSerializerOptions);
                    if (tenantUpdatedEvent != null)
                    {
                        logger.LogInformation("Processing TenantUpdatedEvent for tenant {TenantId}", tenantUpdatedEvent.TenantId);
                        await tenantEventService.HandleTenantUpdatedEventAsync(tenantUpdatedEvent);
                    }
                    break;

                default:
                    logger.LogWarning("Unknown tenant event type: {EventType}", eventType);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing tenant event {EventType}", eventType);
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Kafka Consumer Service stopping...");
        await base.StopAsync(cancellationToken);
    }
}
