using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Turandot;
var apiKeyPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".apikey");
while (true)
{
    var (endpoint, apiKey, modelId) = await ReadOrPromptCredentialsAsync(apiKeyPath);
    try
    {
        var tools = new[]
        {
            new LLM.ToolSpec(
                "Ping",
                "Return Pong to verify tool calls.",
                [],
                (_, _) => Task.FromResult("Pong"))
        };
        var messages = new[]
        {
            new ChatMessageContent(AuthorRole.System, "You must call the Ping tool once and reply with its result only."),
            new ChatMessageContent(AuthorRole.User, "Test tool calling.")
        };
        var reply = await LLM.SendAsync(
            endpoint,
            apiKey,
            modelId,
            messages,
            tools,
            0.0f,
            CancellationToken.None);
        if (reply.Contains("Pong", StringComparison.OrdinalIgnoreCase)) break;
        Console.WriteLine($"Tool call failed. Response: {reply}");
        Console.WriteLine("Please re-enter URL and API key.");
        await PromptAndSaveAsync(apiKeyPath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Connection failed: {ex.Message}");
        Console.WriteLine("Please re-enter URL and API key.");
        await PromptAndSaveAsync(apiKeyPath);
    }
}
Console.WriteLine("Press any key to exit.");
Console.ReadKey(true);
return;
static async Task<(string endpoint, string apiKey, string modelId)> ReadOrPromptCredentialsAsync(string path)
{
    if (!File.Exists(path)) return await PromptAndSaveAsync(path);
    var lines = await File.ReadAllLinesAsync(path);
    if (lines.Length >= 3 &&
        !string.IsNullOrWhiteSpace(lines[0]) &&
        !string.IsNullOrWhiteSpace(lines[1]) &&
        !string.IsNullOrWhiteSpace(lines[2]))
        return (lines[0].Trim(), lines[1].Trim(), lines[2].Trim());
    return await PromptAndSaveAsync(path);
}
static async Task<(string endpoint, string apiKey, string modelId)> PromptAndSaveAsync(string path)
{
    Console.Write("URL: ");
    var endpoint = (Console.ReadLine() ?? string.Empty).Trim();
    Console.Write("API Key: ");
    var apiKey = (Console.ReadLine() ?? string.Empty).Trim();
    Console.Write("Model: ");
    var modelId = (Console.ReadLine() ?? string.Empty).Trim();
    await File.WriteAllLinesAsync(path, [endpoint, apiKey, modelId]);
    return (endpoint, apiKey, modelId);
}
