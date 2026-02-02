using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;
namespace Turandot;
public static class LLM
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed record ToolSpec(
        string Name,
        string Description,
        IReadOnlyList<ParameterSpec> Parameters,
        Func<IDictionary<string, object?>, CancellationToken, Task<string>> Handler);

    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed record ParameterSpec(
        string Name,
        string Description,
        Type Type,
        bool IsRequired);
    private static Kernel BuildKernel(
        string endpoint,
        string apiKey,
        string modelId,
        IEnumerable<ToolSpec>? tools)
    {
        var builder = Kernel.CreateBuilder();
        var client = CreateOpenAiClient(endpoint, apiKey);
        builder.AddOpenAIChatCompletion(modelId, client);
        var kernel = builder.Build();
        if (tools is null) return kernel;
        var plugin = KernelPluginFactory.CreateFromFunctions(nameof(LLM), tools.Select(CreateFunction));
        kernel.Plugins.Add(plugin);
        return kernel;
    }
    private static KernelFunction CreateFunction(ToolSpec tool)
    {
        var parameterNames = tool.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        return KernelFunctionFactory.CreateFromMethod(
            async (KernelArguments arguments, CancellationToken cancellationToken) =>
            {
                var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var parameter in tool.Parameters)
                {
                    if (arguments.TryGetValue(parameter.Name, out var value))
                    {
                        payload[parameter.Name] = value;
                        continue;
                    }
                    if (parameter.IsRequired) throw new ArgumentException($"Missing required argument: {parameter.Name}", parameter.Name);
                    payload[parameter.Name] = null;
                }
                foreach (var extra in arguments.Where(pair => !parameterNames.Contains(pair.Key))) payload[extra.Key] = extra.Value;
                return await tool.Handler(payload, cancellationToken);
            },
            tool.Name,
            tool.Description,
            tool.Parameters.Select(p => new KernelParameterMetadata(p.Name)
            {
                Description = p.Description,
                ParameterType = p.Type,
                IsRequired = p.IsRequired
            }).ToArray());
    }
    private static OpenAIClient CreateOpenAiClient(string endpoint, string apiKey)
    {
        var options = new OpenAIClientOptions();
        var normalizedEndpoint = NormalizeEndpoint(endpoint);
        if (!string.IsNullOrWhiteSpace(normalizedEndpoint)) options.Endpoint = new Uri(normalizedEndpoint);
        var credential = new ApiKeyCredential(apiKey);
        return new OpenAIClient(credential, options);
    }
    private static string NormalizeEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return endpoint;
        var trimmed = endpoint.Trim().TrimEnd('/');
        var suffixes = new[]
        {
            "/chat/completions",
            "/v1/chat/completions"
        };
        foreach (var suffix in suffixes)
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return trimmed[..^suffix.Length];
        return trimmed;
    }
    public static async IAsyncEnumerable<string> SendStreamingAsync(
        string endpoint,
        string apiKey,
        string modelId,
        IEnumerable<ChatMessageContent> messages,
        IEnumerable<ToolSpec>? tools = null,
        float temperature = 0.7f,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var kernel = BuildKernel(endpoint, apiKey, modelId, tools);
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory(messages);
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = temperature
        };
        if (tools is not null) settings.ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions;
        await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(
                           history,
                           settings,
                           kernel,
                           cancellationToken))
            if (!string.IsNullOrEmpty(chunk.Content))
                yield return chunk.Content;
    }
    public static async Task<string> SendAsync(
        string endpoint,
        string apiKey,
        string modelId,
        IEnumerable<ChatMessageContent> messages,
        IEnumerable<ToolSpec>? tools = null,
        float temperature = 0.7f,
        CancellationToken cancellationToken = default)
    {
        var kernel = BuildKernel(endpoint, apiKey, modelId, tools);
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory(messages);
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = temperature
        };
        if (tools is not null) settings.ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions;
        var result = await chat.GetChatMessageContentAsync(
            history,
            settings,
            kernel,
            cancellationToken);
        return result.Content ?? string.Empty;
    }
}
