using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Turandot;
Console.WriteLine("正在验证api key...");
var apiKeyPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".apikey");
var credentials = await LLM.EnsureApiKey(apiKeyPath);
Console.WriteLine("*游戏开始了*");
var messages = new[]
{
    new ChatMessageContent(
        AuthorRole.System,
        """
        你是《图兰朵》的系统。玩家扮演的是流亡的鞑靼王子卡拉夫。现在卡拉夫来到了元大都（故事还没开始）。请你以卡拉夫的第一人称视角介绍卡拉夫。
        请注意以下几点：
        1. 200字以内
        2. 卡拉夫是从西向东流亡的
        3. 此时不要提及图兰朵，因为他们还不认识
        4. 不要表现得特意前往元大都
        """
    ),
    new ChatMessageContent(AuthorRole.User, "请开始。")
};
await foreach (var chunk in LLM.SendStreamingAsync(credentials, messages, temperature: 0.6f))
    Console.Write(chunk);
Console.WriteLine();
Console.WriteLine("Press any key to exit.");
Console.ReadKey(true);