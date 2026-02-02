using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Turandot;
Console.WriteLine("正在验证api key...");
var apiKeyPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".apikey");
var credentials = await LLM.EnsureApiKey(apiKeyPath);
Console.WriteLine("*游戏开始了*");
var history = new List<ChatMessageContent>
{
    new(AuthorRole.System,
        """
        你是《图兰朵》的系统。玩家扮演的是流亡的鞑靼王子卡拉夫。请你引导玩家进行游戏。
        图兰朵是元朝公主。她的姐姐被异族所杀，图兰朵为了报仇，决定杀尽天下所有异族男子。
        图兰朵设下三个谜题，谁能答出谜题，谁就能娶她为妻。答不出谜题的男子将被斩首。
        """)
};
{
    var enumerable = LLM.SendStreamingAsync(credentials, new List<ChatMessageContent>(history)
    {
        new(
            AuthorRole.Assistant,
            """
            现在卡拉夫来到了元大都（故事还没开始）。请你以卡拉夫的第一人称视角介绍卡拉夫。
            请注意以下几点：
            1. 200字以内
            2. 卡拉夫是从西向东流亡的
            3. 此时不要提及图兰朵，因为他们还不认识
            4. 不要表现得特意前往元大都
            """
        ),
        new(AuthorRole.User, "请开始。")
    }, temperature: 0.6f);
    await foreach (var chunk in enumerable)
        Console.Write(chunk);
    Console.WriteLine();
    Console.ReadKey(true);
}
{
    var enumerable = LLM.SendStreamingAsync(credentials, [
        new ChatMessageContent(
            AuthorRole.Assistant,
            """
            玩家进入了元大都，看到很多人在围观波斯王子被斩首。
            此时玩家孤身一人。
            波斯王子是图兰朵的求婚者之一。
            """
        ),
        new ChatMessageContent(
            AuthorRole.User,
            "我进入元大都，看到很多人在围观什么。是波斯王子在被斩首。请你用选项引导我接下来的行动。但是不要偏离图兰朵的剧情。"
        ),
    ], temperature: 0.6f);
    await foreach (var chunk in enumerable)
        Console.Write(chunk);
    Console.ReadKey(true);
}
Console.WriteLine();
Console.WriteLine("Press any key to exit.");
Console.ReadKey(true);