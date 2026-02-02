namespace Zenith
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Commands;
	using Microsoft.Extensions.Logging;
	using Zenith.Models;

	public sealed partial class Plugin : BasePlugin
	{
		public void Initialize_Commands() // ? Decide whether or not its needed
		{
			RegisterZenithCommand("css_placeholderlist", "List all active placeholders in Zenith", (CCSPlayerController? player, CommandInfo command) =>
			{
				ListAllPlaceholders(player: player);
			}, CommandUsage.CLIENT_AND_SERVER, permission: "@zenith/placeholders");

			RegisterZenithCommand("css_commandlist", "List all active commands in Zenith", (CCSPlayerController? player, CommandInfo command) =>
			{
				ListAllCommands(player: player);
			}, CommandUsage.CLIENT_AND_SERVER, permission: "@zenith/commands");

			RegisterZenithCommand("css_zreload", "Reload Zenith configurations manually", (CCSPlayerController? player, CommandInfo command) =>
			{
				ConfigManager.ReloadAllConfigs();
				Player.Find(player)?.Print("Zenith configurations reloaded.");
			}, CommandUsage.CLIENT_AND_SERVER, permission: "@zenith/reload");

			RegisterZenithCommand("css_zresetall", "Reset Zenith storages for all players", (CCSPlayerController? player, CommandInfo command) =>
			{
				Player? caller = Player.Find(player);
				string argument = command.GetArg(1);

				Task.Run(async () => await Player.ResetModuleStorageAll(this, caller, argument));
			}, CommandUsage.CLIENT_AND_SERVER, 1, "[all|rank|stat|time]", permission: "@zenith/resetall");

			RegisterZenithCommand("css_zmigrate", "Migrate other supported plugins' sql data to Zenith", (CCSPlayerController? player, CommandInfo command) =>
			{
				Task.Run(async () => await MigrateOldData());
			}, CommandUsage.SERVER_ONLY, permission: "@zenith/root");

			RegisterZenithCommand("css_zdebug", "Toggle CallerIdentifier debug mode", (CCSPlayerController? player, CommandInfo command) =>
			{
				CallerIdentifier.DebugMode = !CallerIdentifier.DebugMode;
				Logger.LogWarning($"CallerIdentifier DebugMode is now: {CallerIdentifier.DebugMode}");
				Player.Find(player)?.Print($"CallerIdentifier DebugMode is now: {CallerIdentifier.DebugMode}");
			}, CommandUsage.CLIENT_AND_SERVER, permission: "@zenith/root");
		}
	}
}