using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Turandot;
Console.WriteLine("正在验证api key...");
var apiKeyPath = Path.Combine(
	Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
	".apikey");
var credentials = await LLM.EnsureApiKey(apiKeyPath);
var game = new Game(credentials);
await game.PlayAsync();
Console.WriteLine("按任意键退出");
Console.ReadKey(true);
sealed class Game
{
	enum RoleType
	{
		Holder,
		Wolf,
		Villager,
	}
	sealed class Player(string name)
	{
		public readonly string name = name;
	}
	sealed class Role
	{
		public readonly RoleType roleType;
		public readonly Player player;
		public bool dead;
		readonly List<ChatMessageContent> context;
		readonly Game game;
		public string Name => player.name;
		public Role(Game game, RoleType roleType, Player player)
		{
			this.game = game;
			this.player = player;
			this.roleType = roleType;
			var roleText = roleType switch
			{
				RoleType.Holder => "主持人",
				RoleType.Wolf => "狼人",
				RoleType.Villager => "村民",
				_ => throw new ArgumentOutOfRangeException(nameof(roleType), roleType, null),
			};
			context =
			[
				new(AuthorRole.System, $"你是{Name},你们在玩狼人。你的身份是{roleText}"),
			];
		}
		public void Say(string message)
		{
			message = $"[{Name}]{message}";
			foreach(var role in game.roles)
				role.context.Add(new(AuthorRole.Assistant, message));
			Console.WriteLine(message);
		}
		public void Notify(string message)
		{
			message = $"[system]{message}";
			context.Add(new(AuthorRole.System, message));
			using(new ConsoleColorScope(ConsoleColor.DarkGray))
				Console.WriteLine(message);
		}
		public Task<string> Prompt(string prompt)
		{
			using(new ConsoleColorScope(ConsoleColor.DarkGray)) Console.WriteLine($"[{Name}]{prompt}\n(`[名字]`是系统帮添加的,发言内容请不要附带`[{Name}]`)");
			var copied = new List<ChatMessageContent>(context) {new(AuthorRole.User, prompt),};
			return LLM.SendAsync(game.credentials, copied);
		}
		public async Task<Role> Select(string prompt, List<Role> options)
		{
			var availableOptions = string.Join("，", options.Select(static r => r.Name));
			using(new ConsoleColorScope(ConsoleColor.DarkGray)) Console.WriteLine($"[{Name}]{prompt}({availableOptions})");
			const string toolName = "select_target";
			Role? target = null;
			while(target is null)
			{
				var tool = new LLM.ToolSpec(
					toolName,
					"选择一个玩家",
					[new("target", $"玩家名字,{availableOptions}中的一个", typeof(string), true),],
					(payload, _) =>
					{
						var t = (string)payload["target"]!;
						target = options.FirstOrDefault(r => r.Name == t);
						if(target is null) return Task.FromResult($"无效的选择。请选择{availableOptions}");
						return Task.FromResult("已记录你的选择");
					});
				var copied = new List<ChatMessageContent>(context) {new(AuthorRole.User, prompt),};
				_ = await LLM.SendAsync(game.credentials, copied, [tool,], 0.6f);
			}
			return target;
		}
	}
	readonly List<Role> roles = [];
	readonly Role holder;
	readonly (string endpoint, string apiKey, string modelId) credentials;
	public Game((string endpoint, string apiKey, string modelId) creds)
	{
		credentials = creds;
		holder = new(this, RoleType.Holder, new("daniel"));
	}
	public async Task PlayAsync()
	{
		roles.Add(new(this, RoleType.Wolf, new("ethan")));
		roles.Add(new(this, RoleType.Wolf, new("dove")));
		roles.Add(new(this, RoleType.Villager, new("alice")));
		roles.Add(new(this, RoleType.Villager, new("bob")));
		roles.Add(new(this, RoleType.Villager, new("carol")));
		holder.Say($"欢迎大家来玩狼人.我是主持人{holder.player.name}");
		holder.Say($"在坐玩家有{string.Join("，", roles.Select(static r => r.player.name))}");
		holder.Say($"其中有{roles.Count(static r => r.roleType == RoleType.Wolf)}个狼人,其他都是村民");
		holder.Say("天黑请闭眼");
		foreach(var role in roles) role.Notify("你闭上了眼");
		holder.Say("狼人请睁眼互相确认身份");
		foreach(var role in roles.Where(static r => r.roleType == RoleType.Wolf))
		{
			var wolves = string.Join(", ", roles.Where(r => r != role && r.roleType == RoleType.Wolf).Select(static r => r.player.name));
			role.Notify($"你睁开眼,发现{wolves}和你一样也是狼人");
		}
		holder.Say("狼人请闭眼");
		foreach(var role in roles.Where(static r => r.roleType == RoleType.Wolf)) role.Notify("你闭上了眼");
		holder.Say("天亮了");
		foreach(var role in roles) role.Notify("你睁开了眼");
		var originIndex = new Random().Next(0, roles.Count);
		await discuss(originIndex, "请大家依次自我介绍并发表第一轮讨论");
		holder.Say("天黑请闭眼");
		var killed = await kill();
		if(killed is null)
		{
			holder.Say("天亮了,昨晚没有玩家死亡");
			holder.Say($"昨晚平安无事,请从{roles[originIndex].player.name}开始发言,讨论昨晚发生的事情");
			await discuss(originIndex, "请发言");
		}
		else
		{
			killed.dead = true;
			holder.Say($"天亮了,昨晚{killed.Name}死了.请从死者下家开始发言,讨论昨晚发生的事情");
			await discuss(roles.IndexOf(killed) + 1, "请发言");
		}
		return;
		async Task discuss(int firstIndex, string prompt)
		{
			for(var i = 0; i < roles.Count; i++)
			{
				var role = roles[(firstIndex + i) % roles.Count];
				if(role.dead) continue;
				var result = await role.Prompt(prompt);
				role.Say(result);
			}
		}
		async Task<Role?> kill()
		{
			holder.Say("狼人请睁眼");
			foreach(var role in roles.Where(static r => r is {dead: false, roleType: RoleType.Wolf,})) role.Notify("你睁开了眼");
			for(var i = 0; i < roles.Count; i++)
			{
				var target = await vote();
				if(target is null)
					holder.Say("狼人请统一意见.如果无法达成一致,则本轮无人死亡");
				else
				{
					target.dead = true;
					return target;
				}
			}
			return null;
			async Task<Role?> vote()
			{
				Dictionary<Role, Role> votes = new();
				foreach(var role in roles.Where(static r => r is {dead: false, roleType: RoleType.Wolf,}))
				{
					var aliveRoles = roles.Where(static r => !r.dead).ToList();
					var target = await aliveRoles[0].Select("请选择你要杀死的玩家", aliveRoles.Where(static r => !r.dead).ToList());
					votes[role] = target;
				}
				if(votes.Values.Distinct().Count() == 1) return votes.Values.First();
				var message = string.Join(", ", votes.Select(static kv => $"{kv.Key.Name}选择了{kv.Value.Name}"));
				foreach(var role in roles.Where(static r => r is {dead: false, roleType: RoleType.Wolf,})) role.Notify(message);
				return null;
			}
		}
	}
}
file readonly struct ConsoleColorScope: IDisposable
{
	readonly ConsoleColor original;
	public ConsoleColorScope(ConsoleColor color)
	{
		original = Console.ForegroundColor;
		Console.ForegroundColor = color;
	}
	public void Dispose() { Console.ForegroundColor = original; }
}
