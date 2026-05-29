
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
        _moduleServices.RegisterModuleConfig("Settings", "StartPoints", "Starting points for new players", -250);
        _moduleServices.RegisterModuleConfig("Settings", "WarmupPoints", "Allow earning points during warmup", false);
        _moduleServices.RegisterModuleConfig("Settings", "PointSummaries", "Show point summaries", true);
        _moduleServices.RegisterModuleConfig("Settings", "EnableRequirementMessages", "Enable or disable messages the messages for points being disabled", true);
        _moduleServices.RegisterModuleConfig("Settings", "MinPlayers", "Minimum players required to earn points", 5);
        _moduleServices.RegisterModuleConfig("Settings", "PointsForBots", "Allow earning points for killing bots", false);
        _moduleServices.RegisterModuleConfig("Settings", "FFAMode", "Free-for-all mode", false);
        _moduleServices.RegisterModuleConfig("Settings", "ScoreboardScoreSync", "Sync points to the scoreboard", false);
        _moduleServices.RegisterModuleConfig("Settings", "VipMultiplier", "Points multiplier for VIP", 1.5);
        _moduleServices.RegisterModuleConfig("Settings", "SvipMultiplier", "Points multiplier for SVIP", 2.0);
        _moduleServices.RegisterModuleConfig("Settings", "DynamicDeathPoints", "Use dynamic death point deduction based on points gap between killer and victim", true);
        _moduleServices.RegisterModuleConfig("Settings", "DynamicDeathPointsMaxMultiplier", "Maximum multiplier when killed by a much weaker player (shameful death)", 7.0);
        _moduleServices.RegisterModuleConfig("Settings", "DynamicDeathPointsMinMultiplier", "Minimum multiplier when killed by a stronger player (normal death)", 2.0);
        _moduleServices.RegisterModuleConfig("Settings", "ShowRankChanges", "Show center screen notification on rank change", true);
        _moduleServices.RegisterModuleConfig("Settings", "ExtendedDeathMessages", "Use extended death messages (includes enemy name and points)", true);
        _moduleServices.RegisterModuleConfig("Settings", "VIPFlags", "VIP flags for points multiplier", new List<string> { "@css/vip" });
        _moduleServices.RegisterModuleConfig("Settings", "SVIPFlags", "SVIP flags for points multiplier", new List<string> { "@css/svip" });
        _moduleServices.RegisterModuleConfig("Settings", "AllowNegativePoints", "Allow player points to go below zero", true);

        // Negative Recovery
        _moduleServices.RegisterModuleConfig("Settings", "NegativeRecoveryEnabled", "Enable gradual recovery for negative points", false);
        _moduleServices.RegisterModuleConfig("Settings", "NegativeRecoveryInterval", "Interval in minutes for negative recovery", 5);
        _moduleServices.RegisterModuleConfig("Settings", "NegativeRecoveryAmount", "Points recovered per interval when negative", 50);

        // Register Minigame - General
        _moduleServices.RegisterModuleConfig("Minigame", "Enabled", "Enable minigame challenges", true);
        _moduleServices.RegisterModuleConfig("Minigame", "IntervalMinutes", "Interval between challenges (minutes)", 5);
        _moduleServices.RegisterModuleConfig("Minigame", "DisplayDuration", "How long to show the question on center screen (seconds)", 30);
        _moduleServices.RegisterModuleConfig("Minigame", "WinnerDisplayDuration", "How long to show winner announcement (seconds)", 7);
        _moduleServices.RegisterModuleConfig("Minigame", "AnswerCommands", "Commands to answer challenge", new List<string> { "aw" });

        // Minigame - Rewards (Top 3)
        _moduleServices.RegisterModuleConfig("Minigame", "RewardPoints", "Base points reward for 1st place", 150);
        _moduleServices.RegisterModuleConfig("Minigame", "Reward2ndPercent", "Percent of RewardPoints for 2nd place (0 to disable)", 75);
        _moduleServices.RegisterModuleConfig("Minigame", "Reward3rdPercent", "Percent of RewardPoints for 3rd place (0 to disable)", 50);
        _moduleServices.RegisterModuleConfig("Minigame", "GracePeriodSeconds", "Extra seconds after 1st correct answer for others to answer", 5);

        // Minigame - Penalties
        _moduleServices.RegisterModuleConfig("Minigame", "MaxWrongAttempts", "Max wrong attempts per player per challenge", 3);
        _moduleServices.RegisterModuleConfig("Minigame", "WrongPenalty1", "Points penalty for 1st wrong attempt", 150);
        _moduleServices.RegisterModuleConfig("Minigame", "WrongPenalty2", "Points penalty for 2nd wrong attempt", 300);
        _moduleServices.RegisterModuleConfig("Minigame", "WrongPenalty3", "Points penalty for 3rd wrong attempt", 500);
        _moduleServices.RegisterModuleConfig("Minigame", "CooldownSeconds", "Cooldown between wrong attempts (seconds)", 2.0);

        // Minigame - Math Mode
        _moduleServices.RegisterModuleConfig("Minigame", "MaxDifficulty", "Max number range for operands", 100);
        _moduleServices.RegisterModuleConfig("Minigame", "MinOperands", "Minimum operands in expression", 2);
        _moduleServices.RegisterModuleConfig("Minigame", "MaxOperands", "Maximum operands in expression", 4);

        // Minigame - Game Modes
        _moduleServices.RegisterModuleConfig("Minigame", "EnableMathMode", "Enable math challenge mode", true);
        _moduleServices.RegisterModuleConfig("Minigame", "EnableReactionMode", "Enable reaction (type random string) mode", true);
        _moduleServices.RegisterModuleConfig("Minigame", "EnableUnscrambleMode", "Enable unscramble (CS2 map/weapon names) mode", true);
        _moduleServices.RegisterModuleConfig("Minigame", "ReactionStringLength", "Length of random string for reaction mode", 5);

        // Minigame - Auto-scaling Difficulty
        _moduleServices.RegisterModuleConfig("Minigame", "AutoScaleEnabled", "Scale difficulty and rewards based on player count", true);
        _moduleServices.RegisterModuleConfig("Minigame", "AutoScaleThreshold", "Player count threshold to start scaling up", 10);
        _moduleServices.RegisterModuleConfig("Minigame", "AutoScaleMaxMultiplier", "Max difficulty/reward multiplier at full server", 2.0);

        // Register Points
        _moduleServices.RegisterModuleConfig("Points", "Death", "Points on death", -40);
        _moduleServices.RegisterModuleConfig("Points", "Kill", "Points per kill", 10);
        _moduleServices.RegisterModuleConfig("Points", "Headshot", "Bonus points for headshot", 10);
        _moduleServices.RegisterModuleConfig("Points", "Penetrated", "Bonus points for wallbang kill", 15);
        _moduleServices.RegisterModuleConfig("Points", "NoScope", "Bonus points for no-scope kill", 15);
        _moduleServices.RegisterModuleConfig("Points", "Thrusmoke", "Bonus points for kill through smoke", 15);
        _moduleServices.RegisterModuleConfig("Points", "BlindKill", "Bonus points for blind kill", 10);
        _moduleServices.RegisterModuleConfig("Points", "TeamKill", "Points for team kill", -200);
        _moduleServices.RegisterModuleConfig("Points", "Suicide", "Points for suicide", -750);
        _moduleServices.RegisterModuleConfig("Points", "Assist", "Points for assist", 8);
        _moduleServices.RegisterModuleConfig("Points", "AssistFlash", "Points for flash assist", 5);
        _moduleServices.RegisterModuleConfig("Points", "TeamKillAssist", "Points for team kill assist", -100);
        _moduleServices.RegisterModuleConfig("Points", "TeamKillAssistFlash", "Points for team kill flash assist", -100);
        _moduleServices.RegisterModuleConfig("Points", "RoundWin", "Points for round win", 25);
        _moduleServices.RegisterModuleConfig("Points", "RoundLose", "Points for round loss", -50);
        _moduleServices.RegisterModuleConfig("Points", "MVP", "Points for MVP", 35);
        _moduleServices.RegisterModuleConfig("Points", "BombDrop", "Points for dropping the bomb", 0);
        _moduleServices.RegisterModuleConfig("Points", "BombPickup", "Points for picking up the bomb", 0);
        _moduleServices.RegisterModuleConfig("Points", "BombDefused", "Points for defusing the bomb", 10);
        _moduleServices.RegisterModuleConfig("Points", "BombDefusedOthers", "Points for teammates when bomb is defused", 10);
        _moduleServices.RegisterModuleConfig("Points", "BombPlant", "Points for planting the bomb", 10);
        _moduleServices.RegisterModuleConfig("Points", "BombExploded", "Points for bomb explosion", 25);
        _moduleServices.RegisterModuleConfig("Points", "BombExplosionDeath", "Points for dying to bomb explosion (instead of suicide penalty)", -10);
        _moduleServices.RegisterModuleConfig("Points", "HostageHurt", "Points for hurting a hostage", -2);
        _moduleServices.RegisterModuleConfig("Points", "HostageKill", "Points for killing a hostage", -20);
        _moduleServices.RegisterModuleConfig("Points", "HostageRescue", "Points for rescuing a hostage", 15);
        _moduleServices.RegisterModuleConfig("Points", "HostageRescueAll", "Bonus points for rescuing all hostages", 10);
        _moduleServices.RegisterModuleConfig("Points", "LongDistanceKill", "Bonus points for long distance kill", 10);
        _moduleServices.RegisterModuleConfig("Points", "LongDistance", "Distance threshold for long distance kill (units)", 35);
        _moduleServices.RegisterModuleConfig("Points", "SecondsBetweenKills", "Time (seconds) between kills for multi-kill streak", 0);
        _moduleServices.RegisterModuleConfig("Points", "RoundEndKillStreakReset", "Reset kill streak on round end", true);
        _moduleServices.RegisterModuleConfig("Points", "DoubleKill", "Points for double kill", 30);
        _moduleServices.RegisterModuleConfig("Points", "TripleKill", "Points for triple kill", 40);
        _moduleServices.RegisterModuleConfig("Points", "Domination", "Points for domination (4 kills)", 80);
        _moduleServices.RegisterModuleConfig("Points", "Rampage", "Points for rampage (5 kills)", 100);
        _moduleServices.RegisterModuleConfig("Points", "MegaKill", "Points for mega kill (6 kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "Ownage", "Points for ownage (7 kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "UltraKill", "Points for ultra kill (8 kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "KillingSpree", "Points for killing spree (9 kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "MonsterKill", "Points for monster kill (10 kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "Unstoppable", "Points for unstoppable (11 kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "GodLike", "Points for godlike (12+ kills)", 55);
        _moduleServices.RegisterModuleConfig("Points", "GrenadeKill", "Points for HE grenade kill", 25);
        _moduleServices.RegisterModuleConfig("Points", "InfernoKill", "Points for fire kill (Molotov/Incendiary)", 5);
        _moduleServices.RegisterModuleConfig("Points", "ImpactKill", "Points for impact damage kill (e.g. grenade impact)", 150);
        _moduleServices.RegisterModuleConfig("Points", "TaserKill", "Points for taser kill (Zeus)", 10);
        _moduleServices.RegisterModuleConfig("Points", "KnifeKill", "Points for knife kill", 50);
        _moduleServices.RegisterModuleConfig("Points", "PlaytimeInterval", "Interval for playtime bonus points (minutes), 0 to disable", 10);
        _moduleServices.RegisterModuleConfig("Points", "PlaytimePoints", "Points awarded per playtime interval", 10);

        // Get the config accessor
        _configAccessor = _moduleServices.GetModuleConfigAccessor();
    }
}