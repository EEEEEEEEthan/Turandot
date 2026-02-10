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
	sealed class Player(string name, string personalityPrompts = "毫无特色")
	{
		public readonly string personalityPrompts = personalityPrompts;
		public readonly string name = name;
	}
	abstract class Role(Game game, Player player)
	{
		public readonly Player player = player;
		public bool dead;
		readonly List<ChatMessageContent> context = [new(AuthorRole.System, "你在玩狼人游戏.请随意说谎,表演.同时不要轻易相信任何人"),];
		public string Name => player.name;
		public abstract string RoleText { get; }
		public abstract string Goal { get; }
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
			prompt = $"你是{Name}({RoleText},你的性格:{player.personalityPrompts}),请发言:{prompt}";
			using(new ConsoleColorScope(ConsoleColor.DarkGray)) Console.WriteLine($"[{Name}]{prompt}");
			var copied = new List<ChatMessageContent>(context) {new(AuthorRole.User, $"{prompt}(`[名字]`是系统帮添加的,发言内容请不要附带`[{Name}]`,发言尽可能口语化.请隐藏身份,随意说谎,表演,同时也不要轻易相信任何人)"),};
			return LLM.SendAsync(game.credentials, copied);
		}
		public async Task<Role> Select(string prompt, List<Role> options)
		{
			if(options.Count <= 0) throw new ArgumentException("选项不能为空", nameof(options));
			var availableOptions = string.Join("，", options.Select(static r => r.Name));
			prompt = $"你是{Name}({RoleText},你的性格:{player.personalityPrompts}),现在选择目标.{prompt}";
			using(new ConsoleColorScope(ConsoleColor.DarkGray)) Console.WriteLine($"[{Name}]{prompt}[{availableOptions}]");
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
		public override string RoleText => "主持人";
		public override string Goal => "报流程、传话就行，别自己加戏～";
		public void Whisper(Role target, string message)
		{
			message = $"[{Name}](悄悄对{target.Name}说){message}";
			target.AppendMessage(new(AuthorRole.System, message));
			Console.WriteLine(message);
		}
	}
	sealed class WolfRole(Game game, Player player): Role(game, player)
	{
		public override string RoleText => "狼人";
		public override string Goal => "你的目标是尽可能杀死村民,让村民数量小于等于狼人数量相等你就赢了.";
	}
	sealed class VillagerRole(Game game, Player player): Role(game, player)
	{
		public override string RoleText => "村民";
		public override string Goal => "你的目标是让狼人死光";
	}
	sealed class SeerRole(Game game, Player player): Role(game, player)
	{
		public override string RoleText => "预言家";
		public override string Goal => "你的目标是让狼人死光";
	}
	readonly List<Role> roles = [];
	readonly HolderRole holder;
	readonly List<Player> players;
	readonly (string endpoint, string apiKey, string modelId) credentials;
	public Game((string endpoint, string apiKey, string modelId) creds)
	{
		players =
		[
			new("ethan", "狼人时倾向自刀做身份、骗女巫救或银水;好人时对银水信任度高,会据此排坑"),
			new("ziham", "首轮爱起跳或反水,用激进发言带风向;狼时敢对刚真预言家,好人时也常先手压人"),
			new("luna", "前几轮几乎不发言,靠听票型和发言细节记笔记;轮次关键时才长发言,一击必中"),
			new("mike", "被点或站错队时语气会抖、重复用词;好人被冤容易情绪化,狼被踩时反而话多辩解"),
			new("alice", "发言按时间线盘谁跟谁、谁保谁,爱画身份链;信逻辑不信状态,容易忽略演技型玩家"),
			new("leo", "听完强势方发言容易改票;谁语气肯定就跟谁,容易被狼队煽动或好人带队带着走"),
			new("nina", "常用'我不太会玩''我搞不清'降低存在感,实则抿人准、刀法稳;装懵时细节会露馅"),
			new("ryan", "故意说反逻辑、假站边或模糊立场,让全场混乱;狼时搅局抗推,好人时也爱开玩笑干扰判断"),
			new("sara", "拿预言家必报查验、要警徽、强势归票;好人时见不公就开怼,容易成为狼队首刀或抗推目标"),
		];
		Config config = players.Count switch
		{
			4 => new() {wolfCount = 1, seerCount = 1, villagerCount = 2,},
			5 => new() {wolfCount = 1, seerCount = 1, villagerCount = 3,},
			6 => new() {wolfCount = 2, seerCount = 1, villagerCount = 3,},
			7 => new() {wolfCount = 2, seerCount = 1, villagerCount = 4,},
			8 => new() {wolfCount = 3, seerCount = 1, villagerCount = 4,},
			9 => new() {wolfCount = 3, seerCount = 1, villagerCount = 5,},
			_ => throw new ArgumentException("不支持的玩家数量", nameof(creds)),
		};
		if(config.seerCount > 1) throw new ArgumentException("预言家数量不能大于1", nameof(creds));
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
		holder.Say($"欢迎大家来玩狼人.我是主持人{holder.Name}");
		holder.Say($"在坐玩家有{string.Join("，", roles.Select(static r => r.Name))}");
		holder.Say($"本局配置: {config.wolfCount}个狼人, {config.seerCount}个预言家, {config.villagerCount}个村民");
		foreach(var role in roles)
			role.Notify($"你的身份是{role.RoleText}。你的目标是{role.Goal}");
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
		holder.Say("天黑请闭眼");
		foreach(var role in roles) role.Notify("你闭上了眼");
		holder.Say("狼人请睁眼互相确认身份.本轮仅确认身份,不杀人");
		foreach(var role in roles.OfType<WolfRole>())
		{
			var wolves = string.Join(", ", roles.OfType<WolfRole>().Where(r => r != role).Select(static r => r.Name));
			role.Notify($"你睁开眼,发现{wolves}和你一样也是狼人");
		}
		holder.Say("狼人请闭眼");
		foreach(var role in roles.OfType<WolfRole>()) role.Notify("你闭上了眼");
		holder.Say("天亮了.");
		foreach(var role in roles) role.Notify("你睁开了眼");
		var originIndex = new Random().Next(0, roles.Count);
		holder.Say($"刚才的回合仅是互相确认身份,按照规则不会发生任何事情.狼人没有杀人,预言家没有验身份.现在请从{roles[originIndex].player.name}开始发言,简单做一下自我介绍.");
		await discuss(originIndex, "请发言");
		while(true)
		{
			holder.Say("天黑请闭眼");
			var killed = await wolfTurn();
			await seerTurn();
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
		async Task<Role?> wolfTurn()
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
			holder.Say("狼人请闭眼");
			foreach(var role in roles.OfType<WolfRole>().Where(static r => !r.dead)) role.Notify("你闭上了眼");
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
		async Task seerTurn()
		{
			holder.Say("预言家请睁眼");
			foreach(var role in roles.OfType<SeerRole>().Where(static r => !r.dead)) role.Notify("你睁开了眼");
			var alive = roles.Where(static r => !r.dead).ToList();
			foreach(var seer in roles.OfType<SeerRole>().Where(static r => !r.dead))
			{
				var target = await seer.Select("请选择你要查验的玩家", alive);
				var isWolf = target is WolfRole;
				holder.Whisper(seer, $"你查验了{target.Name}，TA是{(isWolf? "狼人" : "好人")}");
			}
			foreach(var role in roles.OfType<SeerRole>().Where(static r => !r.dead)) role.Notify("你闭上了眼");
			holder.Say("预言家请闭眼");
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
