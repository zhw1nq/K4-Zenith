using System.Collections.Concurrent;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Timers;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using ZenithAPI;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Commands;
using Menu;
using Menu.Enums;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Translations;

namespace Zenith_CustomTags;

[MinimumApiVersion(260)]
public class Plugin : BasePlugin
{
    private const string MODULE_ID = "CustomTags";

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public override string ModuleName => $"K4-Zenith | {MODULE_ID}";
    public override string ModuleAuthor => "K4ryuu @ KitsuneLab";
    public override string ModuleVersion => "1.0.10";

    private PlayerCapability<IPlayerServices>? _playerServicesCapability;
    private PluginCapability<IModuleServices>? _moduleServicesCapability;

    private IZenithEvents? _zenithEvents;
    private IModuleServices? _moduleServices;

    private readonly Dictionary<CCSPlayerController, IPlayerServices> _playerCache = [];
    private readonly Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Timers.Timer> _removalTimers = [];

    // Top 100 ranking cache: SteamID -> (Placement, CacheTime)
    private readonly ConcurrentDictionary<ulong, (int Placement, DateTime CacheTime)> _top100Cache = new();
    private DateTime _top100CacheTriggered = DateTime.MinValue;
    private const int TOP100_LIMIT = 100;
    private const string SKILLGROUP_BASE = "202601";
    private const string MODULE_FULL_ID = "K4-Zenith-CustomTags";

    public KitsuneMenu Menu { get; private set; } = null!;
    public IModuleConfigAccessor _coreAccessor = null!;

    private Dictionary<string, TagConfig>? _tagConfigs;
    private Dictionary<string, PredefinedTagConfig>? _predefinedConfigs;

    public override void Load(bool hotReload)
    {
        // Precache skillgroup icons using OnServerPrecacheResources (correct method for panorama resources)
        RegisterListener<Listeners.OnServerPrecacheResources>(manifest =>
        {
            // Precache native skillgroups (0-18)
            for (int i = 0; i <= 18; i++)
            {
                manifest.AddResource($"panorama/images/icons/skillgroups/skillgroup{i}.svg");
            }

            // Precache custom Top 1-100 skillgroups (2026011-202601100)
            for (int i = 1; i <= TOP100_LIMIT; i++)
            {
                manifest.AddResource($"panorama/images/icons/skillgroups/skillgroup{SKILLGROUP_BASE}{i}.vsvg");
            }

            Logger.LogInformation("Precached {Count} custom skillgroup icons", TOP100_LIMIT);
        });

        // Register OnTick for scoreboard skillgroup updates
        RegisterListener<Listeners.OnTick>(UpdateSkillgroupsOnScoreboard);
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        try
        {
            _playerServicesCapability = new("zenith:player-services");
            _moduleServicesCapability = new("zenith:module-services");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize Zenith API: {ex.Message}");
            Logger.LogInformation("Please check if Zenith is installed, configured and loaded correctly.");
            Server.ExecuteCommand($"css_plugins unload {Path.GetFileNameWithoutExtension(ModulePath)}");
            return;
        }

        _moduleServices = _moduleServicesCapability.Get();
        if (_moduleServices == null)
        {
            Logger.LogError("Failed to get Module-Services API for Zenith.");
            Server.ExecuteCommand($"css_plugins unload {Path.GetFileNameWithoutExtension(ModulePath)}");
            return;
        }

        _zenithEvents = _moduleServices.GetEventHandler();
        if (_zenithEvents != null)
        {
            _zenithEvents.OnZenithPlayerLoaded += OnZenithPlayerLoaded;
            _zenithEvents.OnZenithPlayerUnloaded += OnZenithPlayerUnloaded;
            _zenithEvents.OnZenithCoreUnload += OnZenithCoreUnload;
        }
        else
        {
            Logger.LogError("Failed to get Zenith event handler.");
        }

        Menu = new KitsuneMenu(this);
        _coreAccessor = _moduleServices.GetModuleConfigAccessor();

        _moduleServices!.RegisterModuleStorage(new Dictionary<string, object?>
        {
            { "ChoosenTag", "Default" }
        });

        _moduleServices?.RegisterModuleConfig("Config", "TagRemovalDelay", "Delay in seconds before removing tags when permissions are lost (0 to disable)", 5.0f);

        EnsureConfigFileExists();
        EnsurePredefinedConfigFileExists();

        _moduleServices?.RegisterModuleCommands(["tags", "tag"], "Change player tag configuration", (player, info) =>
        {
            if (player == null) return;
            ShowTagSelectionMenu(player);
        }, CommandUsage.CLIENT_ONLY);

        if (hotReload)
        {
            _moduleServices?.LoadAllOnlinePlayerData();

            var players = Utilities.GetPlayers();
            foreach (var player in players)
            {
                if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
                    OnZenithPlayerLoaded(player);
            }

            // Initial cache on hot reload
            CacheTop100();
        }

        // Start Top100 cache timer (every 60 seconds)
        AddTimer(60.0f, CacheTop100, TimerFlags.REPEAT);

        Logger.LogInformation("Zenith {0} module successfully registered.", MODULE_ID);
    }

    private void ShowTagSelectionMenu(CCSPlayerController player)
    {
        _tagConfigs ??= GetTagConfigs();
        _predefinedConfigs ??= GetPredefinedTagConfigs();

        List<MenuItem> items = [];
        List<string> configKeys = [];
        HashSet<string> availableConfigs = new HashSet<string>();

        if (_tagConfigs.TryGetValue("all", out var allConfig) && allConfig.AvailableConfigs != null)
        {
            availableConfigs.UnionWith(allConfig.AvailableConfigs);
        }

        if (_tagConfigs.TryGetValue(player.SteamID.ToString(), out var playerConfig))
        {
            if (playerConfig.AvailableConfigs != null)
            {
                availableConfigs.UnionWith(playerConfig.AvailableConfigs);
            }
        }

        foreach (var kvp in _tagConfigs)
        {
            if (CheckPermissionOrSteamID(player, kvp.Key) && kvp.Value.AvailableConfigs != null)
            {
                availableConfigs.UnionWith(kvp.Value.AvailableConfigs);
            }
        }



        foreach (var configName in availableConfigs)
        {
            if (_predefinedConfigs.TryGetValue(configName, out var config))
            {
                items.Add(new MenuItem(MenuItemType.Button, [new MenuValue(config.Name)]));
                configKeys.Add(configName);
            }
        }

        if (items.Count == 1)
            items.Clear();

        try
        {
            if (_coreAccessor.GetValue<bool>("Core", "CenterMenuMode"))
            {
                ShowCenterTagSelectionMenu(player, items, configKeys);
            }
            else
            {
                ShowChatTagSelectionMenu(player, configKeys);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error showing tag selection menu: {ex.Message}");
        }
    }

    private void ShowCenterTagSelectionMenu(CCSPlayerController player, List<MenuItem> items, List<string> configKeys)
    {
        if (Menu == null)
        {
            Logger.LogError("Menu object is null. Cannot show center tag selection menu.");
            return;
        }

        Menu.ShowScrollableMenu(player, Localizer.ForPlayer(player, "customtags.menu.title"), items, (buttons, menu, selected) =>
        {
            if (selected == null) return;

            if (menu.Option >= 0 && menu.Option < configKeys.Count)
            {
                string selectedConfigKey = configKeys[menu.Option];

                if (buttons == MenuButtons.Select)
                {
                    ApplySelectedConfig(player, selectedConfigKey);
                }
            }
        }, false, _coreAccessor.GetValue<bool>("Core", "FreezeInMenu") && (GetZenithPlayer(player)?.GetSetting<bool>("FreezeInMenu", "K4-Zenith") ?? true), 5, disableDeveloper: !_coreAccessor.GetValue<bool>("Core", "ShowDevelopers"));
    }

    private void ShowChatTagSelectionMenu(CCSPlayerController player, List<string> configKeys)
    {
        ChatMenu tagMenu = new ChatMenu(Localizer.ForPlayer(player, "customtags.menu.title"));

        foreach (var configKey in configKeys)
        {
            string displayName = configKey;
            if (_predefinedConfigs?.TryGetValue(configKey, out var config) == true)
            {
                displayName = config.Name;
            }
            else if (configKey == "none")
            {
                displayName = Localizer.ForPlayer(player, "customtags.menu.none");
            }
            else if (configKey == "default")
            {
                displayName = Localizer.ForPlayer(player, "customtags.menu.default");
            }

            tagMenu.AddMenuOption($"{ChatColors.Gold}{displayName}", (p, o) =>
            {
                ApplySelectedConfig(p, configKey);
            });
        }

        MenuManager.OpenChatMenu(player, tagMenu);
    }
    private void ApplySelectedConfig(CCSPlayerController player, string selectedConfigKey)
    {
        var zenithPlayer = GetZenithPlayer(player);
        if (zenithPlayer == null)
        {
            Logger.LogError($"Failed to get player services for {player.PlayerName}");
            return;
        }

        if (_predefinedConfigs?.TryGetValue(selectedConfigKey, out var selectedPredefinedConfig) == true)
        {
            zenithPlayer.SetStorage("ChoosenTag", selectedConfigKey);
            ApplyConfig(zenithPlayer, selectedPredefinedConfig);
            _moduleServices?.PrintForPlayer(player, Localizer.ForPlayer(player, "customtags.applied.config", selectedPredefinedConfig.Name));
        }
        else
        {
            _moduleServices?.PrintForPlayer(player, $"Invalid tag configuration: {selectedConfigKey}");
        }
    }

    private void EnsureConfigFileExists()
    {
        string configPath = Path.Combine(ModuleDirectory, "tags.json");
        if (!File.Exists(configPath))
        {
            var defaultConfig = new Dictionary<string, TagConfig>
            {
                ["all"] = new TagConfig
                {
                    AvailableConfigs = ["ranking"]
                },
                ["@css/tgs-vip"] = new TagConfig
                {
                    DefaultPreset = "vip",
                    AvailableConfigs = ["vip"]
                },
                ["@css/tgs-svip"] = new TagConfig
                {
                    DefaultPreset = "svip",
                    AvailableConfigs = ["svip"]
                },
                ["@css/tgs-staff"] = new TagConfig
                {
                    DefaultPreset = "staff",
                    AvailableConfigs = ["staff", "developer", "owner"]
                },
                ["@css/tgs-dev"] = new TagConfig
                {
                    DefaultPreset = "developer",
                    AvailableConfigs = ["ranking", "vip", "svip", "staff", "developer", "owner"]
                },
                ["@css/tgs-owner"] = new TagConfig
                {
                    DefaultPreset = "owner",
                    AvailableConfigs = ["ranking", "vip", "svip", "staff", "developer", "owner"]
                }
            };

            var jsonConfig = JsonSerializer.Serialize(defaultConfig, _jsonOptions);
            var jsonWithComments = @"// This configuration file defines tag settings for players.
// You can use the following keys to target specific players or groups:
// - ""all"": Applies to all players
// - ""#GroupName"": Applies to players in a specific group (e.g., ""#Owner"", ""#Admin"")
// - ""@Permission"": Applies to players with a specific permission (e.g., ""@css/admin"")
// - ""SteamID"": Applies to a specific player by their Steam ID

" + jsonConfig;

            File.WriteAllText(configPath, jsonWithComments);
        }
    }

    private void EnsurePredefinedConfigFileExists()
    {
        string configPath = Path.Combine(ModuleDirectory, "predefined_tags.json");
        if (!File.Exists(configPath))
        {
            var defaultConfig = new Dictionary<string, PredefinedTagConfig>
            {
                ["ranking"] = new PredefinedTagConfig
                {
                    Name = "Ranking"
                },
                ["vip"] = new PredefinedTagConfig
                {
                    Name = "VIP",
                    ChatColor = "yellow",
                    NameColor = "yellow",
                    NameTag = "{yellow}VIP » "
                },
                ["svip"] = new PredefinedTagConfig
                {
                    Name = "SVIP",
                    ChatColor = "gold",
                    NameColor = "gold",
                    NameTag = "{gold}SVIP » "
                },
                ["staff"] = new PredefinedTagConfig
                {
                    Name = "STAFF",
                    ChatColor = "blue",
                    NameColor = "blue",
                    NameTag = "{blue}STAFF » "
                },
                ["developer"] = new PredefinedTagConfig
                {
                    Name = "DEVELOPER",
                    ChatColor = "blue",
                    NameColor = "blue",
                    NameTag = "{blue}DEVELOPER » "
                },
                ["owner"] = new PredefinedTagConfig
                {
                    Name = "OWNER",
                    ChatColor = "lightred",
                    NameColor = "lightred",
                    NameTag = "{lightred}OWNER » "
                }
            };

            var jsonConfig = JsonSerializer.Serialize(defaultConfig, _jsonOptions);
            var jsonWithComments = @"// This configuration file defines predefined tag configurations that can be applied to players.
// These configurations can be referenced in the 'AvailableConfigs' list in the main tags.json file.

" + jsonConfig;

            File.WriteAllText(configPath, jsonWithComments);
        }
    }

    private Dictionary<string, TagConfig> GetTagConfigs()
    {
        try
        {
            string configPath = Path.Combine(ModuleDirectory, "tags.json");
            string json = File.ReadAllText(configPath);
            string strippedJson = StripComments(json);
            return JsonSerializer.Deserialize<Dictionary<string, TagConfig>>(strippedJson, _jsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading tag configs: {ex.Message}");
            return [];
        }
    }

    private Dictionary<string, PredefinedTagConfig> GetPredefinedTagConfigs()
    {
        try
        {
            string configPath = Path.Combine(ModuleDirectory, "predefined_tags.json");
            string json = File.ReadAllText(configPath);
            string strippedJson = StripComments(json);
            return JsonSerializer.Deserialize<Dictionary<string, PredefinedTagConfig>>(strippedJson, _jsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading predefined tag configs: {ex.Message}");
            return [];
        }
    }

    private static string StripComments(string json)
    {
        if (string.IsNullOrEmpty(json))
            return json;

        var result = new System.Text.StringBuilder(json.Length);
        using (var reader = new StringReader(json))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmedLine = line.TrimStart();
                if (!trimmedLine.StartsWith("//"))
                {
                    result.AppendLine(line);
                }
            }
        }
        return result.ToString();
    }

    private void ApplyTagConfig(CCSPlayerController player)
    {
        try
        {
            var zenithPlayer = GetZenithPlayer(player);
            if (zenithPlayer == null)
            {
                Logger.LogError($"Failed to get player services for {player.PlayerName}");
                return;
            }

            _tagConfigs ??= GetTagConfigs();
            _predefinedConfigs ??= GetPredefinedTagConfigs();

            string choosenTag = zenithPlayer.GetStorage<string>("ChoosenTag") ?? "Default";

            if (_removalTimers.TryGetValue(player, out var existingTimer))
            {
                existingTimer.Kill();
                _removalTimers.Remove(player);
            }

            if (choosenTag == "None")
            {
                ApplyNullConfig(zenithPlayer);
                return;
            }

            if (choosenTag != "Default" && _predefinedConfigs.TryGetValue(choosenTag, out var chosenPredefinedConfig))
            {
                bool existsForUser = false;
                foreach (var kvp in _tagConfigs)
                {
                    if (CheckPermissionOrSteamID(player, kvp.Key) && kvp.Value.AvailableConfigs != null && kvp.Value.AvailableConfigs.Contains(choosenTag))
                    {
                        ApplyConfig(zenithPlayer, chosenPredefinedConfig);
                        existsForUser = true;
                    }
                }

                if (!existsForUser)
                {
                    float removalDelay = _coreAccessor.GetValue<float>("Config", "TagRemovalDelay");
                    if (removalDelay > 0)
                    {
                        _removalTimers[player] = AddTimer(removalDelay, () =>
                        {
                            if (!CheckTagAvailability(player, choosenTag))
                            {
                                zenithPlayer.SetStorage("ChoosenTag", "Default");
                                ApplyTagConfig(player);
                                _removalTimers.Remove(player);
                            }
                        });
                        return;
                    }
                    else
                    {
                        zenithPlayer.SetStorage("ChoosenTag", "Default");
                    }
                }
                else
                    return;
            }

            bool configApplied = false;
            List<string> availableConfigs = [];

            if (_tagConfigs.TryGetValue("all", out var allConfig))
            {
                // Apply DefaultPreset for "all" if specified
                if (!string.IsNullOrEmpty(allConfig.DefaultPreset) && _predefinedConfigs.TryGetValue(allConfig.DefaultPreset, out var allPreset))
                {
                    ApplyConfig(zenithPlayer, allPreset);
                    zenithPlayer.SetStorage("ChoosenTag", allConfig.DefaultPreset);
                    configApplied = true;
                }
                if (allConfig.AvailableConfigs != null)
                {
                    availableConfigs.AddRange(allConfig.AvailableConfigs);
                }
            }

            foreach (var kvp in _tagConfigs)
            {
                if (kvp.Key == "all")
                    continue;

                if (CheckPermissionOrSteamID(player, kvp.Key))
                {
                    var config = kvp.Value;

                    // Apply DefaultPreset if specified
                    if (!string.IsNullOrEmpty(config.DefaultPreset) && _predefinedConfigs.TryGetValue(config.DefaultPreset, out var defaultPreset))
                    {
                        ApplyConfig(zenithPlayer, defaultPreset);
                        zenithPlayer.SetStorage("ChoosenTag", config.DefaultPreset);
                        configApplied = true;
                    }

                    if (config.AvailableConfigs != null)
                    {
                        availableConfigs.AddRange(config.AvailableConfigs);
                    }
                    break;
                }
            }

            if (!configApplied && availableConfigs.Count > 0)
            {
                foreach (var configName in availableConfigs)
                {
                    if (_predefinedConfigs.TryGetValue(configName, out var availablePredefinedConfig))
                    {
                        ApplyConfig(zenithPlayer, availablePredefinedConfig);
                        _moduleServices?.PrintForPlayer(player, Localizer.ForPlayer(player, "customtags.applied.default_predefined", availablePredefinedConfig.Name));
                        zenithPlayer.SetStorage("ChoosenTag", configName);
                        configApplied = true;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error applying tag config for player {player.PlayerName}: {ex.Message}");
        }
    }

    private bool CheckTagAvailability(CCSPlayerController player, string tagName)
    {
        if (_tagConfigs == null) return false;

        foreach (var kvp in _tagConfigs)
        {
            if (CheckPermissionOrSteamID(player, kvp.Key) && kvp.Value.AvailableConfigs != null && kvp.Value.AvailableConfigs.Contains(tagName))
            {
                return true;
            }
        }
        return false;
    }

    private static bool CheckPermissionOrSteamID(CCSPlayerController player, string key)
    {
        if (key.StartsWith('#'))
        {
            return AdminManager.PlayerInGroup(player, key);
        }

        AdminData? adminData = AdminManager.GetPlayerAdminData(player);
        if (adminData != null)
        {
            string permissionKey = key.StartsWith('@') ? key : "@" + key;
            if (adminData.Flags.Any(flagEntry =>
                flagEntry.Value.Contains(permissionKey, StringComparer.OrdinalIgnoreCase) ||
                flagEntry.Value.Any(flag => permissionKey.StartsWith(flag, StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }
        }

        return SteamID.TryParse(key, out SteamID? keySteamID) &&
               keySteamID != null &&
               Equals(keySteamID, new SteamID(player.SteamID));
    }

    private static void ApplyConfig(IPlayerServices zenithPlayer, PredefinedTagConfig config)
    {
        if (!string.IsNullOrEmpty(config.ChatColor))
            zenithPlayer.SetChatColor(config.ChatColor);

        if (!string.IsNullOrEmpty(config.NameColor))
            zenithPlayer.SetNameColor(config.NameColor);

        if (!string.IsNullOrEmpty(config.NameTag))
            zenithPlayer.SetNameTag(config.NameTag);
    }

    private void OnMapStart(string mapName)
    {
        // Precache skillgroup icons during map load
        PrecacheSkillgroups();

        // Initial cache on map start
        CacheTop100();
    }

    private void PrecacheSkillgroups()
    {
        // Precache default Top 1-100 skillgroups
        for (int i = 1; i <= TOP100_LIMIT; i++)
        {
            string path = $"panorama/images/icons/skillgroups/skillgroup{SKILLGROUP_BASE}{i}.vsvg";
            Server.PrecacheModel(path);
        }

        // Precache additional skillgroups from predefined_tags.json
        _predefinedConfigs ??= GetPredefinedTagConfigs();
        if (_predefinedConfigs != null)
        {
            foreach (var config in _predefinedConfigs.Values)
            {
                if (!string.IsNullOrEmpty(config.SkillgroupID))
                {
                    // Check if it's outside 1-100 range (already precached)
                    if (!int.TryParse(config.SkillgroupID.Replace(SKILLGROUP_BASE, ""), out int id) || id < 1 || id > TOP100_LIMIT)
                    {
                        string path = $"panorama/images/icons/skillgroups/skillgroup{config.SkillgroupID}.vsvg";
                        Server.PrecacheModel(path);
                        Logger.LogInformation("Precached custom skillgroup: {Path}", path);
                    }
                }
            }
        }
    }

    private void CacheTop100()
    {
        // Prevent too frequent updates
        if ((DateTime.UtcNow - _top100CacheTriggered).TotalSeconds < 3)
            return;

        var onlinePlayers = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected)
            .ToList();

        if (onlinePlayers.Count == 0)
            return;

        _top100CacheTriggered = DateTime.UtcNow;

        Task.Run(async () =>
        {
            try
            {
                string? connectionString = _moduleServices?.GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                    return;

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

                var results = await connection.QueryAsync<(string SteamId, int Placement)>(
                    query,
                    new { SteamIds = steamIdString }
                );

                Logger.LogInformation("[Top100Cache] Query returned {Count} results for {PlayerCount} online players",
                    results.Count(), onlinePlayers.Count);

                foreach (var (SteamId, Placement) in results)
                {
                    if (ulong.TryParse(SteamId, out ulong steamId) && Placement <= TOP100_LIMIT)
                    {
                        _top100Cache[steamId] = (Placement, DateTime.UtcNow);
                        Logger.LogInformation("[Top100Cache] Cached {SteamId} at position {Placement}", steamId, Placement);
                    }
                    else if (ulong.TryParse(SteamId, out ulong steamIdOutside))
                    {
                        // Remove from cache if player is no longer in Top 100
                        _top100Cache.TryRemove(steamIdOutside, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to cache Top 100: {Error}", ex.Message);
            }
        });
    }

    private void UpdateSkillgroupsOnScoreboard()
    {
        foreach (var kvp in _playerCache)
        {
            var player = kvp.Key;
            var zenithPlayer = kvp.Value;

            if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
                continue;

            string? chosenTag = zenithPlayer.GetStorage<string>("ChoosenTag", MODULE_FULL_ID);
            ApplySkillgroup(player, chosenTag);
        }
    }

    private void ApplySkillgroup(CCSPlayerController player, string? chosenTag)
    {
        _predefinedConfigs ??= GetPredefinedTagConfigs();
        if (_predefinedConfigs == null)
            return;

        string? skillgroupId = null;

        // Priority Logic based on user spec
        if (chosenTag == "ranking" || chosenTag == "Default")
        {
            // Case 1, 2, 4: !tags = Ranking
            // Check if player is in Top 100
            if (_top100Cache.TryGetValue(player.SteamID, out var cacheEntry))
            {
                // Player is in Top 100 - use ranking skillgroup
                skillgroupId = $"{SKILLGROUP_BASE}{cacheEntry.Placement}";
                Logger.LogInformation("[Skillgroup] Player {Name} ({SteamID}) is Top {Placement}, using skillgroup {ID}",
                    player.PlayerName, player.SteamID, cacheEntry.Placement, skillgroupId);
            }
            else
            {
                Logger.LogInformation("[Skillgroup] Player {Name} ({SteamID}) NOT in Top100 cache (cache size: {Size})",
                    player.PlayerName, player.SteamID, _top100Cache.Count);
                // Player NOT in Top 100 - fallback to permission skillgroup
                // Find highest permission preset that has SkillgroupID
                _tagConfigs ??= GetTagConfigs();
                if (_tagConfigs != null)
                {
                    foreach (var kvp in _tagConfigs)
                    {
                        if (kvp.Key == "all")
                            continue;

                        if (CheckPermissionOrSteamID(player, kvp.Key))
                        {
                            // Found permission - get default preset's skillgroup
                            if (!string.IsNullOrEmpty(kvp.Value.DefaultPreset) &&
                                _predefinedConfigs.TryGetValue(kvp.Value.DefaultPreset, out var preset) &&
                                !string.IsNullOrEmpty(preset.SkillgroupID))
                            {
                                skillgroupId = preset.SkillgroupID;
                                break;
                            }
                        }
                    }
                }
            }
        }
        else if (!string.IsNullOrEmpty(chosenTag) && _predefinedConfigs.TryGetValue(chosenTag, out var selectedPreset))
        {
            // Case 3: Player explicitly chose a permission preset
            if (!string.IsNullOrEmpty(selectedPreset.SkillgroupID))
            {
                skillgroupId = selectedPreset.SkillgroupID;
            }
        }

        // Apply skillgroup to scoreboard
        if (!string.IsNullOrEmpty(skillgroupId) && int.TryParse(skillgroupId, out int skillgroupInt))
        {
            player.CompetitiveWins = 10; // Required to show rank
            player.CompetitiveRanking = skillgroupInt;
            player.CompetitiveRankType = 12; // Competitive mode (uses skillgroup path)

            // Force UI update with SetStateChanged
            Server.NextFrame(() =>
            {
                if (player?.IsValid == true && player.PlayerPawn?.IsValid == true)
                {
                    Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveRanking");
                    Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveWins");
                    Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveRankType");
                }
            });
        }
    }

    private static void ApplyNullConfig(IPlayerServices player)
    {
        player.SetChatColor(null);
        player.SetNameColor(null);
        player.SetNameTag(null);
    }

    private void OnZenithPlayerLoaded(CCSPlayerController player)
    {
        var zenithPlayer = GetZenithPlayer(player);
        if (zenithPlayer == null)
        {
            Logger.LogError($"Failed to get player services for {player.PlayerName}");
            return;
        }

        _playerCache[player] = zenithPlayer;
        ApplyTagConfig(player);
    }

    private void OnZenithPlayerUnloaded(CCSPlayerController player)
    {
        if (_removalTimers.TryGetValue(player, out var timer))
        {
            timer.Kill();
            _removalTimers.Remove(player);
        }
        _playerCache.Remove(player);
    }

    public override void Unload(bool hotReload)
    {
        foreach (var timer in _removalTimers.Values)
        {
            timer.Kill();
        }
        _removalTimers.Clear();
        _playerCache.Clear();

        _moduleServicesCapability?.Get()?.DisposeModule(this.GetType().Assembly);
    }

    private void OnZenithCoreUnload(bool hotReload)
    {
        _playerCache.Clear();

        if (hotReload)
        {
            AddTimer(3.0f, () =>
            {
                try { File.SetLastWriteTime(ModulePath, DateTime.Now); }
                catch (Exception ex) { Logger.LogError($"Failed to update file: {ex.Message}"); }
            });
        }
    }

    public IPlayerServices? GetZenithPlayer(CCSPlayerController? player)
    {
        if (player == null) return null;
        try { return _playerServicesCapability?.Get(player); }
        catch { return null; }
    }
}

public class TagConfig
{
    public string? DefaultPreset { get; set; }
    public string? SkillgroupID { get; set; }
    public List<string> AvailableConfigs { get; set; } = [];
}

public class PredefinedTagConfig
{
    public string Name { get; set; } = "";
    public string? ChatColor { get; set; }
    public string? NameColor { get; set; }
    public string? NameTag { get; set; }
    public string? SkillgroupID { get; set; }
}