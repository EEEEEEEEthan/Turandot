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
		string Name => player.name;
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
			Console.WriteLine(message);
		}
		public Task<string> Prompt(string prompt)
		{
			using(new ConsoleColorScope(ConsoleColor.DarkGray)) Console.WriteLine($"[{Name}]{prompt}");
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
		_ = PlayAsync();
	}
	public async Task PlayAsync()
	{
		roles.Add(new(this, RoleType.Wolf, new("ethan")));
		roles.Add(new(this, RoleType.Wolf, new("dove")));
		roles.Add(new(this, RoleType.Villager, new("alice")));
		roles.Add(new(this, RoleType.Villager, new("bob")));
		roles.Add(new(this, RoleType.Villager, new("carol")));
		holder.Say($"欢迎大家来玩狼人.我是主持人{holder.player.name}");
		holder.Say("天黑请闭眼");
		holder.Say("狼人请睁眼");
		foreach(var role in roles.Where(static r => r.roleType == RoleType.Wolf))
		{
			var wolves = string.Join(", ", roles.Where(r => r != role && r.roleType == RoleType.Wolf));
			role.Notify($"你睁开眼,发现{wolves}和你一样也是狼人");
		}
		holder.Say("狼人请闭眼");
		holder.Say("天亮了");
		var index = new Random().Next(0, roles.Count);
		holder.Say($"从{roles[index].player.name}开始依次发言");
		for(var i = 0; i < roles.Count; i++)
		{
			var role = roles[(index + i) % roles.Count];
			role.Say(await role.Prompt("请做一个简单的发言"));
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
