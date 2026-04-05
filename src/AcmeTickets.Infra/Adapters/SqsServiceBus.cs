using System.Text.Json;
using System.Text.Json.Serialization;
using AcmeTickets.Domain.Common;
using AcmeTickets.Domain.Interfaces;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;

namespace AcmeTickets.Infra.Adapters;

public class SqsServiceBus : IServiceBus
{
    private readonly IAmazonSQS _sqsClient;
    public string QueueUrl { get; }


    public SqsServiceBus(IAmazonSQS sqsClient, string queueUrl)
    {
        _sqsClient = sqsClient;
        QueueUrl = queueUrl;
    }

    public async Task Publish<T>(T message, CancellationToken ct = default) where T : class
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        var json = JsonSerializer.Serialize(message, options);

        var request = new SendMessageRequest
        {
            QueueUrl = QueueUrl,
            MessageBody = json,
        };
        await _sqsClient.SendMessageAsync(request, ct);
    }

    public async Task Subscribe<T>(Func<T, CancellationToken, Task<bool>> handler, ILogger logger, CancellationToken cancelToken)
        where T : class
    {
        while (!cancelToken.IsCancellationRequested)
        {
            try
            {
                var request = new ReceiveMessageRequest { QueueUrl = QueueUrl, WaitTimeSeconds = 20, MaxNumberOfMessages = 5 };
                var response = await _sqsClient.ReceiveMessageAsync(request, cancelToken);

                if (response?.Messages is not { Count: > 0 })
                    continue;

                foreach (var message in response.Messages)
                {
                    try
                    {
                        var dto = JsonSerializer.Deserialize<T>(message.Body, JsonDefaults.Options);
                        if (dto == null)
                            continue;

                        if (await handler(dto, cancelToken))
                        {
                            await _sqsClient.DeleteMessageAsync(QueueUrl, message.ReceiptHandle, cancelToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing message {msg}", message.Body);
                        throw;
                    }
                }
            }
            catch (AmazonSQSException ex)
            {
                logger.LogWarning("Error connecting to SQS: {Msg}", ex.Message);
                await Task.Delay(5000, cancelToken);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Fatal error consuming messages: {msg}", ex.Message);
                await Task.Delay(1000, cancelToken);
            }
        }
    }
}
