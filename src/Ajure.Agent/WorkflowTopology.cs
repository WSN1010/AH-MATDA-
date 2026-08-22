using System.Runtime.ExceptionServices;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Ajure.Agent;

public static class WorkflowTopology
{
    public static async Task<ModelResponse> RunAgentAsync(
        IModelGateway gateway,
        ModelRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(request);

        using var client = new ModelGatewayChatClient(gateway, request);
        var agent = new ChatClientAgent(
            client,
            instructions: request.Instructions,
            name: request.Role.ToString(),
            description: request.Role.DisplayName(),
            tools: []);
        await agent
            .RunAsync(request.Prompt, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return client.LastResponse
            ?? throw new InvalidOperationException($"{request.Role.DisplayName()} produced no response.");
    }

    public static async Task<IReadOnlyList<ModelResponse>> RunIndependentReviewAsync(
        IModelGateway gateway,
        IReadOnlyList<ModelRequest> requests,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
        {
            return [];
        }

        var clients = requests
            .Select(request => new ModelGatewayChatClient(gateway, request))
            .ToArray();
        var agents = requests
            .Select((request, index) => new ChatClientAgent(
                clients[index],
                instructions: request.Instructions,
                name: request.Role.ToString(),
                description: request.Role.DisplayName(),
                tools: []))
            .ToArray();

        try
        {
            var workflow = AgentWorkflowBuilder.BuildConcurrent(
                "ajure-independent-review",
                agents,
                aggregator: null);
            var input = new List<ChatMessage> { new(ChatRole.User, requests[0].Prompt) };
            await using var run = await InProcessExecution
                .RunStreamingAsync(workflow, input, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await run
                .TrySendMessageAsync(new TurnToken(emitEvents: false))
                .ConfigureAwait(false);

            Exception? failure = null;
            var completed = false;
            await foreach (var workflowEvent in run
                               .WatchStreamAsync(cancellationToken)
                               .ConfigureAwait(false))
            {
                switch (workflowEvent)
                {
                    case WorkflowOutputEvent:
                        completed = true;
                        break;
                    case WorkflowErrorEvent workflowError:
                        failure = workflowError.Exception;
                        break;
                    case ExecutorFailedEvent { Data: Exception exception }:
                        failure = exception;
                        break;
                }
            }

            if (failure is not null)
            {
                ExceptionDispatchInfo.Capture(failure.GetBaseException()).Throw();
            }

            if (!completed || clients.Any(static client => client.LastResponse is null))
            {
                throw new InvalidOperationException(
                    "The independent review workflow did not complete every reviewer.");
            }

            return clients.Select(static client => client.LastResponse!).ToArray();
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }
    }
}
