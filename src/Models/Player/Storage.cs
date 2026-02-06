using System.Text.Json;
using Microsoft.Extensions.Logging;
using Dapper;
using CounterStrikeSharp.API;
using Microsoft.Extensions.Localization;
using MySqlConnector;
using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;

namespace Zenith.Models;

public sealed partial class Player
{
    public static readonly string TABLE_PLAYER_SETTINGS = "zenith_player_settings";
    public static readonly string TABLE_PLAYER_STORAGE = "zenith_player_storage";

    public Dictionary<string, Dictionary<string, object?>> Settings = [];
    public Dictionary<string, Dictionary<string, object?>> Storage = [];
    public static readonly Dictionary<string, (Dictionary<string, object?> Settings, IStringLocalizer? Localizer)> moduleDefaultSettings = [];
    public static readonly Dictionary<string, (Dictionary<string, object?> Settings, IStringLocalizer? Localizer)> moduleDefaultStorage = [];

    public static void RegisterModuleSettings(Plugin plugin, Dictionary<string, object?> defaultSettings, IStringLocalizer? localizer = null)
    {
        string callerPlugin = CallerIdentifier.GetCallingPluginName();

        Task.Run(async () =>
        {
            try
            {
                await RegisterModuleDataAsync(plugin, callerPlugin, TABLE_PLAYER_SETTINGS);

                Server.NextWorldUpdate(() =>
                {
                    moduleDefaultSettings[callerPlugin] = (new Dictionary<string, object?>(defaultSettings), localizer);
                });
            }
            catch (Exception ex)
            {
                plugin.Logger.LogError($"Error registering module settings for {callerPlugin}: {ex.Message}");
            }
        });
    }

    public static void RegisterModuleStorage(Plugin plugin, Dictionary<string, object?> defaultStorage)
    {
        string callerPlugin = CallerIdentifier.GetCallingPluginName();

        Task.Run(async () =>
        {
            try
            {
                await RegisterModuleDataAsync(plugin, callerPlugin, TABLE_PLAYER_STORAGE);

                Server.NextWorldUpdate(() =>
                {
                    moduleDefaultStorage[callerPlugin] = (new Dictionary<string, object?>(defaultStorage), null);
                });
            }
            catch (Exception ex)
            {
                plugin.Logger.LogError($"Error registering module storage for {callerPlugin}: {ex.Message}");
            }
        });
    }

    private static async Task RegisterModuleDataAsync(Plugin plugin, string moduleID, string tableName)
    {
        string columnName = tableName == TABLE_PLAYER_SETTINGS ? $"{moduleID}.settings" : $"{moduleID}.storage";
        using var connection = plugin.Database.CreateConnection();
        await connection.OpenAsync();

        var columnExistsQuery = $@"
			SELECT COUNT(*)
			FROM INFORMATION_SCHEMA.COLUMNS
			WHERE TABLE_SCHEMA = DATABASE()
			AND TABLE_NAME = @TableName
			AND COLUMN_NAME = @ColumnName";

        var columnExists = await connection.ExecuteScalarAsync<int>(columnExistsQuery,
            new { TableName = tableName, ColumnName = columnName }) > 0;

        if (!columnExists)
        {
            var addColumnQuery = $@"
                ALTER TABLE `{tableName}`
                ADD COLUMN `{columnName}` JSON NULL";

            await connection.ExecuteAsync(addColumnQuery);
        }
    }

    public void SetSetting(string key, object? value, bool saveImmediately = false, string? moduleID = null)
        => SetData(key, value, Settings, saveImmediately, moduleID);

    public void SetStorage(string key, object? value, bool saveImmediately = false, string? moduleID = null)
        => SetData(key, value, Storage, saveImmediately, moduleID);

    public void SetData(string key, object? value, Dictionary<string, Dictionary<string, object?>> targetDict, bool saveImmediately = false, string? moduleID = null)
    {
        moduleID ??= CallerIdentifier.GetCallingPluginName();

        if (!Loaded)
            return;

        if (!targetDict.TryGetValue(moduleID, out var moduleDict))
        {
            moduleDict = new Dictionary<string, object?>();
            targetDict[moduleID] = moduleDict;
        }

        moduleDict[key] = value;

        if (saveImmediately)
        {
            SavePlayerData(moduleID);
        }
    }

    public T? GetSetting<T>(string key, string? moduleID = null)
        => GetData<T>(key, Settings, moduleID);

    public T? GetStorage<T>(string key, string? moduleID = null)
        => GetData<T>(key, Storage, moduleID);

    public T? GetData<T>(string key, Dictionary<string, Dictionary<string, object?>> targetDict, string? moduleID = null)
    {
        moduleID ??= CallerIdentifier.GetCallingPluginName();

        if (targetDict.TryGetValue(moduleID, out var moduleDict) && moduleDict.TryGetValue(key, out var value))
        {
            try
            {
                if (value is JsonElement jsonElement)
                {
                    return DeserializeJsonElement<T>(jsonElement);
                }
                else if (value is T typedValue)
                {
                    return typedValue;
                }
                else
                {
                    return (T?)Convert.ChangeType(value, typeof(T));
                }
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogError($"Failed to convert setting value for key '{key}' in module '{moduleID}' to type '{typeof(T).Name}'. Error: {ex.Message}");
            }
        }

        // if not found, search in all
        foreach (var module in targetDict)
        {
            if (module.Key.EndsWith(moduleID))
            {
                foreach (var item in module.Value)
                {
                    if (item.Key.EndsWith(key))
                    {
                        try
                        {
                            if (item.Value is JsonElement jsonElement)
                            {
                                return DeserializeJsonElement<T>(jsonElement);
                            }
                            else if (item.Value is T typedValue)
                            {
                                return typedValue;
                            }
                            else
                            {
                                return (T?)Convert.ChangeType(item.Value, typeof(T));
                            }
                        }
                        catch (Exception ex)
                        {
                            _plugin.Logger.LogError($"Failed to convert setting value for key '{key}' in module '{moduleID}' to type '{typeof(T).Name}'. Error: {ex.Message}");
                        }
                    }
                }
            }
        }

        return default;
    }

    private T? DeserializeJsonElement<T>(JsonElement element)
    {
        Type type = typeof(T);

        if (Nullable.GetUnderlyingType(type) != null)
        {
            type = Nullable.GetUnderlyingType(type)!;
        }

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Boolean:
                if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
                    return (T)(object)element.GetBoolean();
                else if (element.ValueKind == JsonValueKind.Number)
                    return (T)(object)(element.GetInt32() != 0);
                break;
            case TypeCode.Int32:
                if (element.ValueKind == JsonValueKind.Number)
                    return (T)(object)element.GetInt32();
                else if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
                    return (T)(object)(element.GetBoolean() ? 1 : 0);
                break;
            case TypeCode.String:
                if (element.ValueKind == JsonValueKind.String)
                    return (T)(object)element.GetString()!;
                break;
            case TypeCode.Double:
                if (element.ValueKind == JsonValueKind.Number)
                    return (T)(object)element.GetDouble();
                break;
            case TypeCode.Single:
                if (element.ValueKind == JsonValueKind.Number)
                    return (T)(object)element.GetSingle();
                break;
            case TypeCode.Int64:
                if (element.ValueKind == JsonValueKind.Number)
                    return (T)(object)element.GetInt64();
                break;
            case TypeCode.DateTime:
                if (element.ValueKind == JsonValueKind.String)
                    return (T)(object)element.GetDateTime();
                break;
            case TypeCode.Object:
                if (type == typeof(Guid) && element.ValueKind == JsonValueKind.String)
                    return (T)(object)element.GetGuid();
                else if (type == typeof(TimeSpan) && element.ValueKind == JsonValueKind.String)
                    return (T)(object)TimeSpan.Parse(element.GetString()!);
                else if (type.IsGenericType)
                {
                    if (type.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var listType = type.GetGenericArguments()[0];
                        var list = Activator.CreateInstance(type) as System.Collections.IList;
                        foreach (var item in element.EnumerateArray())
                        {
                            list!.Add(DeserializeJsonElement(item, listType));
                        }
                        return (T)list!;
                    }
                    else if (type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    {
                        var keyType = type.GetGenericArguments()[0];
                        var valueType = type.GetGenericArguments()[1];
                        var dict = Activator.CreateInstance(type) as System.Collections.IDictionary;
                        foreach (var item in element.EnumerateObject())
                        {
                            var key = Convert.ChangeType(item.Name, keyType);
                            var value = DeserializeJsonElement(item.Value, valueType);
                            dict!.Add(key, value);
                        }
                        return (T)dict!;
                    }
                }
                // For other complex types, use JsonSerializer
                return JsonSerializer.Deserialize<T>(element.GetRawText());
        }

        return default;
    }

    private object? DeserializeJsonElement(JsonElement element, Type type)
    {
        var method = typeof(Player).GetMethod(nameof(DeserializeJsonElement), BindingFlags.NonPublic | BindingFlags.Instance);
        var genericMethod = method!.MakeGenericMethod(type);
        return genericMethod.Invoke(this, [element]);
    }

    public static IStringLocalizer? GetModuleLocalizer(string moduleID)
    {
        if (moduleDefaultSettings.TryGetValue(moduleID, out var moduleInfo))
        {
            return moduleInfo.Localizer;
        }
        return null;
    }

    public async Task LoadPlayerData()
    {
        try
        {
            using var connection = _plugin.Database.CreateConnection();
            await connection.OpenAsync();

            await UpdateLastOnline(connection);

            await LoadDataAsync(connection, Settings, TABLE_PLAYER_SETTINGS, moduleDefaultSettings);
            await LoadDataAsync(connection, Storage, TABLE_PLAYER_STORAGE, moduleDefaultStorage);

            Loaded = true;

            Server.NextWorldUpdate(() =>
            {
                _plugin._moduleServices?.InvokeZenithPlayerLoaded(Controller!);

                if (_plugin._pluginServerPlaceholders.IsEmpty)
                    return;

                string joinFormat = _plugin.GetCoreConfig<string>("Modular", "JoinMessage");
                if (!string.IsNullOrEmpty(joinFormat))
                    _plugin._moduleServices?.PrintForAll(StringExtensions.ReplaceColorTags(_plugin.ReplacePlaceholders(Controller, joinFormat)), false);
            });
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError($"Error loading player data for player {SteamID}: {ex.Message}");

            Server.NextWorldUpdate(() =>
            {
                if (Controller?.IsValid == true && Controller.Connected == PlayerConnectedState.PlayerConnected)
                    _plugin.AddTimer(15.0f, () => Task.Run(() => LoadPlayerData()));
            });
        }
    }

    private async Task LoadDataAsync(MySqlConnection connection, Dictionary<string, Dictionary<string, object?>> targetDict, string tableName, Dictionary<string, (Dictionary<string, object?> Settings, IStringLocalizer? Localizer)> defaults)
    {
        var query = $@"
			SELECT * FROM `{tableName}`
			WHERE `steam_id` = @SteamID;";
        var result = await connection.QueryFirstOrDefaultAsync(query, new { SteamID = SteamID.ToString() });

        Server.NextWorldUpdate(() =>
        {
            if (result != null)
            {
                foreach (var property in result)
                {
                    if (property.Value != null && (property.Key.EndsWith(".settings") || property.Key.EndsWith(".storage")))
                    {
                        var moduleID = property.Key.Split('.')[0];
                        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(property.Value.ToString());
                        if (!targetDict.ContainsKey(moduleID))
                        {
                            targetDict[moduleID] = new Dictionary<string, object?>();
                        }
                        foreach (var item in data)
                        {
                            targetDict[moduleID][item.Key] = item.Value;
                        }
                    }
                }
            }

            ApplyDefaultValues(defaults, targetDict);
        });
    }

    private async Task UpdateLastOnline(MySqlConnection connection)
    {
        var query = $@"
			INSERT INTO `{TABLE_PLAYER_SETTINGS}` (`steam_id`, `name`, `last_online`)
			VALUES (@SteamID, @Name, NOW())
			ON DUPLICATE KEY UPDATE `name` = @Name, `last_online` = NOW();

			INSERT INTO `{TABLE_PLAYER_STORAGE}` (`steam_id`, `name`, `last_online`)
			VALUES (@SteamID, @Name, NOW())
			ON DUPLICATE KEY UPDATE `name` = @Name, `last_online` = NOW();";

        await connection.ExecuteAsync(query, new { SteamID = SteamID.ToString(), Name = Name });
    }

    private static void ApplyDefaultValues(Dictionary<string, (Dictionary<string, object?> Settings, IStringLocalizer? Localizer)> defaults, Dictionary<string, Dictionary<string, object?>> targetDict)
    {
        foreach (var module in defaults)
        {
            var moduleID = module.Key;
            if (!targetDict.TryGetValue(moduleID, out var moduleDict))
            {
                moduleDict = [];
                targetDict[moduleID] = moduleDict;
            }

            foreach (var item in module.Value.Settings)
            {
                if (!moduleDict.ContainsKey(item.Key))
                {
                    moduleDict[item.Key] = item.Value;
                }
            }
        }
    }

    public void SavePlayerData(string? moduleID = null)
    {
        if (!Loaded)
            return;

        Task.Run(async () =>
        {
            await SaveDataAsync(Settings, TABLE_PLAYER_SETTINGS, moduleID);
            await SaveDataAsync(Storage, TABLE_PLAYER_STORAGE, moduleID);
        });
    }

    private async Task SaveDataAsync(Dictionary<string, Dictionary<string, object?>> targetDict, string tableName, string? moduleID = null)
    {
        if (!Loaded)
            return;

        try
        {
            using var connection = _plugin.Database.CreateConnection();
            await connection.OpenAsync();

            var dataToSave = new Dictionary<string, string>();

            if (moduleID != null)
            {
                if (targetDict.TryGetValue(moduleID, out var moduleData))
                {
                    dataToSave[$"{moduleID}.{(tableName == TABLE_PLAYER_STORAGE ? "storage" : "settings")}"] = JsonSerializer.Serialize(moduleData);
                }
            }
            else
            {
                foreach (var module in targetDict)
                {
                    dataToSave[$"{module.Key}.{(tableName == TABLE_PLAYER_STORAGE ? "storage" : "settings")}"] = JsonSerializer.Serialize(module.Value);
                }
            }

            if (dataToSave.Count == 0)
            {
                return; // No data to save
            }

            var columns = string.Join(", ", dataToSave.Keys.Select(k => $"`{MySqlHelper.EscapeString(k)}`"));
            var parameters = string.Join(", ", dataToSave.Keys.Select(k => $"@p_{k.Replace("-", "_")}"));
            var updateStatements = string.Join(", ", dataToSave.Keys.Select(k => $"`{MySqlHelper.EscapeString(k)}` = @p_{k.Replace("-", "_")}"));

            var query = $@"
				INSERT INTO `{tableName}` (`steam_id`, `name`, {columns})
				VALUES (@p_SteamID, @p_Name, {parameters})
				ON DUPLICATE KEY UPDATE `name` = @p_Name, {updateStatements};";

            var queryParams = new DynamicParameters();
            queryParams.Add("@p_SteamID", SteamID.ToString());
            queryParams.Add("@p_Name", Name);
            foreach (var item in dataToSave)
            {
                queryParams.Add($"@p_{item.Key.Replace("-", "_")}", item.Value);
            }

            await connection.ExecuteAsync(query, queryParams);
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError("Error saving player data for player {SteamID}: {ErrorMessage}", SteamID, ex.Message);
        }
    }

    public void ResetModuleSettings()
        => ResetModuleData(false);
    public void ResetModuleStorage()
        => ResetModuleData(true);

    private void ResetModuleData(bool isStorage)
    {
        string callerPlugin = CallerIdentifier.GetCallingPluginName();

        if (!Loaded)
            return;

        var defaults = isStorage ? moduleDefaultStorage : moduleDefaultSettings;
        var targetDict = isStorage ? Storage : Settings;

        if (defaults.TryGetValue(callerPlugin, out var defaultData))
        {
            if (!targetDict.TryGetValue(callerPlugin, out var moduleDict))
            {
                moduleDict = new Dictionary<string, object?>();
                targetDict[callerPlugin] = moduleDict;
            }

            foreach (var item in defaultData.Settings)
            {
                moduleDict[item.Key] = item.Value;
            }
            SavePlayerData(callerPlugin);
        }
        else
        {
            _plugin.Logger.LogWarning($"Attempted to reset non-existent module data: {callerPlugin}");
        }
    }

    public static async Task ResetModuleStorageAll(Plugin plugin, Player? caller, string moduleKey)
    {
        string realTarget;
        switch (moduleKey.ToLower())
        {
            case "all":
                realTarget = "all";
                break;
            case "rank":
                realTarget = "K4-Zenith-Ranks";
                break;
            case "stat":
                realTarget = "K4-Zenith-Stats";
                break;
            case "time":
                realTarget = "K4-Zenith-TimeStats";
                break;
            default:
                if (moduleDefaultStorage.ContainsKey(moduleKey))
                    realTarget = moduleKey;
                else
                    Server.NextWorldUpdate(() => caller?.Print("Invalid argument. Usage: css_zresetall [all|rank|stat|time|module-unique-name]"));
                return;
        }

        using var connection = plugin.Database.CreateConnection();
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            List<string> modulesToReset = realTarget == "all"
                ? [.. moduleDefaultStorage.Keys]
                : moduleDefaultStorage.ContainsKey(realTarget) ? [realTarget] : [];

            foreach (var moduleId in modulesToReset)
            {
                var defaultValues = moduleDefaultStorage[moduleId].Settings;
                if (defaultValues.Count == 0)
                {
                    plugin.Logger.LogWarning("Attempted to reset non-existent module data: {ModuleId}", moduleId);
                    continue;
                }

                string columnName = $"{moduleId}.storage";
                string defaultJson = JsonSerializer.Serialize(defaultValues);

                var query = $@"
					UPDATE `{TABLE_PLAYER_STORAGE}`
					SET `{columnName}` = @DefaultJson;";

                await connection.ExecuteAsync(query, new { DefaultJson = defaultJson }, transaction);

                if (moduleId == "K4-Zenith-Stats")
                {
                    await connection.ExecuteAsync($"TRUNCATE TABLE `zenith_map_stats`;", transaction);
                    await connection.ExecuteAsync($"TRUNCATE TABLE `zenith_weapon_stats`;", transaction);
                }

                foreach (var player in List.Values)
                {
                    if (player.Storage.ContainsKey(moduleId))
                    {
                        player.Storage[moduleId] = new Dictionary<string, object?>(defaultValues);
                    }
                }
            }

            await transaction.CommitAsync();

            Server.NextWorldUpdate(() =>
            {
                plugin._moduleServices?.InvokeZenithStorageReset(realTarget);

                plugin.Logger.LogWarning("Storage reset completed. Target: {Target}, Caller: {User} ({SteamId})",
                    realTarget.Equals("all", StringComparison.CurrentCultureIgnoreCase) ? "all modules" : realTarget,
                    caller?.Name ?? "CONSOLE",
                    caller?.SteamID ?? 0);

                string resetTarget = realTarget.Equals("all", StringComparison.CurrentCultureIgnoreCase)
                    ? plugin.Localizer.ForPlayer(caller?.Controller, "reset.storage.all.modules")
                    : realTarget;

                caller?.Print(plugin.Localizer.ForPlayer(caller?.Controller, "reset.storage.success", resetTarget));
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            plugin.Logger.LogError("Error in ResetModuleStorageAll: {Message}", ex.Message);
            Server.NextWorldUpdate(() => caller?.Print($"An error occurred while resetting {realTarget} storage."));
        }
    }

    public static void LoadAllOnlinePlayerDataWithSingleQuery(Plugin plugin)
    {
        var steamIds = new List<string>();
        var playerList = new List<CCSPlayerController>();

        foreach (var player in Utilities.GetPlayers())
        {
            if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV && !List.Values.Any(p => p.SteamID.ToString() == player.SteamID.ToString()))
            {
                steamIds.Add(player.SteamID.ToString());
                playerList.Add(player);
            }
        }

        if (steamIds.Count == 0)
            return;

        try
        {
            Task.Run(async () =>
            {
                using var connection = plugin.Database.CreateConnection();
                await connection.OpenAsync();

                var query = $@"
				SELECT
					s.steam_id,
					s.last_online AS settings_last_online,
					st.last_online AS storage_last_online,
					{string.Join(",", moduleDefaultSettings.Keys.Select(k => $"s.`{MySqlHelper.EscapeString(k)}.settings` AS `{k}_settings`"))},
					{string.Join(",", moduleDefaultStorage.Keys.Select(k => $"st.`{MySqlHelper.EscapeString(k)}.storage` AS `{k}_storage`"))}
				FROM
					`{TABLE_PLAYER_SETTINGS}` s
				LEFT JOIN
					`{TABLE_PLAYER_STORAGE}` st ON s.steam_id = st.steam_id
				WHERE
					s.steam_id IN @SteamIDs;";

                var results = await connection.QueryAsync<IDictionary<string, object>>(query, new { SteamIDs = steamIds });

                Server.NextWorldUpdate(() =>
                {
                    foreach (var result in results)
                    {
                        var steamId = result["steam_id"].ToString();
                        var player = List.Values.FirstOrDefault(p => p.SteamID.ToString() == steamId);

                        if (player == null)
                            continue;

                        LoadPlayerDataFromResult(player, result, plugin);
                        player.Loaded = true;

                        plugin._moduleServices?.InvokeZenithPlayerLoaded(player.Controller!);
                    }
                });
            });
        }
        catch (Exception ex)
        {
            plugin.Logger.LogError($"An error occurred while querying the database: {ex.Message}");

            Server.NextWorldUpdate(() =>
            {
                plugin.AddTimer(15.0f, () => Task.Run(() => LoadAllOnlinePlayerDataWithSingleQuery(plugin)));
                return;
            });
        }
    }

    private static void LoadPlayerDataFromResult(Player player, IDictionary<string, object> result, Plugin plugin)
    {
        foreach (var module in moduleDefaultSettings.Keys)
        {
            var settingsKey = $"{module}_settings";
            if (result.TryGetValue(settingsKey, out object? value) && value != null)
            {
                try
                {
                    var moduleData = JsonSerializer.Deserialize<Dictionary<string, object>>(value.ToString()!);
                    if (moduleData != null)
                    {
                        if (!player.Settings.TryGetValue(module, out var moduleDict))
                        {
                            moduleDict = new Dictionary<string, object?>();
                            player.Settings[module] = moduleDict;
                        }

                        foreach (var item in moduleData)
                        {
                            moduleDict[item.Key] = item.Value;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    plugin.Logger.LogError($"Error deserializing settings data for module {module}: {ex.Message}");
                }
            }
        }

        foreach (var module in moduleDefaultStorage.Keys)
        {
            var storageKey = $"{module}_storage";
            if (result.TryGetValue(storageKey, out object? value) && value != null)
            {
                try
                {
                    var moduleData = JsonSerializer.Deserialize<Dictionary<string, object>>(value.ToString()!);
                    if (moduleData != null)
                    {
                        if (!player.Storage.TryGetValue(module, out var moduleDict))
                        {
                            moduleDict = new Dictionary<string, object?>();
                            player.Storage[module] = moduleDict;
                        }

                        foreach (var item in moduleData)
                        {
                            moduleDict[item.Key] = item.Value;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    plugin.Logger.LogError($"Error deserializing storage data for module {module}: {ex.Message}");
                }
            }
        }

        ApplyDefaultValues(moduleDefaultSettings, player.Settings);
        ApplyDefaultValues(moduleDefaultStorage, player.Storage);
    }

    public static async Task SaveAllOnlinePlayerDataWithTransaction(Plugin plugin)
    {
        var playerDataToSave = new Dictionary<string, (Dictionary<string, string> Settings, Dictionary<string, string> Storage)>();

        foreach (var player in List.Values)
        {
            if (!player.Loaded)
                continue;

            var settingsData = new Dictionary<string, string>();
            var storageData = new Dictionary<string, string>();

            ProcessPlayerData(player.Settings, settingsData, "settings");
            ProcessPlayerData(player.Storage, storageData, "storage");

            playerDataToSave[player.SteamID.ToString()] = (settingsData, storageData);
        }

        if (playerDataToSave.Count == 0)
            return;

        try
        {
            using var connection = plugin.Database.CreateConnection();
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                foreach (var isStorage in new[] { false, true })
                {
                    string tableName = isStorage ? TABLE_PLAYER_STORAGE : TABLE_PLAYER_SETTINGS;

                    foreach (var playerData in playerDataToSave)
                    {
                        var steamId = playerData.Key;
                        var data = isStorage ? playerData.Value.Storage : playerData.Value.Settings;

                        if (data.Count == 0)
                            continue;

                        await SavePlayerDataToDatabase(connection, tableName, steamId, data, transaction);
                    }
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            plugin.Logger.LogError($"An error occurred while saving player data: {ex.Message}");
        }
    }

    private static void ProcessPlayerData(Dictionary<string, Dictionary<string, object?>> sourceDict, Dictionary<string, string> targetDict, string dataType)
    {
        foreach (var moduleGroup in sourceDict)
        {
            var moduleID = moduleGroup.Key;
            var moduleData = moduleGroup.Value;
            targetDict[$"{moduleID}.{dataType}"] = JsonSerializer.Serialize(moduleData);
        }
    }

    private static async Task SavePlayerDataToDatabase(MySqlConnection connection, string tableName, string steamId, Dictionary<string, string> data, MySqlTransaction transaction)
    {
        var columns = string.Join(", ", data.Keys.Select(k => $"`{MySqlHelper.EscapeString(k)}`"));
        var parameters = string.Join(", ", data.Keys.Select((_, i) => $"@param{i}"));
        var updateStatements = string.Join(", ", data.Keys.Select((k, i) => $"`{MySqlHelper.EscapeString(k)}` = @param{i}"));

        var query = $@"
        INSERT INTO `{tableName}` (`steam_id`, {columns})
        VALUES (@steamId, {parameters})
        ON DUPLICATE KEY UPDATE {updateStatements};";

        var queryParams = new DynamicParameters();
        queryParams.Add("@steamId", steamId);
        for (int i = 0; i < data.Count; i++)
        {
            queryParams.Add($"@param{i}", data.ElementAt(i).Value);
        }

        await connection.ExecuteAsync(query, queryParams, transaction);
    }

    public static void Dispose(Plugin plugin)
    {
        // QUAN TRỌNG: Phải đợi save hoàn tất trước khi process kết thúc
        Task.Run(async () =>
        {
            await SaveAllOnlinePlayerDataWithTransaction(plugin);
        }).Wait();
    }

    public static void ResetOfflineData(Plugin plugin, ulong steamId, string tableName)
    {
        string callerPlugin = CallerIdentifier.GetCallingPluginName();

        Task.Run(async () =>
        {
            try
            {
                using var connection = plugin.Database.CreateConnection();
                await connection.OpenAsync();

                var query = $@"
					UPDATE `{TABLE_PLAYER_STORAGE}`
					SET `{callerPlugin}.{(tableName == TABLE_PLAYER_STORAGE ? "storage" : "settings")}` = @DefaultJson
					WHERE `steam_id` = @SteamID;";

                var defaultValues = tableName == TABLE_PLAYER_STORAGE ? moduleDefaultStorage[callerPlugin].Settings : moduleDefaultSettings[callerPlugin].Settings;
                var defaultJson = JsonSerializer.Serialize(defaultValues);

                await connection.ExecuteAsync(query, new { DefaultJson = defaultJson, SteamID = steamId });
            }
            catch (Exception ex)
            {
                plugin.Logger.LogError($"Error resetting offline storage for player {steamId}: {ex.Message}");
            }
        });
    }

    public static async Task SetOfflineData(Plugin plugin, ulong steamId, string tableName, Dictionary<string, object?> data)
    {
        string callerPlugin = CallerIdentifier.GetCallingPluginName();

        try
        {
            using var connection = plugin.Database.CreateConnection();
            await connection.OpenAsync();

            var query = $@"
				UPDATE `{TABLE_PLAYER_STORAGE}`
				SET `{callerPlugin}.{tableName}` = @Data
				WHERE `steam_id` = @SteamID;";

            await connection.ExecuteAsync(query, new { Data = JsonSerializer.Serialize(data), SteamID = steamId });
        }
        catch (Exception ex)
        {
            plugin.Logger.LogError($"Error setting offline storage for player {steamId}: {ex.Message}");
        }
    }

    public static async Task<T?> GetOfflineData<T>(Plugin plugin, ulong steamId, string tableName, string key)
    {
        string callerPlugin = CallerIdentifier.GetCallingPluginName();

        try
        {
            using var connection = plugin.Database.CreateConnection();
            await connection.OpenAsync();

            var query = $@"
				SELECT `{callerPlugin}.{tableName}`
				FROM `{TABLE_PLAYER_STORAGE}`
				WHERE `steam_id` = @SteamID;";

            var result = await connection.ExecuteScalarAsync<string>(query, new { SteamID = steamId });

            if (result != null)
            {
                using var document = JsonDocument.Parse(result);
                var data = document.RootElement;

                if (data.TryGetProperty(key, out var element))
                {
                    try
                    {
                        return Type.GetTypeCode(typeof(T)) switch
                        {
                            TypeCode.Int32 => (T)(object)element.GetInt32(),
                            TypeCode.Int64 => (T)(object)element.GetInt64(),
                            TypeCode.Single => (T)(object)element.GetSingle(),
                            TypeCode.Double => (T)(object)element.GetDouble(),
                            TypeCode.String => (T)(object)element.GetString()!,
                            TypeCode.Boolean => (T)(object)element.GetBoolean(),
                            _ => JsonSerializer.Deserialize<T>(element.GetRawText()),
                        };
                    }
                    catch (Exception ex)
                    {
                        plugin.Logger.LogError($"Error converting value for key '{key}' to type '{typeof(T).Name}': {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            plugin.Logger.LogError(ex, "Error getting offline storage data for player {SteamID}", steamId);
        }

        return default;
    }
}
