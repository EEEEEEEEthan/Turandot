using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Turandot;
Console.WriteLine("正在验证api key...");
var apiKeyPath = Path.Combine(
	Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
	".apikey");
var credentials = await LLM.EnsureApiKey(apiKeyPath);
var config = new Game.Config(
	new List<Player>
	{
		new HumanPlayer("ethan"),
		new AiPlayer("dove", credentials),
		new AiPlayer("drake", credentials),
		new AiPlayer("coco", credentials),
		new AiPlayer("belle", credentials),
	},
	2,
	3);
_ = new Game(config);
Console.WriteLine("按任意键退出");
Console.ReadKey(true);
abstract class Player(string name)
{
	public readonly string name;
	public abstract int HandleVote(Game.Role me, List<Game.Role> roles);
	public abstract void HandleReceiveMessage(string message);
}
sealed class AiPlayer(string name, (string endpoint, string apiKey, string modelId) credentials): Player(name)
{
	readonly List<string> history = [];
	public override int HandleVote(Game.Role me, List<Game.Role> roles)
	{
		var voteResult = -1;
		var roleList = string.Join(", ", roles.Select(static r => $"{r.nameAndId}"));
		var tool = new LLM.ToolSpec(
			"Vote",
			"投票淘汰一名玩家",
			[new("id", "要投票淘汰的玩家id", typeof(int), true),],
			(args, _) =>
			{
				if(args.TryGetValue("id", out var val))
					voteResult = val switch
					{
						int i => i,
						string s when int.TryParse(s, out var parsed) => parsed,
						_ => -1,
					};
				return Task.FromResult("投票成功");
			});
		var messages = new List<ChatMessageContent>
		{
			new(AuthorRole.System, $"你是{me.nameAndId},请投票，调用Vote工具完成投票。选择对象：{roleList}"),
		};
		messages.AddRange(history.Select(static m => new ChatMessageContent(AuthorRole.User, m)));
		LLM.Send(credentials, messages, [tool,]);
		return voteResult;
	}
	public override void HandleReceiveMessage(string message) { history.Add(message); }
}
sealed class HumanPlayer(string name): Player(name)
{
	public override int HandleVote(Game.Role me, List<Game.Role> roles) { throw new NotImplementedException(); }
	public override void HandleReceiveMessage(string message) { throw new NotImplementedException(); }
}
sealed class Game
{
	public enum RoleType
	{
		Wolf,
		Villager,
	}
	public readonly struct Config(IReadOnlyList<Player> players, int wolfCount, int villagerCount)
	{
		public readonly int wolfCount = wolfCount;
		public readonly int villagerCount = villagerCount;
		public readonly IReadOnlyList<Player> players = players;
	}
	public sealed class Role(Game game, Player player, int id, RoleType roleType)
	{
		public readonly Player player = player;
		public readonly int id = id;
		public readonly string nameAndId = $"{player.name}({id})";
		public readonly RoleType roleType = roleType;
		public readonly bool dead = false;
		public void Say(string message)
		{
			foreach(var role in game.roles)
				if(role == this)
					role.HandleReceiveMessage(new($"[我]{message}"));
				else
					role.HandleReceiveMessage(new($"[{nameAndId}]{message}"));
			Console.WriteLine($"[{nameAndId}]{message}");
		}
		public void Notify(string message) { HandleReceiveMessage(message); }
		void HandleReceiveMessage(string message) { player.HandleReceiveMessage(message); }
	}
	static void Notify(Role role, string message)
	{
		using var _ = new ConsoleColorScope(ConsoleColor.DarkGray);
		Console.WriteLine($"[系统->{role.nameAndId}]{message}");
		role.Notify($"[系统提示(仅你可见)]{message}");
	}
	readonly Config config;
	readonly IReadOnlyList<Role> roles;
	public Game(Config config)
	{
		this.config = config;
		var id = 0;
		var roleTypes = new List<RoleType>();
		roleTypes.AddRange(Enumerable.Repeat(RoleType.Wolf, config.wolfCount));
		roleTypes.AddRange(Enumerable.Repeat(RoleType.Villager, config.villagerCount));
		roleTypes.Shuffle();
		var roles = new List<Role>();
		this.roles = roles;
		for(var i = 0; i < config.players.Count; i++)
		{
			var player = config.players[i];
			var roleType = roleTypes[i];
			var role = new Role(this, player, id++, roleType);
			roles.Add(role);
		}
		Notify("游戏开始了");
		foreach(var role in roles) Notify(role, $"你的身份是{role.roleType}");
		Play();
	}
	void Play()
	{
		Notify("天黑请闭眼");
		Notify("狼人请睁眼");
		foreach(var role in GetRoles(RoleType.Wolf))
		{
			var otherWolves = GetRoles(RoleType.Wolf).Where(r => r != role).ToList();
			if(otherWolves.Count == 0)
				Notify(role, "你睁开了眼睛,你是场上唯一的狼人");
			else
			{
				var names = string.Join(",", GetRoles(RoleType.Wolf).Where(r => r != role).Select(static r => r.nameAndId));
				Notify(role, $"你睁开了眼睛,你看到{names}也睁开了眼睛");
			}
		}
	}
	IEnumerable<Role> GetRoles(RoleType type, bool deadIncluded = false)
	{
		if(deadIncluded)
			return roles.Where(r => r.roleType == type);
		return roles.Where(r => r.roleType == type && !r.dead);
	}
	void Notify(string message)
	{
		using var _ = new ConsoleColorScope(ConsoleColor.Yellow);
		var wrapped = $"[系统公告]{message}";
		Console.WriteLine(wrapped);
		foreach(var role in roles) role.Notify(wrapped);
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
