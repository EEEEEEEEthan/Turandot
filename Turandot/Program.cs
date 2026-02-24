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
	public abstract int HandleVote(List<int> roleIds);
	public abstract void HandleReceiveMessage(string message);
}
sealed class AiPlayer(string name, (string endpoint, string apiKey, string modelId) credentials): Player(name)
{
	readonly List<string> messages = [];
	public override int HandleVote(List<int> roleIds)
	{
		LLM.Send(
	}
	public override void HandleReceiveMessage(string message) { messages.Add(message); }
}
sealed class HumanPlayer(string name): Player(name)
{
	public override int HandleVote(List<int> roleIds) { throw new NotImplementedException(); }
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
	sealed class Role(Game game, Player player, int id, RoleType roleType)
	{
		public readonly Player player = player;
		public readonly int id = id;
		public readonly string name = $"{player.name}({id})";
		public readonly RoleType roleType = roleType;
		public readonly bool dead = false;
		public void Say(string message)
		{
			foreach(var role in game.roles)
				if(role == this)
					role.HandleReceiveMessage(new($"[我]{message}"));
				else
					role.HandleReceiveMessage(new($"[{name}]{message}"));
			Console.WriteLine($"[{name}]{message}");
		}
		public void Notify(string message) { HandleReceiveMessage(message); }
		void HandleReceiveMessage(string message) { player.HandleReceiveMessage(message); }
	}
	static void Notify(Role role, string message)
	{
		using var _ = new ConsoleColorScope(ConsoleColor.DarkGray);
		Console.WriteLine($"[系统->{role.name}]{message}");
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
				var names = string.Join(",", GetRoles(RoleType.Wolf).Where(r => r != role).Select(static r => r.name));
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
