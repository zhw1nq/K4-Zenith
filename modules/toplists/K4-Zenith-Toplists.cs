using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using Dapper;
using Menu;
using Menu.Enums;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using ZenithAPI;

namespace Zenith_TopLists;

[MinimumApiVersion(260)]
public class TopListsPlugin : BasePlugin
{
	private const string MODULE_ID = "Toplists";
	public const int DEFAULT_PLAYER_COUNT = 5;

	public override string ModuleName => $"K4-Zenith | {MODULE_ID}";
	public override string ModuleAuthor => "K4ryuu @ KitsuneLab";
	public override string ModuleVersion => "1.0.9";

	private PluginCapability<IModuleServices>? _moduleServicesCapability;
	private PlayerCapability<IPlayerServices>? _playerServicesCapability;
	public IModuleServices? ModuleServices { get; private set; }
	public IZenithEvents? ZenithEvents { get; private set; }
	public IModuleConfigAccessor? CoreAccessor { get; private set; }

	public RankTopHandler? RankTopHandler { get; private set; }
	public TimeTopHandler? TimeTopHandler { get; private set; }
	public StatsTopHandler? StatsTopHandler { get; private set; }

	private readonly ConcurrentDictionary<ulong, Tuple<long, DateTime>> _topPlacementCache = new();
	private DateTime _topPlacementCacheTriggered = DateTime.MinValue;

	public KitsuneMenu? Menu { get; private set; }
	public Dictionary<string, bool> _loadedModules = [];

	public override void OnAllPluginsLoaded(bool hotReload)
	{
		try
		{
			_moduleServicesCapability = new("zenith:module-services");
			_playerServicesCapability = new("zenith:player-services");

			ModuleServices = _moduleServicesCapability.Get();
			if (ModuleServices == null)
			{
				throw new Exception("Failed to get Module-Services API for Zenith.");
			}

			ZenithEvents = ModuleServices.GetEventHandler();
			if (ZenithEvents != null)
			{
				ZenithEvents.OnZenithCoreUnload += OnZenithCoreUnload;
				ZenithEvents.OnZenithPlayerLoaded += player => CacheTopPlacements();
			}
			else
			{
				Logger.LogError("Failed to get Zenith event handler.");
			}

			Menu = new KitsuneMenu(this);

			CoreAccessor = ModuleServices.GetModuleConfigAccessor();

			ModuleServices.RegisterModuleConfig("Commands", "GeneralTopCommands", "Commands to use the general toplists", new List<string> { "top", "toplist" });
			ModuleServices.RegisterModuleConfig("Commands", "RankTopCommands", "Commands to use the rank toplists", new List<string> { "ranktop", "rtop" });
			ModuleServices.RegisterModuleConfig("Commands", "TimeTopCommands", "Commands to use the time toplists", new List<string> { "timetop", "ttop" });
			ModuleServices.RegisterModuleConfig("Commands", "StatsTopCommands", "Commands to use the statistic toplists", new List<string> { "stattop", "statstop", "stop" });

			ModuleServices.RegisterModuleConfig("Settings", "ClanTagMax", "The maximum of the top placement to add clantag addition", 20);

			RankTopHandler = new RankTopHandler(this);
			TimeTopHandler = new TimeTopHandler(this);
			StatsTopHandler = new StatsTopHandler(this);

			ModuleServices.RegisterModuleCommands(CoreAccessor.GetValue<List<string>>("Commands", "GeneralTopCommands"), "Shows the toplist main menu", OnTopCommand, CommandUsage.CLIENT_ONLY);
			ModuleServices.RegisterModuleCommands(CoreAccessor.GetValue<List<string>>("Commands", "RankTopCommands"), "Shows the top players by ranks", OnRankTopCommand, CommandUsage.CLIENT_ONLY);
			ModuleServices.RegisterModuleCommands(CoreAccessor.GetValue<List<string>>("Commands", "TimeTopCommands"), "Show top players by playtime", OnTimeTopCommand, CommandUsage.CLIENT_ONLY);
			ModuleServices.RegisterModuleCommands(CoreAccessor.GetValue<List<string>>("Commands", "StatsTopCommands"), "Show top players by statistics", OnStatsTopCommand, CommandUsage.CLIENT_ONLY);

			_loadedModules.Add("Ranks", Directory.Exists(Path.Combine(ModuleDirectory, "..", "K4-Zenith-Ranks")));
			_loadedModules.Add("Stats", Directory.Exists(Path.Combine(ModuleDirectory, "..", "K4-Zenith-Stats")));
			_loadedModules.Add("Time", Directory.Exists(Path.Combine(ModuleDirectory, "..", "K4-Zenith-TimeStats")));

			if (_loadedModules.All(x => !x.Value))
				Logger.LogWarning("No supported modules found. Please make sure to install at least one of the following modules: Ranks, Stats, TimeStats.");

			AddTimer(60.0f, CacheTopPlacements, TimerFlags.REPEAT);

			ModuleServices!.RegisterModulePlayerPlaceholder("rank_top_placement", p =>
			{
				if (p == null) return string.Empty;

				var steamId = p.SteamID;

				if (_topPlacementCache.TryGetValue(steamId, out var cachedData) && CoreAccessor!.GetValue<int>("Settings", "ClanTagMax") <= cachedData.Item1)
				{
					return Localizer["top.clantag", cachedData.Item1];
				}

				return string.Empty;
			});

			Logger.LogInformation("Zenith {0} module successfully registered.", MODULE_ID);
		}
		catch (Exception ex)
		{
			Logger.LogError($"Failed to initialize Zenith API: {ex.Message}");
			Logger.LogInformation("Please check if Zenith is installed, configured and loaded correctly.");
			Server.ExecuteCommand($"css_plugins unload {Path.GetFileNameWithoutExtension(ModulePath)}");
		}
	}

	private void CacheTopPlacements()
	{
		if ((DateTime.UtcNow - _topPlacementCacheTriggered).TotalSeconds < 3)
			return;

		if (!_loadedModules["Ranks"])
			return;

		var onlinePlayers = Utilities.GetPlayers()
			.Where(p => p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.Connected)
			.ToList();

		if (onlinePlayers.Count == 0)
			return;

		_topPlacementCacheTriggered = DateTime.UtcNow;

		Task.Run(async () =>
		{
			try
			{
				string? connectionString = ModuleServices?.GetConnectionString();
				if (string.IsNullOrEmpty(connectionString))
				{
					throw new InvalidOperationException("Database connection string is null or empty.");
				}

				using var connection = new MySqlConnection(connectionString);
				await connection.OpenAsync();

				var steamIds = onlinePlayers.Select(p => p.SteamID.ToString()).ToList();

				const string query = @"
					SELECT
						t1.steam_id,
						(SELECT COUNT(*) + 1
						FROM zenith_player_storage t2
						WHERE CAST(JSON_EXTRACT(t2.`K4-Zenith-Ranks.storage`, '$.Points') AS DECIMAL(65,2)) >
							COALESCE(CAST(JSON_EXTRACT(t1.`K4-Zenith-Ranks.storage`, '$.Points') AS DECIMAL(65,2)), 0)
						) as rank_position
					FROM zenith_player_storage t1
					WHERE
						FIND_IN_SET(t1.steam_id, @SteamIds) > 0
						AND JSON_EXTRACT(t1.`K4-Zenith-Ranks.storage`, '$.Points') IS NOT NULL
						AND t1.`K4-Zenith-Ranks.storage` IS NOT NULL";

				string steamIdString = string.Join(",", steamIds);

				var results = await connection.QueryAsync<(string SteamId, long Placement)>(
					query,
					new { SteamIds = steamIdString }
				);

				foreach (var (SteamId, Placement) in results)
				{
					var foundPlayer = onlinePlayers.FirstOrDefault(p => p.IsValid && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.Connected && p.SteamID.ToString() == SteamId);

					if (foundPlayer != null)
					{
						var steamId = onlinePlayers.First(p => p.SteamID.ToString() == SteamId).SteamID;
						_topPlacementCache[steamId] = Tuple.Create(Placement, DateTime.UtcNow);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Failed to cache top placements: {Error}", ex.Message);
			}
		});
	}

	private void OnTopCommand(CCSPlayerController? player, CommandInfo command)
	{
		if (player == null || RankTopHandler == null) return;

		if (CoreAccessor?.GetValue<bool>("Core", "CenterMenuMode") == true)
		{
			ShowCenterMainTopMenu(player);
		}
		else
		{
			ShowChatMainTopMenu(player);
		}
	}

	private void ShowCenterMainTopMenu(CCSPlayerController player)
	{
		var items = new List<MenuItem>
		{
			new MenuItem(MenuItemType.Button, [new MenuButtonCallback(Localizer.ForPlayer(player, "top.menu.rank"), "rank", (player, data) => {
				RankTopHandler?.HandleRankTopCommand(player);
			}, !_loadedModules["Ranks"])]),
			new MenuItem(MenuItemType.Button, [new MenuButtonCallback(Localizer.ForPlayer(player, "top.menu.time"), "time", (player, data) => {
				TimeTopHandler?.HandleTimeTopCommand(player);
			}, !_loadedModules["Time"])]),
			new MenuItem(MenuItemType.Button, [new MenuButtonCallback(Localizer.ForPlayer(player, "top.menu.stats"), "stats", (player, data) => {
				StatsTopHandler?.HandleStatsTopCommand(player);
			}, !_loadedModules["Stats"])])
		};

		Menu?.ShowScrollableMenu(player, Localizer.ForPlayer(player, "top.menu.main.title"), items, null, false, CoreAccessor!.GetValue<bool>("Core", "FreezeInMenu") && (GetZenithPlayer(player)?.GetSetting<bool>("FreezeInMenu", "K4-Zenith") ?? true), 5, disableDeveloper: !CoreAccessor!.GetValue<bool>("Core", "ShowDevelopers"));
	}

	private void ShowChatMainTopMenu(CCSPlayerController player)
	{
		var chatMenu = new ChatMenu(Localizer.ForPlayer(player, "top.menu.main.title"));

		chatMenu.AddMenuOption(Localizer.ForPlayer(player, "top.menu.rank"), (p, _) =>
		{
			RankTopHandler?.HandleRankTopCommand(p);
		}, !_loadedModules["Ranks"]);

		chatMenu.AddMenuOption(Localizer.ForPlayer(player, "top.menu.time"), (p, _) =>
		{
			TimeTopHandler?.HandleTimeTopCommand(p);
		}, !_loadedModules["Time"]);

		chatMenu.AddMenuOption(Localizer.ForPlayer(player, "top.menu.stats"), (p, _) =>
		{
			StatsTopHandler?.HandleStatsTopCommand(p);
		}, !_loadedModules["Stats"]);

		MenuManager.OpenChatMenu(player, chatMenu);
	}

	private void OnRankTopCommand(CCSPlayerController? player, CommandInfo command)
	{
		if (player == null || RankTopHandler == null || !_loadedModules["Ranks"]) return;
		RankTopHandler.HandleRankTopCommand(player, command);
	}

	private void OnTimeTopCommand(CCSPlayerController? player, CommandInfo command)
	{
		if (player == null || TimeTopHandler == null || !_loadedModules["Time"]) return;
		TimeTopHandler.HandleTimeTopCommand(player, command);
	}

	private void OnStatsTopCommand(CCSPlayerController? player, CommandInfo command)
	{
		if (player == null || StatsTopHandler == null || !_loadedModules["Stats"]) return;
		StatsTopHandler.HandleStatsTopCommand(player, command);
	}

	private void OnZenithCoreUnload(bool hotReload)
	{
		if (hotReload)
		{
			AddTimer(3.0f, () =>
			{
				try { File.SetLastWriteTime(Path.Combine(ModulePath), DateTime.Now); }
				catch (Exception ex) { Logger.LogError($"Failed to update file: {ex.Message}"); }
			});
		}
	}

	public override void Unload(bool hotReload)
	{
		_moduleServicesCapability?.Get()?.DisposeModule(this.GetType().Assembly);
	}

	public static string TruncateString(string input, int maxLength = 12)
	{
		if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
			return input;

		return string.Concat(input.AsSpan(0, maxLength), "...");
	}

	public IPlayerServices? GetZenithPlayer(CCSPlayerController? player)
	{
		if (player == null) return null;
		try { return _playerServicesCapability?.Get(player); }
		catch { return null; }
	}
}
