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
		public int witchCount;
		public int villagerCount;
		public readonly int Count => wolfCount + seerCount + witchCount + villagerCount;
	}
	sealed class Player(string name, string personalityPrompts = "毫无特色")
	{
		public readonly string personalityPrompts = personalityPrompts;
		public readonly string name = name;
		public string Memory
		{
			get
			{
				if(File.Exists($"{name}_memory.txt")) return File.ReadAllText($"{name}_memory.txt");
				return"";
			}
			set => File.WriteAllText("{name}_memory.txt", value);
		}
	}
	abstract class Role(Game game, Player player)
	{
		public readonly Player player = player;
		public bool dead;
		protected readonly Game game = game;
		protected readonly List<ChatMessageContent> context = [new(AuthorRole.System, "你在玩狼人游戏.请随意说谎,表演.同时不要轻易相信任何人"),];
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
			prompt = $"你是{Name}({RoleText}),请发言:{prompt}";
			using(new ConsoleColorScope(ConsoleColor.DarkGray)) Console.WriteLine($"[{Name}]{prompt}");
			prompt =
				$"""
				你的性格:{player.personalityPrompts}.
				你记得：{player.Memory}.
				你们在玩狼人杀.
				{prompt}(`[名字]`是系统帮添加的,发言内容请不要附带`[{Name}]`,发言尽可能口语化.请隐藏身份,随意说谎,表演,同时也不要轻易相信任何人,不要说关于`听到声音`等内容)
				""";
			var copied = new List<ChatMessageContent>(context)
				{new(AuthorRole.User, $"{prompt}"),};
			return LLM.SendAsync(game.credentials, copied);
		}
		public async Task<Role> Select(string prompt, List<Role> options)
		{
			if(options.Count <= 0) throw new ArgumentException("选项不能为空", nameof(options));
			var availableOptions = string.Join("，", options.Select(static r => r.Name));
			prompt = $"你是{Name}({RoleText}),现在选择目标.{prompt}";
			using(new ConsoleColorScope(ConsoleColor.DarkGray)) Console.WriteLine($"[{Name}]{prompt}[{availableOptions}]");
			prompt = $"""
				你的性格:{player.personalityPrompts}.
				你记得：{player.Memory}.
				你们在玩狼人杀.
				{prompt}
				""";
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
		protected async Task<Role?> SelectTargetOrSkip(string prompt, List<Role> options)
		{
			const string skipOption = "__SKIP__";
			if(options.Count <= 0) throw new ArgumentException("选项不能为空", nameof(options));
			var availableOptions = string.Join("，", options.Select(static r => r.Name)) + "，" + skipOption;
			prompt = $"你是{Name}({RoleText}),现在选择目标.{prompt}";
			using(new ConsoleColorScope(ConsoleColor.DarkGray)) Console.WriteLine($"[{Name}]{prompt}[{availableOptions}]");
			prompt = $"你的性格:{player.personalityPrompts}.\n你记得：{player.Memory}.\n你们在玩狼人杀.\n{prompt}";
			const string toolName = "select_target_or_skip";
			Role? target = null;
			var skipped = false;
			while(target is null && !skipped)
			{
				var tool = new LLM.ToolSpec(
					toolName,
					$"选择玩家或`{skipOption}`跳过",
					[new("target", $"从[{availableOptions}]中选一个", typeof(string), true),],
					(payload, _) =>
					{
						var t = (payload["target"]?.ToString() ?? "").Trim();
						if(t == skipOption)
						{
							skipped = true;
							return Task.FromResult("已记录");
						}
						var r = options.FirstOrDefault(x => x.Name == t);
						if(r is null) return Task.FromResult($"无效。请选{availableOptions}");
						target = r;
						return Task.FromResult("已记录");
					});
				var copied = new List<ChatMessageContent>(context) {new(AuthorRole.User, prompt),};
				_ = await LLM.SendAsync(game.credentials, copied, [tool,], 0.6f);
			}
			if(skipped)
				using(new ConsoleColorScope(ConsoleColor.DarkGray))
					Console.WriteLine($"[{Name}]跳过");
			else if(target is {})
				using(new ConsoleColorScope(ConsoleColor.DarkGray))
					Console.WriteLine($"[{Name}]选择了{target.Name}");
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
		public override string Goal => "你的目标是保护特殊身份的人类,让狼人死光";
	}
	sealed class SeerRole(Game game, Player player): Role(game, player)
	{
		public override string RoleText => "预言家";
		public override string Goal => "你的目标是让狼人死光";
	}
	sealed class WitchRole(Game game, Player player): Role(game, player)
	{
		bool hasSavePotion = true;
		public override string RoleText => "女巫";
		public override string Goal => "你的目标是让狼人死光。你有一瓶救药可救活当晚被狼刀的玩家，一瓶毒药可毒死一名玩家(各整局只能用一次,请谨慎使用)";
		public bool HasPoisonPotion { get; private set; } = true;
		/// <summary>女巫决定是否用救药救活被刀玩家，返回 true 表示使用救药</summary>
		public async Task<bool> TrySave(Role killed)
		{
			if(!hasSavePotion) return false;
			const string toolName = "witch_save";
			var prompt = $"{killed.Name}死了。请决定是否使用救药救活TA(使用{toolName}工具)";
			using(new ConsoleColorScope(ConsoleColor.DarkGray)) Console.WriteLine($"[{Name}]{prompt}[救/不救]");
			bool? saved = null;
			while(saved is null)
			{
				var tool = new LLM.ToolSpec(
					toolName,
					$"选择是否使用救药(你的性格:{player.personalityPrompts})",
					[new("use_save", "是否使用救药救活，必须填`yes`或`no`", typeof(string), true),],
					(payload, _) =>
					{
						var v = (payload["use_save"]?.ToString() ?? "").Trim();
						switch(v)
						{
							case"yes":
								saved = true;
								return Task.FromResult("已记录");
							case"no":
								saved = false;
								return Task.FromResult("已记录");
							default: return Task.FromResult("无效。请填救或不救");
						}
					});
				var copied = new List<ChatMessageContent>(context) {new(AuthorRole.User, prompt),};
				_ = await LLM.SendAsync(game.credentials, copied, [tool,], 0.6f);
			}
			if(saved == true) hasSavePotion = false;
			using(new ConsoleColorScope(ConsoleColor.DarkGray)) Console.WriteLine($"[{Name}]选择{(saved == true? "救" : "不救")}");
			return saved == true;
		}
		public async Task<Role?> TryPoison(List<Role> alive)
		{
			if(!HasPoisonPotion || alive.Count <= 0) return null;
			var options = alive.Where(r => r != this).ToList();
			if(options.Count == 0) return null;
			var target = await SelectTargetOrSkip("请选择你要毒死的玩家或者跳过", options);
			if(target is null) return null;
			HasPoisonPotion = false;
			return target;
		}
	}
	readonly List<Role> roles = [];
	readonly HolderRole holder;
	readonly List<Player> players;
	readonly (string endpoint, string apiKey, string modelId) credentials;
	public Game((string endpoint, string apiKey, string modelId) creds)
	{
		players =
		[
			new("ethan", "狼人时小概率自刀做身份、骗女巫救或银水;好人时对银水信任度高,会据此排坑"),
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
			4 => new() {wolfCount = 1, seerCount = 1, witchCount = 1, villagerCount = 1,},
			5 => new() {wolfCount = 1, seerCount = 1, witchCount = 1, villagerCount = 2,},
			6 => new() {wolfCount = 2, seerCount = 1, witchCount = 1, villagerCount = 2,},
			7 => new() {wolfCount = 2, seerCount = 1, witchCount = 1, villagerCount = 3,},
			8 => new() {wolfCount = 3, seerCount = 1, witchCount = 1, villagerCount = 3,},
			9 => new() {wolfCount = 3, seerCount = 1, witchCount = 1, villagerCount = 4,},
			_ => throw new ArgumentException("不支持的玩家数量", nameof(creds)),
		};
		if(config.seerCount > 1) throw new ArgumentException("预言家数量不能大于1", nameof(creds));
		if(config.witchCount > 1) throw new ArgumentException("女巫数量不能大于1", nameof(creds));
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
		for(var i = config.witchCount; i-- > 0;) addWitch();
		for(var i = config.villagerCount; i-- > 0;) addVillager();
		for(var i = roles.Count; i-- > 0;)
		{
			var j = random.Next(0, i + 1);
			(roles[i], roles[j]) = (roles[j], roles[i]);
		}
		holder.Say($"欢迎大家来玩狼人.我是主持人{holder.Name}");
		holder.Say($"在坐玩家有{string.Join("，", roles.Select(static r => r.Name))}");
		holder.Say($"本局配置: {config.wolfCount}个狼人, {config.seerCount}个预言家, {config.witchCount}个女巫, {config.villagerCount}个村民");
		foreach(var role in roles)
		{
			var text = $"你的身份是{role.RoleText}。你的目标是{role.Goal}";
			if(role is WolfRole)
			{
				var wolfNames = string.Join(", ", roles.Where(r => r != role && r is WolfRole).Select(static r => r.Name));
				text += ". " + wolfNames + " 和你一样是狼人";
			}
			role.Notify(text);
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
		void addWitch()
		{
			var player = players[^1];
			players.RemoveAt(players.Count - 1);
			roles.Add(new WitchRole(this, player));
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
		var originIndex = new Random().Next(0, roles.Count);
		holder.Say($"游戏开始了.请从{roles[originIndex].player.name}开始发言,简单做一下自我介绍.");
		await discuss(originIndex, "游戏刚开始,不要说谁的语气有问题");
		while(true)
		{
			holder.Say("天黑请闭眼");
			var wolfTarget = await wolfTurn();
			var (saved, poisoned) = await witchTurn(wolfTarget);
			await seerTurn();
			var deaths = new List<Role>();
			if(wolfTarget is {} && !saved)
			{
				wolfTarget.dead = true;
				deaths.Add(wolfTarget);
			}
			if(poisoned is {})
			{
				poisoned.dead = true;
				deaths.Add(poisoned);
			}
			if(deaths.Count == 0)
			{
				holder.Say($"天亮了,昨晚平安无事,请从{roles[originIndex].player.name}开始发言,讨论昨晚发生的事情");
				await discuss(originIndex, "请发言");
			}
			else
			{
				holder.Say($"天亮了,昨晚{string.Join("、", deaths.Select(static r => r.Name))}死了.请从死者下家开始发言,讨论昨晚发生的事情");
				var firstDeathIdx = deaths.Min(d => roles.IndexOf(d));
				await discuss((firstDeathIdx + 1) % roles.Count, "请依次发言.每人只有一次发言机会");
				var executed = await voteExecute();
				if(executed is {})
				{
					executed.dead = true;
					holder.Say($"{executed.Name}被投票处决");
					var wolfCount = roles.Count(static r => r is WolfRole {dead: false,});
					var villagerCount = roles.Count(static r => !r.dead && r is not WolfRole);
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
					var lastWords = await executed.Prompt("请发表遗言");
					executed.Say(lastWords);
				}
			}
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
			Role? target = null;
			for(var i = 0; i < roles.Count; i++)
			{
				target = await vote();
				if(target is null)
					holder.Say("狼人请统一意见.如果无法达成一致,则本轮无人死亡");
				else
					break;
			}
			holder.Say("狼人请闭眼");
			foreach(var role in roles.OfType<WolfRole>().Where(static r => !r.dead)) role.Notify("你闭上了眼");
			return target;
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
		async Task<(bool saved, Role? poisoned)> witchTurn(Role? killed)
		{
			var witches = roles.OfType<WitchRole>().Where(static r => !r.dead).ToList();
			if(witches.Count == 0) return(false, null);
			holder.Say("女巫请睁眼");
			foreach(var witch in witches) witch.Notify("你睁开了眼");
			foreach(var witch in witches) holder.Whisper(witch, killed is null? "没有人死" : $"{killed.Name}被狼人杀死。你是否使用救药救活TA？");
			var saved = false;
			if(killed != null)
				foreach(var witch in witches)
					if(await witch.TrySave(killed))
						saved = true;
			var alive = roles.Where(static r => !r.dead).ToList();
			Role? poisoned = null;
			foreach(var witch in witches)
				if(!witch.HasPoisonPotion)
					holder.Whisper(witch, "你没有毒药了");
				else
				{
					holder.Whisper(witch, "你想毒死谁");
					var p = await witch.TryPoison(alive);
					if(p is {}) poisoned = p;
				}
			foreach(var witch in witches) witch.Notify("你闭上了眼");
			holder.Say("女巫请闭眼");
			return(saved, poisoned);
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
