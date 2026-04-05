using System.Text.Json;
using AcmeTickets.Domain.Common;
using AcmeTickets.Domain.Entities;
using AcmeTickets.Domain.Interfaces;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

namespace AcmeTickets.Infra.Adapters;

public class Workflows : IWorkflows
{
    private readonly IAmazonStepFunctions _stepFunctions;
    private readonly string _ticketArn;

    public Workflows(IAmazonStepFunctions stepFunctions, string ticketArn)
    {
        _stepFunctions = stepFunctions;
        _ticketArn = ticketArn;
    }

    public async Task<object> StartReservationFlow(Ticket ticket) //, ILogger logger
    {
        var startRequest = new StartExecutionRequest
        {
            StateMachineArn = _ticketArn,
            Input = JsonSerializer.Serialize(new
            {
                Pk = $"EVENT#{ticket.EventId}",
                Sk = $"TICKET#{ticket.TicketId}",
                status = "Canceled"
            }, JsonDefaults.Options) ?? "{}"
        };
        //logger.LogDebug("StartReservationFlow {arn}", startRequest.StateMachineArn);
        return await _stepFunctions.StartExecutionAsync(startRequest);
    }

}
