using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using TicketBooking.Domain.Entities;
using TicketBooking.Domain.Interfaces;

namespace TicketBooking.Infra.Repositories;

public class DynamoDbTicketRepository : ITicketRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private const string TableName = "Tickets";

    public DynamoDbTicketRepository(IAmazonDynamoDB dynamoDb)
    {
        _dynamoDb = dynamoDb;
    }

    public async Task<bool> ReserveTicket(Ticket ticket)
    {
        var request = new UpdateItemRequest
        {
            TableName = "Tickets",
            Key = new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = $"EVENT#{ticket.EventId}" } },
                { "sk", new AttributeValue { S = $"TICKET#{ticket.TicketId}" } }
            },
            UpdateExpression = "SET #s = :reserved, #c2 = :v2, updatedAt = :now",
            ConditionExpression = "attribute_not_exists(#s) OR #s = :available",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                { "#s", "status" },
                { "#c2", "isVip" }
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":reserved", new AttributeValue { S = "Reserved" } },
                { ":available", new AttributeValue { S = "Available" } },
                { ":v2", new AttributeValue { BOOL = ticket.IsVip } },
                { ":now", new AttributeValue { S = DateTime.UtcNow.ToString("O") } }
            }
        };

        try
        {
            await _dynamoDb.UpdateItemAsync(request);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    public async Task<bool> ConfirmTicket(Ticket ticket)
    {
        var request = new UpdateItemRequest
        {
            TableName = "Tickets",
            Key = new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = $"EVENT#{ticket.EventId}" } },
                { "sk", new AttributeValue { S = $"TICKET#{ticket.TicketId}" } }
            },
            UpdateExpression = "SET #s = :confirmed, updatedAt = :now",
            ConditionExpression = "#s = :reserved",
            ExpressionAttributeNames = new Dictionary<string, string> { { "#s", "status" } },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":confirmed", new AttributeValue { S = "Confirmed" } },
                { ":reserved", new AttributeValue { S = "Reserved" } },
                { ":now", new AttributeValue { S = DateTime.UtcNow.ToString("O") } }
            }
        };

        try
        {
            await _dynamoDb.UpdateItemAsync(request);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    public async Task<List<Ticket>> GetTickets(string eventId)
    {
        var request = new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"EVENT#{eventId}" } }
            }
        };

        var response = await _dynamoDb.QueryAsync(request);
        var tickets = response.Items.Select(item =>
        {
            string GetS(string key) => item.TryGetValue(key, out var attr) ? attr.S : string.Empty;
            return Ticket.Create
            (
                eventId,
                int.TryParse(GetS("sk").Replace("TICKET#", ""), out var id) ? id : 0,
                item.TryGetValue("userId", out var userId) ? userId.S : "Unknown",
                item.TryGetValue("isVip", out var vipAttr) && (vipAttr.BOOL ?? false),
                Enum.TryParse(GetS("status"), out TicketStatus status) ? status : TicketStatus.Available,
                item.TryGetValue("updatedAt", out var dateAttr) &&
                DateTime.TryParse(dateAttr.S, out var date)
                    ? date
                    : default
            ).Value;
        }).ToList();

        return tickets;
    }

    public async Task<Ticket?> GetTicket(string eventId, int ticketId)
    {
        var request = new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = $"EVENT#{eventId}" } },
                { "sk", new AttributeValue { S = $"TICKET#{ticketId}" } }
            }
        };

        var response = await _dynamoDb.GetItemAsync(request);
        if (!response.IsItemSet) return null;

        var item = response.Item;
        var statusTxt = item.TryGetValue("status", out var st) ? st.S : "Available";

        return Ticket.Create
        (
             eventId,
             ticketId,
             item.TryGetValue("userId", out var userId) ? userId.S : "Unknown",
             item.TryGetValue("isVip", out var vipAttr) && (vipAttr.BOOL ?? false),
             Enum.TryParse(statusTxt, out TicketStatus status) ? status : TicketStatus.Available,
             item.TryGetValue("updatedAt", out var updated)
                ? DateTime.Parse(updated.S)
                : default
        ).Value;
    }
}
