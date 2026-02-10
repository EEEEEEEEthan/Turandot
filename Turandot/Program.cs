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
sealed class Game
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
	static void PrintLine(object? message) { Console.WriteLine(message); }
	static void Print(object? message) { Console.Write(message); }
	static void PrintLineColored(ConsoleColor color, object? message)
	{
		var previousColor = Console.ForegroundColor;
		Console.ForegroundColor = color;
		Console.WriteLine(message);
		Console.ForegroundColor = previousColor;
	}
	static void PrintColored(ConsoleColor color, object? message)
	{
		var previousColor = Console.ForegroundColor;
		Console.ForegroundColor = color;
		Console.Write(message);
		Console.ForegroundColor = previousColor;
	}
	readonly List<Role> roles = [];
	readonly HashSet<string> dead = [];
	(string endpoint, string apiKey, string modelId) credentials;
	IEnumerable<Role> Alive => roles.Where(r => !dead.Contains(r.name));
	public async Task PlayAsync((string endpoint, string apiKey, string modelId) creds)
	{
		credentials = creds;
		roles.Add(new WolfRole("ethan"));
		roles.Add(new WolfRole("dove"));
		roles.Add(new PeasantRole("alice"));
		roles.Add(new PeasantRole("bob"));
		roles.Add(new PeasantRole("carol"));
		BroadcastMessage("[系统]游戏开始了,在场的玩家有ethan,dove,alice,bob,carol。其中2个狼人,3个村民");
		BroadcastMessage("[系统]天黑请闭眼");
		BroadcastMessage("[系统]狼人请睁眼");
		BroadcastMessage("[系统]狼人请确认身份");
		foreach(var role in roles)
			if(role is WolfRole)
				BroadcastMessage($"[系统]{role.name}是狼人", static ro => ro is WolfRole);
		BroadcastMessage("[系统]天亮了.请大家自我介绍");
		await SpeakRound("请自我介绍", Alive.OrderBy(static _ => Random.Shared.Next()).ToList());
		BroadcastMessage("[系统]天黑请闭眼");
		BroadcastMessage("[系统]狼人请杀人");
		var killed = await Kill();
		dead.Add(killed.name);
		BroadcastMessage($"[系统]天亮了,{killed.name}被杀死了.从死者下家开始发言.");
		var orderFromDeadNext = OrderFromNextTo(roles.FindIndex(r => r.name == killed.name));
		await SpeakRound("投票前的讨论.每个人只有一次发言机会", orderFromDeadNext);
		BroadcastMessage($"[系统]讨论完毕,现在开始投票.从死者下家开始投票");
		var executed = await VoteExecute(orderFromDeadNext);
		dead.Add(executed.name);
		BroadcastMessage($"[系统]投票结果：{executed.name}被处决。");
		var lastWordsMessages = executed
			.context
			.Concat([new(AuthorRole.User, "你被投票处决了，请发表遗言"),])
			.ToList();
		var lastWords = await LLM.SendAsync(credentials.endpoint, credentials.apiKey, credentials.modelId, lastWordsMessages);
		BroadcastMessage($"[{executed.name}]:{lastWords}");
	}
	async Task<Role> VoteExecute(List<Role> order)
	{
		var names = order.Select(static r => r.name).ToList();
		var nameList = string.Join("，", names);
		Dictionary<Role, string> choices = [];
		foreach(var r in order)
		{
			var role = r;
			const string toolName = "vote";
			var tool = new LLM.ToolSpec(
				toolName,
				"投票处决一名玩家",
				[new("目标", "要投票处决的玩家名字", typeof(string), true),],
				(payload, _) =>
				{
					var target = (string)payload["目标"]!;
					PrintLineColored(ConsoleColor.DarkGray, $"{role.name}投票给{target}");
					choices[role] = target;
					return Task.FromResult("已记录");
				});
			var messages = role
				.context
				.Concat([new(AuthorRole.User, $"白天讨论结束，请投票处决一人。可选：{nameList}。请调用{toolName}工具。"),])
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
				if(choices.ContainsKey(role)) break;
				messages.Add(new(AuthorRole.Assistant, reply));
				messages.Add(new(AuthorRole.User, "请调用vote工具，参数为要处决的玩家名字。"));
			}
		}
		var grouped = choices.Values.GroupBy(static x => x).OrderByDescending(static g => g.Count()).ToList();
		var maxCount = grouped.First().Count();
		var tied = grouped.Where(g => g.Count() == maxCount).Select(static g => g.Key).ToList();
		var chosen = tied.Count == 1? tied[0] : tied[new Random().Next(tied.Count)];
		return roles.First(r => r.name == chosen);
	}
	async Task<Role> Kill()
	{
		var wolves = roles.OfType<WolfRole>().Where(w => !dead.Contains(w.name)).ToList();
		var aliveNames = Alive.Select(r => r.name).ToList();
		var targetList = string.Join("，", aliveNames);
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
					"选择今晚要击杀的玩家，调用后狼人们会得知当前选择。只能选存活玩家。",
					[new("目标玩家", "要击杀的玩家名字", typeof(string), true),],
					(payload, _) =>
					{
						var target = (string)payload["目标玩家"]!;
						PrintLineColored(ConsoleColor.DarkGray, $"{wolf.name}选择了{target}");
						choices[wolf] = target;
						return Task.FromResult("已记录你的选择");
					});
				var messages = wolf
					.context
					.Concat(
					[
						new(
							AuthorRole.User,
							$"请选择今晚要击杀的目标玩家。可选（仅存活）：{targetList}。{otherLastRound}\n请调用选择击{toolName}工具，参数为你要击杀的玩家名字。"),
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
				return roles.First(r => r.name == targetName);
			}
			previousRoundChoices = new(choices);
			BroadcastMessage("狼人目标不一致，请重新选择", static r => r is WolfRole);
		}
	}
	/// <summary>从指定玩家下家开始的存活顺序（环状）</summary>
	List<Role> OrderFromNextTo(int index)
	{
		var alive = Alive.ToList();
		if(index < 0) return alive;
		var ordered = new List<Role>();
		for(var i = 1; i <= roles.Count; i++)
		{
			var r = roles[(index + i) % roles.Count];
			if(!dead.Contains(r.name)) ordered.Add(r);
		}
		return ordered;
	}
	async Task SpeakRound(string prompt, List<Role> order)
	{
		foreach(var role in order)
		{
			var messages = role
				.context
				.Concat([new(AuthorRole.User, prompt),])
				.ToList();
			var content = await LLM.SendAsync(credentials.endpoint, credentials.apiKey, credentials.modelId, messages);
			var msg = $"[{role.name}]{content}";
			BroadcastMessage(msg);
		}
	}
	void BroadcastMessage(string message, Func<Role, bool>? filter = null)
	{
		foreach(var role in roles)
		{
			if(filter?.Invoke(role) == false) continue;
			role.context.Add(new(AuthorRole.System, message));
		}
		if(filter is null)
			PrintLine(message);
		else
			PrintLineColored(ConsoleColor.DarkGray, message);
	}
}
