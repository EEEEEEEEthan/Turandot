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
	struct Config
	{
		public int wolfCount;
		public int seerCount;
		public int villagerCount;
		public readonly int Count => wolfCount + seerCount + villagerCount;
	}
	sealed class Player(string name)
	{
		public readonly string name = name;
	}
	abstract class Role(Game game, Player player)
	{
		public readonly Player player = player;
		public bool dead;
		readonly List<ChatMessageContent> context = [];
		public string Name => player.name;
		protected abstract string RoleText { get; }
		public void AppendMessage(ChatMessageContent content) { context.Add(content); }
		public void Say(string message)
		{
			message = $"[{Name}]{message}";
			foreach(var role in game.roles)
				role.context.Add(new(AuthorRole.Assistant, message));
			Console.WriteLine(message);
		}
		public void Notify(string message)
		{
			message = $"[{Name}]{message}";
			context.Add(new(AuthorRole.System, message));
			using(new ConsoleColorScope(ConsoleColor.DarkGray))
				Console.WriteLine(message);
		}
		public Task<string> Prompt(string prompt)
		{
			prompt = $"你是{Name}({RoleText}),请发言:{prompt}";
			using(new ConsoleColorScope(ConsoleColor.DarkGray)) Console.WriteLine($"[{Name}]{prompt}");
			var copied = new List<ChatMessageContent>(context) {new(AuthorRole.User, $"{prompt}(`[名字]`是系统帮添加的,发言内容请不要附带`[{Name}]`,发言尽可能口语化)"),};
			return LLM.SendAsync(game.credentials, copied);
		}
		public async Task<Role> Select(string prompt, List<Role> options)
		{
			if(options.Count <= 0) throw new ArgumentException("选项不能为空", nameof(options));
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
			using(new ConsoleColorScope(ConsoleColor.DarkGray)) Console.WriteLine($"[{Name}]选择了{target.Name}");
			return target;
		}
	}
	sealed class HolderRole(Game game, Player player): Role(game, player)
	{
		protected override string RoleText => "主持人";
		public void Whisper(Role target, string message)
		{
			message = $"[{Name}](悄悄对{target.Name}说){message}";
			target.AppendMessage(new(AuthorRole.System, message));
			Console.WriteLine(message);
		}
	}
	sealed class WolfRole(Game game, Player player): Role(game, player)
	{
		protected override string RoleText => "狼人";
	}
	sealed class VillagerRole(Game game, Player player): Role(game, player)
	{
		protected override string RoleText => "村民";
	}
	sealed class SeerRole(Game game, Player player): Role(game, player)
	{
		protected override string RoleText => "预言家";
	}
	readonly List<Role> roles = [];
	readonly HolderRole holder;
	readonly List<Player> players;
	readonly (string endpoint, string apiKey, string modelId) credentials;
	readonly Config config;
	public Game((string endpoint, string apiKey, string modelId) creds)
	{
		players = new[] {"ethan", "dove", "frank", "alice", "bob", "carol", "grace", "heidy", "ivan",}.Select(static name => new Player(name)).ToList();
		config = players.Count switch
		{
			4 => new() {wolfCount = 1, seerCount = 1, villagerCount = 2,},
			5 => new() {wolfCount = 1, seerCount = 1, villagerCount = 3,},
			6 => new() {wolfCount = 2, seerCount = 1, villagerCount = 3,},
			7 => new() {wolfCount = 2, seerCount = 1, villagerCount = 4,},
			8 => new() {wolfCount = 3, seerCount = 1, villagerCount = 4,},
			9 => new() {wolfCount = 3, seerCount = 1, villagerCount = 5,},
			_ => throw new ArgumentException("不支持的玩家数量", nameof(creds)),
		};
		if(config.Count != players.Count) throw new ArgumentException("角色数量与玩家数量不匹配", nameof(creds));
		credentials = creds;
		holder = new(this, new("daniel"));
		var random = new Random((int)DateTime.Now.Ticks);
		for(var i = players.Count; i-- > 0;)
		{
			var j = random.Next(0, i + 1);
			(players[i], players[j]) = (players[j], players[i]);
		}
		for(var i = config.wolfCount; i-- > 0;) addWolf();
		for(var i = config.seerCount; i-- > 0;) addSeer();
		for(var i = config.villagerCount; i-- > 0;) addVillager();
		for(var i = roles.Count; i-- > 0;)
		{
			var j = random.Next(0, i + 1);
			(roles[i], roles[j]) = (roles[j], roles[i]);
		}
		return;
		void addWolf()
		{
			var player = players[^1];
			players.RemoveAt(players.Count - 1);
			roles.Add(new WolfRole(this, player));
		}
		void addSeer()
		{
			var player = players[^1];
			players.RemoveAt(players.Count - 1);
			roles.Add(new SeerRole(this, player));
		}
		void addVillager()
		{
			var player = players[^1];
			players.RemoveAt(players.Count - 1);
			roles.Add(new VillagerRole(this, player));
		}
	}
	public async Task PlayAsync()
	{
		holder.Say($"欢迎大家来玩狼人.我是主持人{holder.Name}");
		holder.Say($"在坐玩家有{string.Join("，", roles.Select(static r => r.Name))}");
		holder.Say($"其中有{roles.Count(static r => r is WolfRole)}个狼人,其他都是村民");
		holder.Say("天黑请闭眼");
		foreach(var role in roles) role.Notify("你闭上了眼");
		holder.Say("狼人请睁眼互相确认身份");
		foreach(var role in roles.OfType<WolfRole>())
		{
			var wolves = string.Join(", ", roles.OfType<WolfRole>().Where(r => r != role).Select(static r => r.Name));
			role.Notify($"你睁开眼,发现{wolves}和你一样也是狼人");
		}
		holder.Say("狼人请闭眼");
		foreach(var role in roles.OfType<WolfRole>()) role.Notify("你闭上了眼");
		holder.Say("天亮了");
		foreach(var role in roles) role.Notify("你睁开了眼");
		var originIndex = new Random().Next(0, roles.Count);
		holder.Say($"请从{roles[originIndex].player.name}开始发言,简单做一下自我介绍");
		await discuss(originIndex, "请发言");
		while(true)
		{
			holder.Say("天黑请闭眼");
			var killed = await kill();
			if(killed is null)
			{
				holder.Say($"天亮了,昨晚平安无事,请从{roles[originIndex].player.name}开始发言,讨论昨晚发生的事情");
				await discuss(originIndex, "请发言");
			}
			else
			{
				killed.dead = true;
				holder.Say($"天亮了,昨晚{killed.Name}死了.请从死者下家开始发言,讨论昨晚发生的事情");
				await discuss(roles.IndexOf(killed) + 1, "请依次发言.每人只有一次发言机会");
				var executed = await voteExecute();
				if(executed is {})
				{
					executed.dead = true;
					holder.Say($"{executed.Name}被投票处决");
					var lastWords = await executed.Prompt("请发表遗言");
					executed.Say(lastWords);
				}
			}
			var wolfCount = roles.Count(static r => r is WolfRole {dead: false,});
			var villagerCount = roles.Count(static r => r is VillagerRole {dead: false,});
			if(wolfCount >= villagerCount)
			{
				holder.Say("游戏结束, 狼人胜利");
				return;
			}
			if(wolfCount <= 0)
			{
				holder.Say("游戏结束, 村民胜利");
				return;
			}
			holder.Say("游戏继续");
		}
		async Task<Role?> voteExecute()
		{
			var alive = roles.Where(static r => !r.dead).ToList();
			if(alive.Count == 0) return null;
			var votes = new Dictionary<Role, int>();
			foreach(var role in alive)
			{
				var target = await role.Select("请投票选出要处决的玩家", alive);
				votes.TryGetValue(target, out var count);
				votes[target] = count + 1;
			}
			var maxVotes = votes.Values.Max();
			var top = votes.Where(kv => kv.Value == maxVotes).ToList();
			if(top.Count != 1) return null; // 平票不处决
			return top[0].Key;
		}
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
			foreach(var role in roles.OfType<WolfRole>().Where(static r => !r.dead)) role.Notify("你睁开了眼");
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
				foreach(var role in roles.OfType<WolfRole>().Where(static r => !r.dead))
				{
					var aliveRoles = roles.Where(static r => !r.dead).ToList();
					var target = await role.Select("请选择你要杀死的玩家", aliveRoles.Where(static r => !r.dead).ToList());
					votes[role] = target;
				}
				if(votes.Values.Distinct().Count() == 1) return votes.Values.First();
				var message = string.Join(", ", votes.Select(static kv => $"{kv.Key.Name}选择了{kv.Value.Name}"));
				foreach(var role in roles.OfType<WolfRole>().Where(static r => !r.dead)) role.Notify(message);
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
