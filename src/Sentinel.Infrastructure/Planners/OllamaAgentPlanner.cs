using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sentinel.Application.Abstractions;
using Sentinel.Domain.Enums;
using Sentinel.Domain.ValueObjects;

namespace Sentinel.Infrastructure.Planners;

public sealed class OllamaAgentPlanner : IAgentPlanner
{
    public const string HttpClientName = "Ollama";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AgentPlannerOptions _options;

    public OllamaAgentPlanner(
        IHttpClientFactory httpClientFactory,
        IOptions<AgentPlannerOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<ActionProposal> ProposeActionAsync(
        AgentPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        var prompt =
            $$"""
            You are Sentinel Orchestrator's planning agent. Propose one structured operational action as JSON with fields:
            actionName (string), targetEnvironment (string), riskTier (Low|Medium|High|Critical), payloadJson (string), rationale (string).
            Workflow: {{request.WorkflowName}}
            Step: {{request.StepName}} (Type={{request.StepType}}, DefaultRisk={{request.DefaultRiskTier}}, Env={{request.TargetEnvironment}})
            """;

        var payload = new
        {
            model = _options.Model,
            stream = false,
            format = "json",
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        using var response = await client.PostAsJsonAsync("/api/chat", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var chatResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Ollama returned an empty response.");

        var content = chatResponse.Message?.Content
            ?? throw new InvalidOperationException("Ollama chat response did not contain message content.");

        var parsed = JsonSerializer.Deserialize<OllamaProposalDto>(content, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Ollama proposal JSON.");

        var riskTier = Enum.TryParse<RiskTier>(parsed.RiskTier, ignoreCase: true, out var parsedRisk)
            ? parsedRisk
            : request.DefaultRiskTier;

        return new ActionProposal(
            ActionName: parsed.ActionName ?? string.Empty,
            TargetEnvironment: string.IsNullOrWhiteSpace(parsed.TargetEnvironment) ? request.TargetEnvironment : parsed.TargetEnvironment,
            RiskTier: riskTier,
            PayloadJson: string.IsNullOrWhiteSpace(parsed.PayloadJson) ? "{}" : parsed.PayloadJson,
            Rationale: parsed.Rationale ?? string.Empty);
    }

    private sealed record OllamaChatResponse(OllamaMessage? Message);
    private sealed record OllamaMessage(string? Content);
    private sealed record OllamaProposalDto(
        string? ActionName,
        string? TargetEnvironment,
        string? RiskTier,
        string? PayloadJson,
        string? Rationale);
}
