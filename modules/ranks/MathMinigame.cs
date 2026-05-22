using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using ZenithAPI;

namespace Zenith_Ranks;

public enum AnswerResult
{
    NotActive,
    Correct,
    Wrong,
    Cooldown,
    MaxAttempts,
    Spectator,
    AlreadyWon
}

public enum MinigameMode
{
    Math,
    Reaction,
    Unscramble
}

public class MathMinigame
{
    private readonly Plugin _plugin;
    private readonly Random _random = new();

    // --- Challenge state ---
    private MinigameMode _currentMode;
    private string? _currentQuestion;
    private string? _currentAnswer;
    private bool _isActive;
    private bool _isGracePeriod;
    private DateTime _lastChallengeTime = DateTime.MinValue;
    private DateTime _challengeStartTime;

    // --- Saved state for expired/result display ---
    private MinigameMode _lastMode;
    private string? _lastQuestion;
    private string? _lastAnswer;

    // --- Timers ---
    private CounterStrikeSharp.API.Modules.Timers.Timer? _displayTimer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _expireTimer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _graceTimer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _resultDisplayTimer;

    // --- Per-player tracking ---
    private readonly ConcurrentDictionary<ulong, int> _playerWrongAttempts = new();
    private readonly ConcurrentDictionary<ulong, DateTime> _playerCooldowns = new();

    // --- Winners tracking (Top 3) ---
    private readonly List<(CCSPlayerController Player, string Name, ulong SteamId)> _winners = [];

    // --- Unscramble word bank (CS2 maps & weapons) ---
    private static readonly string[] UnscrambleWords =
    [
        "Mirage", "Dust2", "Inferno", "Nuke", "Overpass", "Ancient", "Anubis", "Vertigo",
        "Train", "Cache", "Cobblestone", "Office", "Italy", "Assault",
        "AK47", "M4A4", "M4A1S", "AWP", "Deagle", "USP", "Glock",
        "Galil", "Famas", "Scout", "P90", "MP9", "MAC10", "UMP45",
        "Nova", "XM1014", "Negev", "Zeus", "Knife", "Molotov", "Smoke",
        "Flash", "Kevlar", "Helmet", "Defuser"
    ];

    public MathMinigame(Plugin plugin)
    {
        _plugin = plugin;
    }

    // ========================
    // PUBLIC API
    // ========================

    public void CheckAndStartChallenge()
    {
        if (!_plugin._configAccessor.GetValue<bool>("Minigame", "Enabled"))
            return;

        if (_isActive)
            return;

        int intervalMinutes = _plugin._configAccessor.GetValue<int>("Minigame", "IntervalMinutes");
        if (intervalMinutes <= 0)
            return;

        if ((DateTime.Now - _lastChallengeTime).TotalMinutes < intervalMinutes)
            return;

        StartChallenge();
    }

    /// <summary>
    /// Debug: Force start a challenge immediately, optionally with a specific mode.
    /// </summary>
    public void ForceStartChallenge(MinigameMode? forceMode = null)
    {
        if (_isActive)
            EndChallenge();

        if (forceMode.HasValue)
        {
            _currentMode = forceMode.Value;
            var (q, a) = GenerateChallenge(_currentMode);
            if (q == null || a == null) { _plugin.Logger.LogWarning("[Minigame] Debug: Failed to generate"); return; }
            _currentQuestion = q;
            _currentAnswer = a;
            _isActive = true;
            _isGracePeriod = false;
            _lastChallengeTime = DateTime.Now;
            _challengeStartTime = DateTime.Now;
            _playerWrongAttempts.Clear();
            _playerCooldowns.Clear();
            _winners.Clear();
            int displayDuration = _plugin._configAccessor.GetValue<int>("Minigame", "DisplayDuration");
            StartDisplayTimer();
            AnnounceChallengeInChat();
            KillTimer(ref _expireTimer);
            _expireTimer = _plugin.AddTimer((float)displayDuration, OnChallengeExpired);
            _plugin.Logger.LogInformation("[Minigame] Debug: Started {Mode} | Q={Question} | A={Answer}", _currentMode, _currentQuestion, _currentAnswer);
        }
        else
        {
            StartChallenge();
            if (_isActive)
                _plugin.Logger.LogInformation("[Minigame] Debug: Started {Mode} | Q={Question} | A={Answer}", _currentMode, _currentQuestion, _currentAnswer);
        }
    }

    public AnswerResult TryAnswer(CCSPlayerController player, string message)
    {
        if (!_isActive || _currentAnswer == null)
            return AnswerResult.NotActive;

        // Block spectators
        if (player.Team <= CsTeam.Spectator)
        {
            _plugin._moduleServices?.PrintForPlayer(player,
                _plugin.Localizer.ForPlayer(player, "k4.minigame.spectator"));
            return AnswerResult.Spectator;
        }

        ulong steamId = player.SteamID;

        // Already won?
        if (_winners.Any(w => w.SteamId == steamId))
            return AnswerResult.AlreadyWon;

        // Max attempts check
        int maxAttempts = _plugin._configAccessor.GetValue<int>("Minigame", "MaxWrongAttempts");
        int currentAttempts = _playerWrongAttempts.GetValueOrDefault(steamId, 0);
        if (currentAttempts >= maxAttempts)
        {
            _plugin._moduleServices?.PrintForPlayer(player,
                _plugin.Localizer.ForPlayer(player, "k4.minigame.maxattempts"));
            return AnswerResult.MaxAttempts;
        }

        // Cooldown check
        double cooldownSeconds = _plugin._configAccessor.GetValue<double>("Minigame", "CooldownSeconds");
        if (_playerCooldowns.TryGetValue(steamId, out DateTime cooldownUntil) && DateTime.Now < cooldownUntil)
        {
            double remaining = (cooldownUntil - DateTime.Now).TotalSeconds;
            _plugin._moduleServices?.PrintForPlayer(player,
                _plugin.Localizer.ForPlayer(player, "k4.minigame.cooldown", $"{remaining:F1}"));
            return AnswerResult.Cooldown;
        }

        string trimmedMessage = message.Trim();
        bool isCorrect = CheckAnswer(trimmedMessage);

        if (!isCorrect)
        {
            HandleWrongAnswer(player, steamId, currentAttempts, maxAttempts);
            return AnswerResult.Wrong;
        }

        // === Correct answer ===
        return HandleCorrectAnswer(player, steamId);
    }

    // ========================
    // CHALLENGE LIFECYCLE
    // ========================

    private void StartChallenge()
    {
        try
        {
            _currentMode = PickRandomMode();
            var (question, answer) = GenerateChallenge(_currentMode);

            if (question == null || answer == null)
            {
                _plugin.Logger.LogWarning("[MathMinigame] Failed to generate challenge");
                return;
            }

            _currentQuestion = question;
            _currentAnswer = answer;
            _isActive = true;
            _isGracePeriod = false;
            _lastChallengeTime = DateTime.Now;
            _challengeStartTime = DateTime.Now;

            // Reset tracking
            _playerWrongAttempts.Clear();
            _playerCooldowns.Clear();
            _winners.Clear();

            int displayDuration = _plugin._configAccessor.GetValue<int>("Minigame", "DisplayDuration");

            // Use repeating 1-second timer to show center HTML (fixes the overlap/stuck bug)
            StartDisplayTimer();

            // Chat announcement
            AnnounceChallengeInChat();

            // Auto-expire timer
            KillTimer(ref _expireTimer);
            _expireTimer = _plugin.AddTimer((float)displayDuration, OnChallengeExpired);
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError("[MathMinigame] Error starting challenge: {Error}", ex.Message);
        }
    }

    private void OnChallengeExpired()
    {
        if (!_isActive) return;

        // Save before clearing
        string question = _currentQuestion ?? "";
        string answer = _currentAnswer ?? "";
        MinigameMode mode = _currentMode;

        EndChallenge();

        // Show expired HTML with answer reveal
        int expiredDuration = _plugin._configAccessor.GetValue<int>("Minigame", "WinnerDisplayDuration");
        ShowExpiredHtml(mode, question, answer, expiredDuration);

        _plugin._moduleServices?.PrintForAll(
            _plugin.Localizer.ForPlayer(null, "k4.minigame.expired"));
    }

    private void EndChallenge()
    {
        // Save state for display after ending
        _lastMode = _currentMode;
        _lastQuestion = _currentQuestion;
        _lastAnswer = _currentAnswer;

        _isActive = false;
        _isGracePeriod = false;
        _currentQuestion = null;
        _currentAnswer = null;

        KillTimer(ref _displayTimer);
        KillTimer(ref _expireTimer);
        KillTimer(ref _graceTimer);
    }

    // ========================
    // ANSWER HANDLING
    // ========================

    private bool CheckAnswer(string playerAnswer)
    {
        if (_currentAnswer == null) return false;

        // For math mode, compare as integers
        if (_currentMode == MinigameMode.Math)
        {
            if (long.TryParse(_currentAnswer, out long expected) &&
                long.TryParse(playerAnswer, out long playerVal))
                return expected == playerVal;
        }

        // For reaction/unscramble, case-insensitive string match
        return string.Equals(playerAnswer, _currentAnswer, StringComparison.OrdinalIgnoreCase);
    }

    private void HandleWrongAnswer(CCSPlayerController player, ulong steamId, int currentAttempts, int maxAttempts)
    {
        _playerWrongAttempts[steamId] = currentAttempts + 1;

        double cooldownSeconds = _plugin._configAccessor.GetValue<double>("Minigame", "CooldownSeconds");
        _playerCooldowns[steamId] = DateTime.Now.AddSeconds(cooldownSeconds);

        int penaltyPoints = GetPenaltyForAttempt(currentAttempts);
        int remainingAttempts = maxAttempts - (currentAttempts + 1);

        if (_plugin._playerCache.TryGetValue(player, out var penaltyPlayer))
        {
            _plugin.ModifyPlayerPoints(penaltyPlayer, -penaltyPoints, "k4.events.minigame.wrong");
        }

        if (remainingAttempts > 0)
        {
            _plugin._moduleServices?.PrintForPlayer(player,
                _plugin.Localizer.ForPlayer(player, "k4.minigame.wrong", penaltyPoints, remainingAttempts));
        }
        else
        {
            _plugin._moduleServices?.PrintForPlayer(player,
                _plugin.Localizer.ForPlayer(player, "k4.minigame.wrong.final", penaltyPoints));
        }
    }

    private AnswerResult HandleCorrectAnswer(CCSPlayerController player, ulong steamId)
    {
        int placement = _winners.Count + 1;
        _winners.Add((player, player.PlayerName, steamId));

        string question = _currentQuestion ?? "";
        string answer = _currentAnswer ?? "";

        int rewardPoints = CalculateRewardForPlacement(placement);
        int winnerDisplayDuration = _plugin._configAccessor.GetValue<int>("Minigame", "WinnerDisplayDuration");

        // Give points
        if (_plugin._playerCache.TryGetValue(player, out var playerServices))
        {
            _plugin.ModifyPlayerPoints(playerServices, rewardPoints, "k4.events.minigame");
        }

        // Chat announcement
        _plugin._moduleServices?.PrintForAll(
            _plugin.Localizer.ForPlayer(null, "k4.minigame.winner.chat", player.PlayerName, rewardPoints, question, answer));

        if (placement == 1)
        {
            // First winner: start grace period
            int gracePeriod = _plugin._configAccessor.GetValue<int>("Minigame", "GracePeriodSeconds");

            if (gracePeriod > 0 && IsTop3Enabled())
            {
                _isGracePeriod = true;

                // Cancel the original expire timer
                KillTimer(ref _expireTimer);

                // Start grace period timer
                KillTimer(ref _graceTimer);
                _graceTimer = _plugin.AddTimer((float)gracePeriod, () =>
                {
                    if (!_isActive) return;
                    FinalizeChallenge(question, answer, winnerDisplayDuration);
                });

                // Announce grace period
                _plugin._moduleServices?.PrintForAll(
                    _plugin.Localizer.ForPlayer(null, "k4.minigame.grace", gracePeriod));
            }
            else
            {
                // No grace period, end immediately
                FinalizeChallenge(question, answer, winnerDisplayDuration);
            }
        }
        else
        {
            // 2nd/3rd winner during grace period - just notify
            _plugin._moduleServices?.PrintForPlayer(player,
                _plugin.Localizer.ForPlayer(player, "k4.minigame.placement", placement, rewardPoints));
        }

        return AnswerResult.Correct;
    }

    private void FinalizeChallenge(string question, string answer, int winnerDisplayDuration)
    {
        EndChallenge();

        // Show winner results on center HTML
        ShowWinnerResultHtml(question, answer, winnerDisplayDuration);

        // Auto-clear after duration
        KillTimer(ref _resultDisplayTimer);
        _resultDisplayTimer = _plugin.AddTimer((float)winnerDisplayDuration, () =>
        {
            KillTimer(ref _resultDisplayTimer);
        });
    }

    // ========================
    // DISPLAY (REPEATING TIMER FIX)
    // ========================

    /// <summary>
    /// Instead of sending a single center message with a long duration,
    /// use a repeating 1-second timer. When the timer stops, the message disappears cleanly.
    /// </summary>
    private void StartDisplayTimer()
    {
        KillTimer(ref _displayTimer);
        _displayTimer = _plugin.AddTimer(1.0f, () =>
        {
            if (!_isActive)
            {
                KillTimer(ref _displayTimer);
                return;
            }

            ShowChallengeHtml();
        }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

        // Also show immediately (don't wait 1 second)
        ShowChallengeHtml();
    }

    private void ShowChallengeHtml()
    {
        foreach (var player in _plugin.GetValidPlayers())
        {
            try
            {
                string html;

                if (_isGracePeriod)
                {
                    html = BuildGracePeriodHtml(player.Controller);
                }
                else
                {
                    html = _currentMode switch
                    {
                        MinigameMode.Math => BuildMathChallengeHtml(player.Controller),
                        MinigameMode.Reaction => BuildReactionChallengeHtml(player.Controller),
                        MinigameMode.Unscramble => BuildUnscrambleChallengeHtml(player.Controller),
                        _ => BuildMathChallengeHtml(player.Controller)
                    };
                }

                player.PrintToCenter(html, 2, ActionPriority.High);
            }
            catch { }
        }
    }

    // --- Mode-specific challenge HTML ---

    private (int Remaining, string TimeColor) GetTimeInfo()
    {
        int displayDuration = _plugin._configAccessor.GetValue<int>("Minigame", "DisplayDuration");
        int elapsed = (int)(DateTime.Now - _challengeStartTime).TotalSeconds;
        int remaining = Math.Max(0, displayDuration - elapsed);
        string timeColor = remaining <= 10 ? "#FF4444" : "#AAAAAA";
        return (remaining, timeColor);
    }

    private int GetScaledReward()
    {
        return CalculateRewardForPlacement(1);
    }

    private string BuildMathChallengeHtml(CCSPlayerController? controller)
    {
        var (remaining, timeColor) = GetTimeInfo();
        int reward = GetScaledReward();
        string modeLabel = _plugin.Localizer.ForPlayer(controller, "k4.minigame.mode.math");
        string question = _plugin.Localizer.ForPlayer(controller, "k4.minigame.challenge.question", _currentQuestion!);
        string hint = _plugin.Localizer.ForPlayer(controller, "k4.minigame.challenge.hint");

        return $@"
            <font color='#FFD700' class='fontSize-s'>━━ {modeLabel} ━━</font><br>
            <font color='#FFFFFF' class='fontSize-m'>🧮 {question}</font><br>
            <font color='#00FF00' class='fontSize-s'>💡 {hint}</font><br>
            <font color='{timeColor}' class='fontSize-s'>⏱ {remaining}s</font>  <font color='#00FFAA' class='fontSize-s'>🎁 +{reward}</font>";
    }

    private string BuildReactionChallengeHtml(CCSPlayerController? controller)
    {
        var (remaining, timeColor) = GetTimeInfo();
        int reward = GetScaledReward();
        string modeLabel = _plugin.Localizer.ForPlayer(controller, "k4.minigame.mode.reaction");

        // Space out each character for readability
        string spacedCode = string.Join("  ", _currentQuestion!.ToCharArray());

        return $@"
            <font color='#FFD700' class='fontSize-s'>━━ {modeLabel} ━━</font><br>
            <font color='#00FFFF' class='fontSize-l'>{spacedCode}</font><br>
            <font color='#00FF00' class='fontSize-s'>💡 !aw {_currentQuestion}</font><br>
            <font color='{timeColor}' class='fontSize-s'>⏱ {remaining}s</font>  <font color='#00FFAA' class='fontSize-s'>🎁 +{reward}</font>";
    }

    private string BuildUnscrambleChallengeHtml(CCSPlayerController? controller)
    {
        var (remaining, timeColor) = GetTimeInfo();
        int reward = GetScaledReward();
        string modeLabel = _plugin.Localizer.ForPlayer(controller, "k4.minigame.mode.unscramble");

        // Build hint: first letter + underscores
        string answer = _currentAnswer!;
        string firstLetter = answer[..1].ToUpper();
        string underscores = string.Join(" ", Enumerable.Range(0, answer.Length - 1).Select(_ => "_"));
        string hintDisplay = $"{firstLetter} {underscores}";
        string charCount = _plugin.Localizer.ForPlayer(controller, "k4.minigame.unscramble.hint", answer.Length);

        return $@"
            <font color='#FFD700' class='fontSize-s'>━━ {modeLabel} ━━</font><br>
            <font color='#FF66FF' class='fontSize-m'>🔀 {_currentQuestion}</font><br>
            <font color='#FFFFFF' class='fontSize-s'>💡 {hintDisplay}   {charCount}</font><br>
            <font color='#00FF00' class='fontSize-s'>📝 !aw &lt;answer&gt;</font><br>
            <font color='{timeColor}' class='fontSize-s'>⏱ {remaining}s</font>  <font color='#00FFAA' class='fontSize-s'>🎁 +{reward}</font>";
    }

    // --- Grace period HTML (answer hidden to prevent copying) ---

    private string BuildGracePeriodHtml(CCSPlayerController? controller)
    {
        string title = _plugin.Localizer.ForPlayer(controller, "k4.minigame.grace.title");
        string html = $"<font color='#FFD700' class='fontSize-s'>━━ {title} ━━</font><br>";

        string[] placeColors = ["#00FF00", "#00CCFF", "#FF9900"];
        string[] placeEmoji = ["🥇", "🥈", "🥉"];

        for (int i = 0; i < _winners.Count && i < 3; i++)
        {
            int reward = CalculateRewardForPlacement(i + 1);
            html += $"<font color='{placeColors[i]}' class='fontSize-s'>{placeEmoji[i]} {_winners[i].Name} (+{reward}) ✅</font><br>";
        }

        string graceHint = _plugin.Localizer.ForPlayer(controller, "k4.minigame.grace.hint");
        html += $"<font color='#AAAAAA' class='fontSize-s'>💡 {graceHint}</font>";
        return html;
    }

    // --- Winner result HTML (shows answer after challenge ends) ---

    private void ShowWinnerResultHtml(string question, string answer, int duration)
    {
        DateTime showUntil = DateTime.Now.AddSeconds(duration);
        MinigameMode mode = _lastMode;

        var timer = _plugin.AddTimer(1.0f, () =>
        {
            if (DateTime.Now >= showUntil)
            {
                KillTimer(ref _resultDisplayTimer);
                return;
            }

            foreach (var p in _plugin.GetValidPlayers())
            {
                try
                {
                    string html = BuildWinnerResultHtml(p.Controller, mode, question, answer);
                    p.PrintToCenter(html, 2, ActionPriority.High);
                }
                catch { }
            }
        }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

        _resultDisplayTimer = timer;

        // Show immediately
        foreach (var p in _plugin.GetValidPlayers())
        {
            try
            {
                string html = BuildWinnerResultHtml(p.Controller, mode, question, answer);
                p.PrintToCenter(html, 2, ActionPriority.High);
            }
            catch { }
        }
    }

    private string BuildWinnerResultHtml(CCSPlayerController? controller, MinigameMode mode, string question, string answer)
    {
        string title = _plugin.Localizer.ForPlayer(controller, "k4.minigame.result.title");
        string html = $"<font color='#FFD700' class='fontSize-s'>━━ {title} ━━</font><br>";

        string[] placeColors = ["#00FF00", "#00CCFF", "#FF9900"];
        string[] placeEmoji = ["🥇", "🥈", "🥉"];

        for (int i = 0; i < _winners.Count && i < 3; i++)
        {
            int reward = CalculateRewardForPlacement(i + 1);
            html += $"<font color='{placeColors[i]}' class='fontSize-s'>{placeEmoji[i]} {_winners[i].Name} (+{reward})</font><br>";
        }

        // Mode-specific answer display
        string answerLine = FormatAnswerForDisplay(mode, question, answer);
        html += $"<font color='#AAAAAA' class='fontSize-s'>📝 {answerLine}</font>";
        return html;
    }

    // --- Expired HTML (shows answer when no one answered) ---

    private void ShowExpiredHtml(MinigameMode mode, string question, string answer, int duration)
    {
        DateTime showUntil = DateTime.Now.AddSeconds(duration);

        KillTimer(ref _resultDisplayTimer);
        var timer = _plugin.AddTimer(1.0f, () =>
        {
            if (DateTime.Now >= showUntil)
            {
                KillTimer(ref _resultDisplayTimer);
                return;
            }

            foreach (var p in _plugin.GetValidPlayers())
            {
                try
                {
                    string html = BuildExpiredHtml(p.Controller, mode, question, answer);
                    p.PrintToCenter(html, 2, ActionPriority.High);
                }
                catch { }
            }
        }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

        _resultDisplayTimer = timer;

        // Show immediately
        foreach (var p in _plugin.GetValidPlayers())
        {
            try
            {
                string html = BuildExpiredHtml(p.Controller, mode, question, answer);
                p.PrintToCenter(html, 2, ActionPriority.High);
            }
            catch { }
        }
    }

    private string BuildExpiredHtml(CCSPlayerController? controller, MinigameMode mode, string question, string answer)
    {
        string title = _plugin.Localizer.ForPlayer(controller, "k4.minigame.expired.title");
        string noAnswer = _plugin.Localizer.ForPlayer(controller, "k4.minigame.expired.noanswer");
        string answerLine = FormatAnswerForDisplay(mode, question, answer);

        return $@"
            <font color='#FF4444' class='fontSize-s'>━━ {title} ━━</font><br>
            <font color='#AAAAAA' class='fontSize-s'>😔 {noAnswer}</font><br>
            <font color='#FFFFFF' class='fontSize-s'>📝 {answerLine}</font>";
    }

    // --- Format answer string based on mode ---

    private static string FormatAnswerForDisplay(MinigameMode mode, string question, string answer)
    {
        return mode switch
        {
            MinigameMode.Math => $"{question} = {answer}",
            MinigameMode.Reaction => $"{answer} ✓",
            MinigameMode.Unscramble => $"{question} → {answer}",
            _ => $"{question} = {answer}"
        };
    }

    // ========================
    // CHALLENGE GENERATION
    // ========================

    private MinigameMode PickRandomMode()
    {
        var enabledModes = new List<MinigameMode>();

        if (_plugin._configAccessor.GetValue<bool>("Minigame", "EnableMathMode"))
            enabledModes.Add(MinigameMode.Math);
        if (_plugin._configAccessor.GetValue<bool>("Minigame", "EnableReactionMode"))
            enabledModes.Add(MinigameMode.Reaction);
        if (_plugin._configAccessor.GetValue<bool>("Minigame", "EnableUnscrambleMode"))
            enabledModes.Add(MinigameMode.Unscramble);

        if (enabledModes.Count == 0)
            return MinigameMode.Math;

        return enabledModes[_random.Next(enabledModes.Count)];
    }

    private (string? Question, string? Answer) GenerateChallenge(MinigameMode mode)
    {
        return mode switch
        {
            MinigameMode.Math => GenerateMathChallenge(),
            MinigameMode.Reaction => GenerateReactionChallenge(),
            MinigameMode.Unscramble => GenerateUnscrambleChallenge(),
            _ => GenerateMathChallenge()
        };
    }

    private (string? Question, string? Answer) GenerateMathChallenge()
    {
        int scaledOperands = GetScaledOperandCount();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            var (expression, result) = BuildMathExpression(scaledOperands);

            if (Math.Abs(result) <= 99999)
                return (expression, result.ToString());
        }

        return (null, null);
    }

    private (string? Question, string? Answer) GenerateReactionChallenge()
    {
        int length = _plugin._configAccessor.GetValue<int>("Minigame", "ReactionStringLength");
        length = Math.Clamp(length, 3, 10);

        // Scale length based on player count
        if (_plugin._configAccessor.GetValue<bool>("Minigame", "AutoScaleEnabled"))
        {
            double scale = GetAutoScaleMultiplier();
            length = Math.Clamp((int)(length * scale), 3, 12);
        }

        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[_random.Next(chars.Length)];

        string code = new(result);
        return (code, code);
    }

    private (string? Question, string? Answer) GenerateUnscrambleChallenge()
    {
        string word = UnscrambleWords[_random.Next(UnscrambleWords.Length)];
        string scrambled = ScrambleWord(word);

        // Make sure scrambled != original
        int retries = 0;
        while (scrambled.Equals(word, StringComparison.OrdinalIgnoreCase) && retries < 10)
        {
            scrambled = ScrambleWord(word);
            retries++;
        }

        return (scrambled, word);
    }

    private string ScrambleWord(string word)
    {
        var chars = word.ToCharArray();
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }

    // ========================
    // MATH EXPRESSION BUILDER (No DataTable.Compute)
    // ========================

    private (string Expression, long Result) BuildMathExpression(int operandCount)
    {
        int maxDifficulty = _plugin._configAccessor.GetValue<int>("Minigame", "MaxDifficulty");
        maxDifficulty = Math.Max(10, maxDifficulty);

        string[] operators = ["+", "-", "*"];

        // Generate operands and operators, compute result inline
        long result = GenerateOperand(maxDifficulty, "");
        string expression = result.ToString();

        for (int i = 1; i < operandCount; i++)
        {
            string op = operators[_random.Next(operators.Length)];
            long operand = op == "*" ? _random.Next(2, 4) : GenerateOperand(maxDifficulty, op);

            expression += $" {op} {operand}";

            result = op switch
            {
                "+" => result + operand,
                "-" => result - operand,
                "*" => result * operand,
                _ => result
            };
        }

        return (expression, result);
    }

    private long GenerateOperand(int maxDifficulty, string lastOp)
    {
        if (lastOp == "*")
            return _random.Next(2, 4);

        int numChoice = _random.Next(10);
        if (numChoice < 5) // 50% small
            return _random.Next(1, 21);
        if (numChoice < 8) // 30% medium
            return _random.Next(20, Math.Min(maxDifficulty, 50) + 1);
        // 20% larger
        return _random.Next(50, Math.Min(maxDifficulty, 100) + 1);
    }

    private int GetScaledOperandCount()
    {
        int minOperands = Math.Max(2, _plugin._configAccessor.GetValue<int>("Minigame", "MinOperands"));
        int maxOperands = Math.Max(minOperands, _plugin._configAccessor.GetValue<int>("Minigame", "MaxOperands"));

        int baseCount = _random.Next(minOperands, maxOperands + 1);

        if (_plugin._configAccessor.GetValue<bool>("Minigame", "AutoScaleEnabled"))
        {
            double scale = GetAutoScaleMultiplier();
            if (scale > 1.3)
                baseCount = Math.Min(baseCount + 1, maxOperands + 2);
        }

        return baseCount;
    }

    // ========================
    // AUTO-SCALING
    // ========================

    private double GetAutoScaleMultiplier()
    {
        int playerCount = _plugin._playerCache.Count;
        int threshold = _plugin._configAccessor.GetValue<int>("Minigame", "AutoScaleThreshold");
        double maxMult = _plugin._configAccessor.GetValue<double>("Minigame", "AutoScaleMaxMultiplier");

        if (playerCount <= threshold || threshold <= 0)
            return 1.0;

        // Linear scale from 1.0 to maxMult as players go from threshold to threshold*4
        double ratio = Math.Clamp((double)(playerCount - threshold) / (threshold * 3), 0.0, 1.0);
        return 1.0 + ratio * (maxMult - 1.0);
    }

    // ========================
    // REWARD / PENALTY CALCULATIONS
    // ========================

    private int CalculateRewardForPlacement(int placement)
    {
        int baseReward = _plugin._configAccessor.GetValue<int>("Minigame", "RewardPoints");

        // Apply auto-scaling
        if (_plugin._configAccessor.GetValue<bool>("Minigame", "AutoScaleEnabled"))
        {
            double scale = GetAutoScaleMultiplier();
            baseReward = (int)(baseReward * scale);
        }

        return placement switch
        {
            1 => baseReward,
            2 => (int)(baseReward * _plugin._configAccessor.GetValue<int>("Minigame", "Reward2ndPercent") / 100.0),
            3 => (int)(baseReward * _plugin._configAccessor.GetValue<int>("Minigame", "Reward3rdPercent") / 100.0),
            _ => 0
        };
    }

    private int GetPenaltyForAttempt(int attemptIndex)
    {
        return attemptIndex switch
        {
            0 => _plugin._configAccessor.GetValue<int>("Minigame", "WrongPenalty1"),
            1 => _plugin._configAccessor.GetValue<int>("Minigame", "WrongPenalty2"),
            _ => _plugin._configAccessor.GetValue<int>("Minigame", "WrongPenalty3"),
        };
    }

    private bool IsTop3Enabled()
    {
        return _plugin._configAccessor.GetValue<int>("Minigame", "Reward2ndPercent") > 0
            || _plugin._configAccessor.GetValue<int>("Minigame", "Reward3rdPercent") > 0;
    }

    // ========================
    // CHAT ANNOUNCEMENTS
    // ========================

    private void AnnounceChallengeInChat()
    {
        string modeKey = _currentMode switch
        {
            MinigameMode.Math => "k4.minigame.chat.announce.math",
            MinigameMode.Reaction => "k4.minigame.chat.announce.reaction",
            MinigameMode.Unscramble => "k4.minigame.chat.announce.unscramble",
            _ => "k4.minigame.chat.announce.math"
        };

        _plugin._moduleServices?.PrintForAll(
            _plugin.Localizer.ForPlayer(null, modeKey, _currentQuestion!));
    }

    private string GetModeTitle(CCSPlayerController? controller)
    {
        return _currentMode switch
        {
            MinigameMode.Math => _plugin.Localizer.ForPlayer(controller, "k4.minigame.mode.math"),
            MinigameMode.Reaction => _plugin.Localizer.ForPlayer(controller, "k4.minigame.mode.reaction"),
            MinigameMode.Unscramble => _plugin.Localizer.ForPlayer(controller, "k4.minigame.mode.unscramble"),
            _ => "CHALLENGE"
        };
    }

    // ========================
    // UTILITY
    // ========================

    private static void KillTimer(ref CounterStrikeSharp.API.Modules.Timers.Timer? timer)
    {
        timer?.Kill();
        timer = null;
    }
}
