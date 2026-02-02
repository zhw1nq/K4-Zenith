namespace Zenith
{
    using System.Reflection;
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Attributes;
    using CounterStrikeSharp.API.Modules.Timers;
    using Dapper;
    using FluentMigrator.Runner;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using MySqlConnector;
    using Zenith.Models;

    [MinimumApiVersion(260)]
    public sealed partial class Plugin : BasePlugin
    {
        public Menu.KitsuneMenu Menu { get; private set; } = null!;
        public Database Database { get; private set; } = null!;
        public DateTime _lastStorageSave = DateTime.Now;

        public override void Load(bool hotReload)
        {
            Initialize_Config();

            Database = new Database(this);

            if (!Database.TestConnection())
            {
                Logger.LogError("Failed to connect to the database. Please check your configuration.");
                Server.ExecuteCommand($"css_plugins unload {Path.GetFileNameWithoutExtension(ModulePath)}");
                return;
            }

            string backupPath = Path.Combine(ModuleDirectory, $"DatabaseBackup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.sql");
            Task.Run(async () =>
            {
                try
                {
                    RunAutoMigrations(backupPath);

                    await Database.PurgeOldData();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Database migration failed: {ex.Message}");
                }
            }).Wait();

            Menu = new Menu.KitsuneMenu(this);

            Initialize_API();

            AddTimer(3.0f, () => // ! CSS Initialize some variables slow, so we need to wait a bit
            {
                Initialize_Events();
                Initialize_Settings();
                Initialize_Commands();
                Initialize_Placeholders();

                Player.RegisterModuleSettings(this, new Dictionary<string, object?>
                {
                    { "ShowClanTags", true },
                    { "ShowChatTags", true },
                    { "FreezeInMenu", GetCoreConfig<bool>("Core", "FreezeInMenu") },
                }, Localizer);

                RegisterListener<Listeners.OnTick>(() =>
                {
                    foreach (var player in Player.List.Values)
                    {
                        if (player.IsValid)
                            player.ShowCenterMessage();
                    }
                });

                if (hotReload)
                {
                    Logger.LogCritical(@"*");
                    Logger.LogCritical(@"*");
                    Logger.LogCritical(@"*    ██╗    ██╗ █████╗ ██████╗ ███╗   ██╗██╗███╗   ██╗ ██████╗");
                    Logger.LogCritical(@"*    ██║    ██║██╔══██╗██╔══██╗████╗  ██║██║████╗  ██║██╔════╝");
                    Logger.LogCritical(@"*    ██║ █╗ ██║███████║██████╔╝██╔██╗ ██║██║██╔██╗ ██║██║  ███╗");
                    Logger.LogCritical(@"*    ██║███╗██║██╔══██║██╔══██╗██║╚██╗██║██║██║╚██╗██║██║   ██║");
                    Logger.LogCritical(@"*    ╚███╔███╔╝██║  ██║██║  ██║██║ ╚████║██║██║ ╚████║╚██████╔╝");
                    Logger.LogCritical(@"*     ╚══╝╚══╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝╚═╝╚═╝  ╚═══╝ ╚═════╝");
                    Logger.LogCritical(@"*");
                    Logger.LogCritical(@"*    WARNING: Hot reloading Zenith Core currently breaks the plugin. Please restart the server instead.");
                    Logger.LogCritical(@"*    More information: https://github.com/roflmuffin/CounterStrikeSharp/issues/565");
                    Logger.LogCritical(@"*");

                    var players = Utilities.GetPlayers();

                    foreach (var player in players)
                    {
                        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
                        {
                            _ = new Player(this, player, true);
                        }
                    }

                    Player.LoadAllOnlinePlayerDataWithSingleQuery(this);
                }

                AddTimer(3.0f, () =>
                {
                    foreach (var player in Player.List.Values)
                    {
                        if (player.IsValid)
                            player.EnforcePluginValues();
                    }
                }, TimerFlags.REPEAT);

                AddTimer(60.0f, () =>
                {
                    int interval = GetCoreConfig<int>("Database", "AutoSaveInterval");
                    if (interval <= 0)
                        return;

                    if ((DateTime.Now - _lastStorageSave).TotalMinutes >= interval)
                    {
                        _lastStorageSave = DateTime.Now;
                        Task.Run(() => Player.SaveAllOnlinePlayerDataWithTransaction(this));
                    }
                }, TimerFlags.REPEAT);

                var overridePlugins = GetModuleConfigValue<List<string>>("Modular", "OverridePlugins");
                if (overridePlugins.Count != 0)
                {
                    Logger.LogInformation("Forcing low priority to: " + string.Join(", ", overridePlugins));

                    overridePlugins.ForEach(plugin =>
                    {
                        if (IsPluginExists(plugin))
                            Server.ExecuteCommand($"css_plugins unload {plugin}");
                    });

                    AddTimer(3.0f, () =>
                    {
                        overridePlugins.ForEach(plugin =>
                        {
                            if (IsPluginExists(plugin))
                                Server.ExecuteCommand($"css_plugins load {plugin}");
                        });
                    });

                    bool IsPluginExists(string plugin) => Directory.Exists(Path.Combine(ModuleDirectory, "..", plugin));
                }
            });
        }

        public override void Unload(bool hotReload)
        {
            _moduleServices?.InvokeZenithCoreUnload(hotReload);

            ConfigManager.Dispose();
            Player.Dispose(this);
            RemoveAllCommands();
            RemoveModulePlaceholders();
        }

        public void RunAutoMigrations(string backupPath, bool force = false)
        {
            var localService = new ServiceCollection()
                .AddFluentMigratorCore()
                .ConfigureRunner(rb =>
                {
                    rb.AddMySql5() // MySQL or MariaDB
                        .WithGlobalConnectionString(Database.GetConnectionString())
                        .ScanIn(Assembly.GetExecutingAssembly()).For.Migrations();
                })
                .AddLogging(lb => lb.AddFluentMigratorConsole())
                .BuildServiceProvider(false);

            var runner = localService.GetRequiredService<IMigrationRunner>();
            var migrations = runner.MigrationLoader.LoadMigrations();

            // Filter migrations based on modules
            if (!Directory.Exists(Path.Combine(ModuleDirectory, "..", "K4-Zenith-Bans")))
            {
                migrations = new SortedList<long, FluentMigrator.Infrastructure.IMigrationInfo>(
                    migrations
                    .Where(m => !m.Value.Migration.GetType().Name.StartsWith("bans", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(m => m.Key, m => m.Value));
            }

            if (!Directory.Exists(Path.Combine(ModuleDirectory, "..", "K4-Zenith-Stats")))
            {
                migrations = new SortedList<long, FluentMigrator.Infrastructure.IMigrationInfo>(
                    migrations
                    .Where(m => !m.Value.Migration.GetType().Name.StartsWith("stats", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(m => m.Key, m => m.Value));
            }

            // Only run migrations that haven't been applied yet
            var pendingMigrations = force ? [.. migrations] : migrations
                .Where(m => runner.HasMigrationsToApplyUp(m.Key))
                .ToList();

            if (pendingMigrations.Count != 0)
            {
                Task.Run(async () =>
                {
                    Logger.LogWarning("Creating a backup of the database before running migrations.");
                    await BackupDatabase(backupPath);
                    Logger.LogInformation($"Database backup completed to {backupPath}. Starting migrations.");

                    foreach (var migration in pendingMigrations)
                    {
                        Logger.LogInformation($"Running migration: {migration.Value.Migration.GetType().Name}");
                        runner.MigrateUp(migration.Key);  // Run migrations that are not in the VersionInfo table
                    }

                    Logger.LogInformation("Database migrations completed successfully.");
                }).Wait();
            }
            else
            {
                Logger.LogInformation("No migrations to apply. Database is up to date.");
            }
        }


        public async Task BackupDatabase(string outputPath)
        {
            using var connection = new MySqlConnection(Database.GetConnectionString());

            // Open connection
            await connection.OpenAsync();

            // Get database name
            var databaseName = await connection.ExecuteScalarAsync<string>("SELECT DATABASE();");

            // Start writing the SQL file
            using var writer = new StreamWriter(outputPath);

            // Write the schema (table structure)
            var tables = await connection.QueryAsync<string>("SHOW TABLES;");
            foreach (var table in tables)
            {
                var createTableQuery = await connection.ExecuteScalarAsync<string>($"SHOW CREATE TABLE `{table}`;");
                await writer.WriteLineAsync($"-- Schema for table `{table}`");
                await writer.WriteLineAsync(createTableQuery + ";");
                await writer.WriteLineAsync();

                // Write the data for each table
                var rows = await connection.QueryAsync($"SELECT * FROM `{table}`;");
                foreach (var row in rows)
                {
                    var insertQuery = $"INSERT INTO `{table}` VALUES (";
                    foreach (var prop in row)
                    {
                        var value = prop.Value == null ? "NULL" : $"'{MySqlHelper.EscapeString(prop.Value.ToString())}'";
                        insertQuery += $"{value},";
                    }
                    insertQuery = insertQuery.TrimEnd(',') + ");";
                    await writer.WriteLineAsync(insertQuery);
                }
                await writer.WriteLineAsync();
            }

            // Finish up
            await writer.WriteLineAsync($"-- Backup completed for `{databaseName}`.");
        }
    }
}