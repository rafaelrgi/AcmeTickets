using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions.Model;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using TicketBooking.Api.Hubs;
using TicketBooking.Api.Workers;
using TicketBooking.Application.Dtos;
using TicketBooking.Domain.Entities;
using TicketBooking.Domain.Interfaces;
using TicketBooking.Domain.Settings;
using TicketBooking.Infra.Caching;
using System.Text.Json;
using TicketBooking.Domain.Common;
using TicketBooking.Infra.Adapters;

public class TicketWorkflowTests : IClassFixture<LocalStackFixture>
{
    private readonly LocalStackFixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public TicketWorkflowTests(LocalStackFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(new XUnit3Sink())
            .CreateLogger();
        _loggerFactory = new LoggerFactory().AddSerilog(serilogLogger);
    }

    private class XUnit3Sink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage();
            TestContext.Current.SendDiagnosticMessage($"[{logEvent.Level}] {message}");
        }
    }

    [Fact]
    public Task DummyTest_ToCheckSetup()
    {
        var test = true;
        test.Should().Be(!false);
        return Task.CompletedTask;
    }

    [Fact]
    public void ServiceBusMessages_ShouldGetTranslatedCorrectly()
    {
        // Arrange
        const string json1 = "{\"message\":\"Ticket\",\"event\": \"EVENT#Lok In Rio\",\"ticket\": \"TICKET#2\",\"status\": \"Reserved\"}";
        const string json2 = "{\"message\":\"Ticket\",\"event\": \"EVENT#Loky In Rio\",\"ticket\": \"TICKET#32\",\"status\": \"Confirmed\"}";
        const string json3 = "{\"message\":\"Ticket\",\"event\": \"EVENT#Lóke In Rio\",\"ticket\": \"TICKET#64\",\"status\": \"Available\"}";
        const string json4 = "{\"message\":\"Event\",\"event\": \"EVENT#Loki In Rio\"}";

        // Act
        var msg1 = TicketUpdateWorker.CleanMessage(JsonSerializer.Deserialize<BusMessageDto>(json1, JsonDefaults.Options)!);
        var msg2 = TicketUpdateWorker.CleanMessage(JsonSerializer.Deserialize<BusMessageDto>(json2, JsonDefaults.Options)!);
        var msg3 = TicketUpdateWorker.CleanMessage(JsonSerializer.Deserialize<BusMessageDto>(json3, JsonDefaults.Options)!);
        var msg4 = TicketUpdateWorker.CleanMessage(JsonSerializer.Deserialize<BusMessageDto>(json4, JsonDefaults.Options)!);

        // Assert
        msg1.Should().NotBeNull();
        msg1.Event.Should().Be("Lok In Rio");
        msg1.Ticket.Should().Be("2");
        msg1.Status.Should().Be("Reserved");
        msg2.Should().NotBeNull();
        msg2.Event.Should().Be("Loky In Rio");
        msg2.Ticket.Should().Be("32");
        msg2.Status.Should().Be("Confirmed");
        msg3.Should().NotBeNull();
        msg3.Event.Should().Be("Lóke In Rio");
        msg3.Ticket.Should().Be("64");
        msg3.Status.Should().Be("Available");
        msg4.Should().NotBeNull();
        msg4.Event.Should().Be("Loki In Rio");

    }

    /*
    [Theory]
    [InlineData("{\"pk\": \"EVENT#rock-in-rio\", \"sk\": \"TICKET#256\"}", 256)]
    [InlineData("{\"pk\": \"EVENT#woodstock\", \"sk\": \"TICKET#666\"}", 666)]
    [InlineData("{\"pk\": \"EVENT#rock-n-roll-circus\", \"sk\": \"TICKET#\"}", 0)]
    public void Worker_ShouldExtractCorrectTicketId(string jsonInput, int expectedEventId)
    {
        // Act
        var result = TicketUpdateWorker.GetTicketIdFromJson(jsonInput);

        // Assert
        result.Should().Be(expectedEventId);
    }
    */

    [Fact]
    public async Task WhenReservationExpires_ShouldCancel()
    {
        // Arrange
        const string pk = "EVENT#rock-grande-do=sul";
        const string sk = "TICKET#99";

        await ResetDatabase();
        await _fixture.DynamoDb.PutItemAsync("Tickets", new Dictionary<string, AttributeValue>
        {
            { "pk", new AttributeValue(pk) },
            { "sk", new AttributeValue(sk) },
            { "status", new AttributeValue("Reserved") }
        }, TestContext.Current.CancellationToken);

        // Act
        await _fixture.StepFunctions.StartExecutionAsync(new StartExecutionRequest
        {
            StateMachineArn = _fixture.StateMachineArn,
            Input = "{\"pk\": \"EVENT#rock-grande-do=sul\", \"sk\": \"TICKET#99\"}"
        }, TestContext.Current.CancellationToken);

        // Assert
        await Task.Delay(2000, TestContext.Current.CancellationToken);

        var result = await _fixture.DynamoDb.GetItemAsync("Tickets", new Dictionary<string, AttributeValue>
        {
            { "pk", new AttributeValue(pk) },
            { "sk", new AttributeValue(sk) }
        }, TestContext.Current.CancellationToken);

        var queueUrl = await _fixture.Sqs.GetQueueUrlAsync("TicketUpdatesQueue", TestContext.Current.CancellationToken);
        var messages = await _fixture.Sqs.ReceiveMessageAsync(queueUrl.QueueUrl, TestContext.Current.CancellationToken);

        result.Item["Status"].S.Should().Be("Cancelled");
        messages.Messages.Should().NotBeEmpty();
        messages.Messages[0].Body.Should().Contain("Cancelled");
    }

    [Fact]
    public async Task ProcessMessage_WhenSuccessful_ShouldNotifyFrontend()
    {
        // Arrange
        const string eventId = "rock-in-rio-1985";
        const int ticketId = 16;
        string json = $"{{\"event\": \"EVENT#{eventId}\", \"ticket\": \"{ticketId}\",\"status\": \"Reserved\"}}";
        var message = JsonSerializer.Deserialize<BusMessageDto>(json, JsonDefaults.Options);
        message.Should().NotBeNull();

        await ResetDatabase();
        var mockClientProxy = new Mock<IClientProxy>();

        var mockClients = new Mock<IHubClients>();
        mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);

        var mockHubContext = new Mock<IHubContext<TicketHub>>();
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        var mockRepo = new Mock<ITicketRepository>();
        mockRepo.Setup(r => r.GetTicket(eventId, ticketId))
            .ReturnsAsync(Ticket.Create(eventId, ticketId, "T-800", true, TicketStatus.Reserved).Value);

        var mockSqs = new Mock<IAmazonSQS>();
        var worker = CreateTicketUpdateWorker(mockRepo, mockSqs, mockHubContext);

        // Act
        await worker.ProcessMessage(TicketUpdateWorker.CleanMessage(message), TestContext.Current.CancellationToken);

        // Assert
        // Check the "Ticket" notification
        mockClientProxy.Verify(
            client => client.SendCoreAsync(
                "Ticket",
                It.Is<object[]>(args => args[0].ToString() == eventId),
                It.IsAny<CancellationToken>()
                ),
            Times.Once);
    }

    [Fact]
    public async Task ServiceBusSubscribe_WhenHandlerReturnsTrue_ShouldDeleteMessage()
    {
        // Arrange
        var mockSqs = new Mock<IAmazonSQS>();
        var handleMock = "receipt-handle-123";
        var queueUrl = "http://localhost:4566/queue";

        var response = new ReceiveMessageResponse
        {
            Messages = new List<Message>
            {
                new Message { Body = "{}", ReceiptHandle = handleMock }
            }
        };

        mockSqs.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var subscriber = new SqsServiceBus(mockSqs.Object, queueUrl);
        var cts = new CancellationTokenSource();

        // Act
        var workerLogger = _loggerFactory.CreateLogger<TicketUpdateWorker>();
        await subscriber.Subscribe<BusMessageDto>(async (msg, ct) =>
        {
            await cts.CancelAsync();
            return true;
        }, workerLogger, cts.Token);

        // Assert
        mockSqs.Verify(x => x.DeleteMessageAsync(
                queueUrl,
                handleMock,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private TicketUpdateWorker CreateTicketUpdateWorker(Mock<ITicketRepository> mockRepo, Mock<IAmazonSQS> mockSqs,
        Mock<IHubContext<TicketHub>> mockHubContext)
    {
        var opts = Options.Create(new MemoryDistributedCacheOptions());
        IDistributedCache cache = new MemoryDistributedCache(opts);

        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider
            .Setup(x => x.GetService(typeof(ITicketRepository)))
            .Returns(mockRepo.Object);
        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(mockScope.Object);

        const string ticketUpdatesQueue = "http://localhost:4566/000000000000/TicketUpdatesQueue";
        var workerLogger = _loggerFactory.CreateLogger<TicketUpdateWorker>();
        var cacheLogger = _loggerFactory.CreateLogger<TicketCacheService>();
        IServiceBus serviceBus = new SqsServiceBus(mockSqs.Object, ticketUpdatesQueue);

        var worker = new TicketUpdateWorker(serviceBus, mockHubContext.Object,
            new TicketCacheService(cache, cacheLogger), new EventCacheService(cache, cacheLogger), mockScopeFactory.Object, workerLogger);
        return worker;
    }

    private async Task ResetDatabase()
    {
        try
        {
            await _fixture.DynamoDb.DeleteTableAsync("Tickets");
        }
        catch
        {
        }

        await _fixture.DynamoDb.CreateTableAsync(new CreateTableRequest
        {
            TableName = "Tickets",
            AttributeDefinitions = [new("pk", ScalarAttributeType.S), new("sk", ScalarAttributeType.S)],
            KeySchema = [new("pk", KeyType.HASH), new("sk", KeyType.RANGE)],
            ProvisionedThroughput = new ProvisionedThroughput(5, 5)
        });
    }
}
