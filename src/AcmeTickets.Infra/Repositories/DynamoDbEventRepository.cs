using AcmeTickets.Domain.Entities;
using AcmeTickets.Domain.Interfaces;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace AcmeTickets.Infra.Repositories;

public class DynamoDbEventRepository : IEventRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private const string TableName = "Events";

    public DynamoDbEventRepository(IAmazonDynamoDB dynamoDb)
    {
        _dynamoDb = dynamoDb;
    }

    public async Task<List<string>> GetEventIds()
    {
        var request = new ScanRequest
        {
            TableName = TableName,
            ProjectionExpression = "pk"
        };
        var response = await _dynamoDb.ScanAsync(request);

        var items = response.Items
            .Select(i => i["pk"].S.Replace("EVENT#", ""))
            .Distinct()
            .OrderBy(s => s)
            .ToList();
        return items;
    }

    public async Task<Event?> GetEvent(string eventId)
    {
        var request = new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = $"EVENT#{eventId}" } },
            }
        };

        var response = await _dynamoDb.GetItemAsync(request);
        if (!response.IsItemSet) return null;

        var item = response.Item;

        return Event.Create(eventId, int.Parse(item["totalTickets"].S)).Value;
    }

    public async Task<EventStats?> GetDashboardStats(string eventId)
    {
        var totalTask = QueryCountTotal(eventId);
        var reservedTask =  QueryCountByStatus(eventId, "Reserved");
        var confirmedTask =  QueryCountByStatus(eventId, "Confirmed");
        await Task.WhenAll(totalTask, reservedTask, confirmedTask);

        int total = totalTask.Result;
        int reserved = reservedTask.Result;
        int confirmed = confirmedTask.Result;

        return new EventStats
        (
            eventId,
            total,
            confirmed,
            reserved,
            total - confirmed - reserved
        );
    }

    public async Task<bool> CreateEvent(Event evt)
    {
        var row = new Dictionary<string, AttributeValue>
        {
            { "pk", new AttributeValue { S = $"EVENT#{evt.EventId}" } },
            { "eventId", new AttributeValue { S = evt.EventId } },
            { "totalTickets", new AttributeValue { S = evt.TotalTickets.ToString() } },
        };

        var response = await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = "Events",
            Item = row
        });

        return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
    }

    private async Task<int> QueryCountTotal(string eventId)
    {
        var request = new GetItemRequest
        {
            TableName = "Events",
            Key = new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = $"EVENT#{eventId}" } }
            }
        };
        var response = await _dynamoDb.GetItemAsync(request);
        if (!response.IsItemSet) return 0;

        var item = response.Item;
        if (!item.TryGetValue("totalTickets", out var attr))
            return 0;

        var literalValue = attr.N ?? attr.S;
        if (int.TryParse(literalValue, out int total))
            return total;
        return 0;
    }

    private async Task<int> QueryCountByStatus(string eventId, string status)
    {
        var countRequest = new QueryRequest
        {
            TableName = "Tickets",
            KeyConditionExpression = "pk = :pk",
            FilterExpression = "#s = :status",
            ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" } },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"EVENT#{eventId}" } },
                { ":status", new AttributeValue { S = status } }
            },
            Select = Select.COUNT
        };
        var countResponse = await _dynamoDb.QueryAsync(countRequest);
        return countResponse.Count ?? 0;
    }
}
