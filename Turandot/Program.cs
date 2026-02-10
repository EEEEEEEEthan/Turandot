using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Turandot;
Console.WriteLine("正在验证api key...");
var apiKeyPath = Path.Combine(
	Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
	".apikey");
var credentials = await LLM.EnsureApiKey(apiKeyPath);
var game = new Game();
await game.PlayAsync(credentials);
Console.WriteLine("按任意键退出");
Console.ReadKey(true);
class Game
{
	class Role(string name)
	{
		public readonly string name = name;
		public readonly List<ChatMessageContent> context =
		[
			new(AuthorRole.System, $"你叫{name},你们在玩狼人。"),
		];
	}
	sealed class WolfRole: Role
	{
		public WolfRole(string name): base(name) { context.Add(new(AuthorRole.System, $"你叫{name},你们在玩狼人,你抽到的角色是狼。")); }
	}
	sealed class PeasantRole: Role
	{
		public PeasantRole(string name): base(name) { context.Add(new(AuthorRole.System, $"你叫{name},你们在玩狼人,你抽到的角色是村民。")); }
	}
	readonly List<Role> roles = [];
	public async Task PlayAsync((string endpoint, string apiKey, string modelId) credentials)
	{
		roles.Add(new WolfRole("ethan"));
		roles.Add(new WolfRole("dove"));
		roles.Add(new PeasantRole("alice"));
		roles.Add(new PeasantRole("bob"));
		roles.Add(new PeasantRole("carol"));
		BroadcastMessage("游戏开始了,在场的玩家有ethan,dove,alice,bob,carol。其中2个狼人,3个村民");
		BroadcastMessage("天黑请闭眼");
		BroadcastMessage("狼人请睁眼");
		BroadcastMessage("狼人请杀人");
		_ = await Kill(credentials);
	}
	async Task<Role> Kill((string endpoint, string apiKey, string modelId) credentials)
	{
		var wolves = roles.OfType<WolfRole>().ToList();
		var villagers = roles.OfType<PeasantRole>().Select(static r => r.name).ToList();
		var villagerList = string.Join("，", villagers);
		Dictionary<WolfRole, string>? previousRoundChoices = null;
		while(true)
		{
			var choices = new Dictionary<WolfRole, string>();
			foreach(var w in wolves)
			{
				var wolf = w;
				var otherLastRound = previousRoundChoices is null
					? ""
					: "\n上一轮其他狼人的选择："
					+ string.Join(
						"，",
						wolves
							.Where(wo => wo != wolf)
							.Where(wo => previousRoundChoices.TryGetValue(wo, out _))
							.Select(wo => $"{wo.name}选了{previousRoundChoices[wo]}"));
				const string toolName = "select_target";
				var tool = new LLM.ToolSpec(
					toolName,
					"选择今晚要击杀的玩家，调用后狼人们会得知当前选择。",
					[new("目标玩家", "要击杀的玩家名字", typeof(string), true),],
					(payload, _) =>
					{
						var target = (string)payload["目标玩家"]!;
						choices[wolf] = target;
						return Task.FromResult("已记录你的选择");
					});
				var messages = wolf
					.context
					.Concat(
					[
						new(
							AuthorRole.User,
							$"请选择今晚要击杀的目标玩家。可选：{villagerList}。{otherLastRound}\n请调用选择击{toolName}工具，参数为你要击杀的玩家名字。"),
					])
					.ToList();
				while(true)
				{
					var reply = await LLM.SendAsync(
						credentials.endpoint,
						credentials.apiKey,
						credentials.modelId,
						messages,
						[tool,],
						0.6f);
					if(choices.ContainsKey(wolf)) break;
					messages.Add(new(AuthorRole.Assistant, reply));
					messages.Add(new(AuthorRole.User, "你必须调用选择击杀目标工具，参数为要击杀的玩家名字。"));
				}
			}
			if(choices.Values.Distinct().Count() == 1)
			{
				var targetName = choices.Values.First();
				BroadcastMessage("狼人一致选择击杀 " + targetName, static r => r is WolfRole);
				return roles.First(r => r.name == targetName);
			}
			previousRoundChoices = new(choices);
			BroadcastMessage("狼人目标不一致，请重新选择", static r => r is WolfRole);
		}
	}
	void BroadcastMessage(string message, Func<Role, bool>? filter = null)
	{
		foreach(var role in roles)
		{
			if(filter?.Invoke(role) == false) continue;
			role.context.Add(new(AuthorRole.System, message));
		}
		Console.WriteLine(message);
	}
}
