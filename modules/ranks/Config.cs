
using CounterStrikeSharp.API.Core;
using ZenithAPI;

namespace Zenith_Ranks;

public sealed partial class Plugin : BasePlugin
{
    public IModuleConfigAccessor _configAccessor = null!;

    private void RegisterConfigs()
    {
        if (_moduleServices == null) return;

        // Register Commands
        _moduleServices.RegisterModuleConfig("Commands", "RankCommands", "Commands to show rank", new List<string> { "rank", "level" });

        // Register Settings
        _moduleServices.RegisterModuleConfig("Settings", "StartPoints", "Starting points for new players", 200);
        _moduleServices.RegisterModuleConfig("Settings", "WarmupPoints", "Allow earning points during warmup", false);
        _moduleServices.RegisterModuleConfig("Settings", "PointSummaries", "Show point summaries", true);
        _moduleServices.RegisterModuleConfig("Settings", "EnableRequirementMessages", "Enable or disable messages the messages for points being disabled", true);
        _moduleServices.RegisterModuleConfig("Settings", "MinPlayers", "Minimum players required to earn points", 5);
        _moduleServices.RegisterModuleConfig("Settings", "PointsForBots", "Allow earning points for killing bots", false);
        _moduleServices.RegisterModuleConfig("Settings", "FFAMode", "Free-for-all mode", false);
        _moduleServices.RegisterModuleConfig("Settings", "ScoreboardScoreSync", "Sync points to the scoreboard", false);
        _moduleServices.RegisterModuleConfig("Settings", "VipMultiplier", "Points multiplier for VIP", 1.25);
        _moduleServices.RegisterModuleConfig("Settings", "SvipMultiplier", "Points multiplier for SVIP", 1.5);
        _moduleServices.RegisterModuleConfig("Settings", "DynamicDeathPoints", "Use dynamic death point deduction", true);
        _moduleServices.RegisterModuleConfig("Settings", "DynamicDeathPointsMaxMultiplier", "Maximum multiplier for dynamic death points", 5.50);
        _moduleServices.RegisterModuleConfig("Settings", "DynamicDeathPointsMinMultiplier", "Minimum multiplier for dynamic death points", 2);
        _moduleServices.RegisterModuleConfig("Settings", "ShowRankChanges", "Show center screen notification on rank change", true);
        _moduleServices.RegisterModuleConfig("Settings", "ExtendedDeathMessages", "Use extended death messages (includes enemy name and points)", true);
        _moduleServices.RegisterModuleConfig("Settings", "VIPFlags", "VIP flags for points multiplier", new List<string> { "@css/vip" });
        _moduleServices.RegisterModuleConfig("Settings", "SVIPFlags", "SVIP flags for points multiplier", new List<string> { "@css/svip" });
        _moduleServices.RegisterModuleConfig("Settings", "AllowNegativePoints", "Allow player points to go below zero", true);

        // Negative Recovery
        _moduleServices.RegisterModuleConfig("Settings", "NegativeRecoveryEnabled", "Enable gradual recovery for negative points", false);
        _moduleServices.RegisterModuleConfig("Settings", "NegativeRecoveryInterval", "Interval in minutes for negative recovery", 5);
        _moduleServices.RegisterModuleConfig("Settings", "NegativeRecoveryAmount", "Points recovered per interval when negative", 50);

        // Register Minigame
        _moduleServices.RegisterModuleConfig("Minigame", "Enabled", "Enable math minigame", true);
        _moduleServices.RegisterModuleConfig("Minigame", "IntervalMinutes", "Interval between math challenges (minutes)", 10);
        _moduleServices.RegisterModuleConfig("Minigame", "RewardPoints", "Points reward for correct answer", 50);
        _moduleServices.RegisterModuleConfig("Minigame", "DisplayDuration", "How long to show the math question on center screen (seconds)", 30);
        _moduleServices.RegisterModuleConfig("Minigame", "WinnerDisplayDuration", "How long to show winner announcement (seconds)", 7);
        _moduleServices.RegisterModuleConfig("Minigame", "MaxDifficulty", "Max number range for operands", 200);
        _moduleServices.RegisterModuleConfig("Minigame", "MinOperands", "Minimum operands in expression", 5);
        _moduleServices.RegisterModuleConfig("Minigame", "MaxOperands", "Maximum operands in expression", 10);
        _moduleServices.RegisterModuleConfig("Minigame", "AnswerCommands", "Commands to answer math challenge", new List<string> { "aw" });

        // Register Points
        _moduleServices.RegisterModuleConfig("Points", "Death", "Points on death", -60);
        _moduleServices.RegisterModuleConfig("Points", "Kill", "Points per kill", 8);
        _moduleServices.RegisterModuleConfig("Points", "Headshot", "Bonus points for headshot", 8);
        _moduleServices.RegisterModuleConfig("Points", "Penetrated", "Bonus points for wallbang kill", 15);
        _moduleServices.RegisterModuleConfig("Points", "NoScope", "Bonus points for no-scope kill", 10);
        _moduleServices.RegisterModuleConfig("Points", "Thrusmoke", "Bonus points for kill through smoke", 8);
        _moduleServices.RegisterModuleConfig("Points", "BlindKill", "Bonus points for blind kill", 8);
        _moduleServices.RegisterModuleConfig("Points", "TeamKill", "Points for team kill", -750);
        _moduleServices.RegisterModuleConfig("Points", "Suicide", "Points for suicide", -750);
        _moduleServices.RegisterModuleConfig("Points", "Assist", "Points for assist", 6);
        _moduleServices.RegisterModuleConfig("Points", "AssistFlash", "Points for flash assist", 3);
        _moduleServices.RegisterModuleConfig("Points", "TeamKillAssist", "Points for team kill assist", -150);
        _moduleServices.RegisterModuleConfig("Points", "TeamKillAssistFlash", "Points for team kill flash assist", -100);
        _moduleServices.RegisterModuleConfig("Points", "RoundWin", "Points for round win", 25);
        _moduleServices.RegisterModuleConfig("Points", "RoundLose", "Points for round loss", -35);
        _moduleServices.RegisterModuleConfig("Points", "MVP", "Points for MVP", 20);
        _moduleServices.RegisterModuleConfig("Points", "BombDrop", "Points for dropping the bomb", 0);
        _moduleServices.RegisterModuleConfig("Points", "BombPickup", "Points for picking up the bomb", 0);
        _moduleServices.RegisterModuleConfig("Points", "BombDefused", "Points for defusing the bomb", 15);
        _moduleServices.RegisterModuleConfig("Points", "BombDefusedOthers", "Points for teammates when bomb is defused", 5);
        _moduleServices.RegisterModuleConfig("Points", "BombPlant", "Points for planting the bomb", 10);
        _moduleServices.RegisterModuleConfig("Points", "BombExploded", "Points for bomb explosion", 10);
        _moduleServices.RegisterModuleConfig("Points", "BombExplosionDeath", "Points for dying to bomb explosion (instead of suicide penalty)", -10);
        _moduleServices.RegisterModuleConfig("Points", "HostageHurt", "Points for hurting a hostage", -2);
        _moduleServices.RegisterModuleConfig("Points", "HostageKill", "Points for killing a hostage", -20);
        _moduleServices.RegisterModuleConfig("Points", "HostageRescue", "Points for rescuing a hostage", 15);
        _moduleServices.RegisterModuleConfig("Points", "HostageRescueAll", "Bonus points for rescuing all hostages", 10);
        _moduleServices.RegisterModuleConfig("Points", "LongDistanceKill", "Bonus points for long distance kill", 20);
        _moduleServices.RegisterModuleConfig("Points", "LongDistance", "Distance threshold for long distance kill (units)", 40);
        _moduleServices.RegisterModuleConfig("Points", "SecondsBetweenKills", "Time (seconds) between kills for multi-kill streak", 0);
        _moduleServices.RegisterModuleConfig("Points", "RoundEndKillStreakReset", "Reset kill streak on round end", true);
        _moduleServices.RegisterModuleConfig("Points", "DoubleKill", "Points for double kill", 10);
        _moduleServices.RegisterModuleConfig("Points", "TripleKill", "Points for triple kill", 15);
        _moduleServices.RegisterModuleConfig("Points", "Domination", "Points for domination (4 kills)", 22);
        _moduleServices.RegisterModuleConfig("Points", "Rampage", "Points for rampage (5 kills)", 75);
        _moduleServices.RegisterModuleConfig("Points", "MegaKill", "Points for mega kill (6 kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "Ownage", "Points for ownage (7 kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "UltraKill", "Points for ultra kill (8 kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "KillingSpree", "Points for killing spree (9 kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "MonsterKill", "Points for monster kill (10 kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "Unstoppable", "Points for unstoppable (11 kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "GodLike", "Points for godlike (12+ kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "GrenadeKill", "Points for HE grenade kill", 15);
        _moduleServices.RegisterModuleConfig("Points", "InfernoKill", "Points for fire kill (Molotov/Incendiary)", 10);
        _moduleServices.RegisterModuleConfig("Points", "ImpactKill", "Points for impact damage kill (e.g. grenade impact)", 150);
        _moduleServices.RegisterModuleConfig("Points", "TaserKill", "Points for taser kill (Zeus)", 20);
        _moduleServices.RegisterModuleConfig("Points", "KnifeKill", "Points for knife kill", 30);
        _moduleServices.RegisterModuleConfig("Points", "PlaytimeInterval", "Interval for playtime bonus points (minutes), 0 to disable", 10);
        _moduleServices.RegisterModuleConfig("Points", "PlaytimePoints", "Points awarded per playtime interval", 10);

        // Get the config accessor
        _configAccessor = _moduleServices.GetModuleConfigAccessor();
    }
}