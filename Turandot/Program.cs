using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Turandot;
var apiKeyPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".apikey");
var (endpoint, apiKey, modelId) = await LLM.EnsureApiKey(apiKeyPath);
var opening = await LLM.SendAsync(
    endpoint,
    apiKey,
    modelId,
    [
        new ChatMessageContent(
            AuthorRole.System,
            "你是系统。请以系统身份用中文为《图兰朵》开场：玩家是卡拉夫王子，你要说一段开场白，要求简洁有舞台感。"
        ),
        new ChatMessageContent(AuthorRole.User, "请开始。")
    ],
    temperature: 0.6f);
Console.WriteLine(opening);
Console.WriteLine("Press any key to exit.");
Console.ReadKey(true);
