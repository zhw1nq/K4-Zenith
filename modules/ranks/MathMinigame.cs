using System.Collections.Concurrent;
using System.Data;
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
    Spectator
}

public class MathMinigame
{
    private readonly Plugin _plugin;
    private readonly Random _random = new();

    private string? _currentExpression;
    private string? _currentAnswer;
    private bool _isActive;
    private DateTime _lastChallengeTime = DateTime.MinValue;

    // Timer reference for proper cancellation
    private CounterStrikeSharp.API.Modules.Timers.Timer? _expireTimer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _resultClearTimer;

    // Per-player tracking: wrong attempt count
    private readonly ConcurrentDictionary<ulong, int> _playerWrongAttempts = new();
    // Per-player tracking: cooldown until time
    private readonly ConcurrentDictionary<ulong, DateTime> _playerCooldowns = new();

    private static readonly int[] WrongPenalties = [50, 100, 150];
    private const int MaxWrongAttempts = 3;
    private const double CooldownSeconds = 2.0;

    private readonly string[] _operators = ["+", "-", "*"];

    // Local expression evaluator
    private static readonly DataTable _calculator = new();

    public MathMinigame(Plugin plugin)
    {
        _plugin = plugin;
    }

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

    private void StartChallenge()
    {
        try
        {
            string? expression = null;
            string? answer = null;

            // Retry up to 10 times to get a valid result
            for (int attempt = 0; attempt < 10; attempt++)
            {
                expression = GenerateExpression();
                answer = EvaluateLocal(expression);

                if (answer == null)
                    continue;

                // Validate: integer, within 5 digits (abs <= 99999)
                if (long.TryParse(answer, out long resultValue))
                {
                    if (Math.Abs(resultValue) <= 99999)
                        break;
                }

                // Result too large or invalid, retry
                answer = null;
            }

            if (expression == null || answer == null)
            {
                _plugin.Logger.LogWarning("[MathMinigame] Failed to generate valid expression after retries");
                return;
            }

            _currentExpression = expression;
            _currentAnswer = answer;
            _isActive = true;
            _lastChallengeTime = DateTime.Now;

            // Reset per-player tracking
            _playerWrongAttempts.Clear();
            _playerCooldowns.Clear();

            int displayDuration = _plugin._configAccessor.GetValue<int>("Minigame", "DisplayDuration");

            // Show center HTML to all players
            ShowChallengeHtml(displayDuration);

            // Chat announcement
            _plugin._moduleServices?.PrintForAll(
                _plugin.Localizer.ForPlayer(null, "k4.minigame.chat.announce", _currentExpression));

            // Cancel any previous expire timer
            _expireTimer?.Kill();

            // Auto-expire after displayDuration
            _expireTimer = _plugin.AddTimer((float)displayDuration, () =>
            {
                if (!_isActive)
                    return; // Already solved, skip

                EndChallenge();

                _plugin._moduleServices?.PrintForAll(
                    _plugin.Localizer.ForPlayer(null, "k4.minigame.expired"));
            });
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError("[MathMinigame] Error starting challenge: {Error}", ex.Message);
        }
    }

    public AnswerResult TryAnswer(CCSPlayerController player, string message)
    {
        if (!_isActive || _currentAnswer == null)
            return AnswerResult.NotActive;

        // Block spectators from answering
        if (player.Team <= CsTeam.Spectator)
        {
            _plugin._moduleServices?.PrintForPlayer(player,
                _plugin.Localizer.ForPlayer(player, "k4.minigame.spectator"));
            return AnswerResult.Spectator;
        }

        ulong steamId = player.SteamID;

        // Check if player exceeded max attempts
        int currentAttempts = _playerWrongAttempts.GetValueOrDefault(steamId, 0);
        if (currentAttempts >= MaxWrongAttempts)
        {
            _plugin._moduleServices?.PrintForPlayer(player,
                _plugin.Localizer.ForPlayer(player, "k4.minigame.maxattempts"));
            return AnswerResult.MaxAttempts;
        }

        // Check cooldown
        if (_playerCooldowns.TryGetValue(steamId, out DateTime cooldownUntil) && DateTime.Now < cooldownUntil)
        {
            double remaining = (cooldownUntil - DateTime.Now).TotalSeconds;
            _plugin._moduleServices?.PrintForPlayer(player,
                _plugin.Localizer.ForPlayer(player, "k4.minigame.cooldown", $"{remaining:F1}"));
            return AnswerResult.Cooldown;
        }

        string trimmedMessage = message.Trim();

        // Compare as integers (exact match)
        bool isCorrect = false;
        if (long.TryParse(_currentAnswer, out long expectedValue) &&
            long.TryParse(trimmedMessage, out long playerValue))
        {
            isCorrect = expectedValue == playerValue;
        }
        else
        {
            // Fallback to string comparison
            isCorrect = string.Equals(trimmedMessage, _currentAnswer, StringComparison.OrdinalIgnoreCase);
        }

        if (!isCorrect)
        {
            // Increment wrong attempts
            int attemptIndex = currentAttempts; // 0-based index
            _playerWrongAttempts[steamId] = currentAttempts + 1;

            // Set cooldown
            _playerCooldowns[steamId] = DateTime.Now.AddSeconds(CooldownSeconds);

            // Get penalty based on attempt number (escalating)
            int penaltyPoints = WrongPenalties[Math.Min(attemptIndex, WrongPenalties.Length - 1)];
            int remainingAttempts = MaxWrongAttempts - (currentAttempts + 1);

            if (_plugin._playerCache.TryGetValue(player, out var penaltyPlayer))
            {
                _plugin.ModifyPlayerPoints(penaltyPlayer, -penaltyPoints, "k4.events.minigame.wrong");
            }

            // Notify the player
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

            return AnswerResult.Wrong;
        }

        // === Correct answer! ===
        string expression = _currentExpression ?? "";
        string answer = _currentAnswer;

        // 1. Stop the challenge immediately
        EndChallenge();

        int rewardPoints = _plugin._configAccessor.GetValue<int>("Minigame", "RewardPoints");
        int winnerDisplayDuration = _plugin._configAccessor.GetValue<int>("Minigame", "WinnerDisplayDuration");

        // 2. Give points to the winner
        if (_plugin._playerCache.TryGetValue(player, out var playerServices))
        {
            _plugin.ModifyPlayerPoints(playerServices, rewardPoints, "k4.events.minigame");
        }

        // 3. Show winner result HTML to all players
        foreach (var p in _plugin.GetValidPlayers())
        {
            try
            {
                string resultHtml = $@"
                <font color='#00FF00' class='fontSize-s'>✓ {player.PlayerName} (+{rewardPoints})</font><br>
                <font color='#AAAAAA' class='fontSize-s'>{expression} = {answer}</font>";

                p.PrintToCenter(resultHtml, winnerDisplayDuration, ActionPriority.High);
            }
            catch { }
        }

        // 4. Auto-clear winner HTML after duration
        _resultClearTimer?.Kill();
        _resultClearTimer = _plugin.AddTimer((float)winnerDisplayDuration, () =>
        {
            ClearAllCenterHtml();
        });

        // 5. Chat announcement
        _plugin._moduleServices?.PrintForAll(
            _plugin.Localizer.ForPlayer(null, "k4.minigame.winner.chat", player.PlayerName, rewardPoints, expression, answer));

        return AnswerResult.Correct;
    }

    /// <summary>
    /// Ends the current challenge: resets state and cancels expire timer.
    /// Must be called before showing result/expired messages.
    /// </summary>
    private void EndChallenge()
    {
        _isActive = false;
        _currentExpression = null;
        _currentAnswer = null;

        // Cancel expire timer so it doesn't fire after challenge is resolved
        _expireTimer?.Kill();
        _expireTimer = null;

        // Clear challenge HTML from all players immediately
        ClearAllCenterHtml();
    }

    /// <summary>
    /// Clears center HTML for all valid players.
    /// </summary>
    private void ClearAllCenterHtml()
    {
        foreach (var p in _plugin.GetValidPlayers())
        {
            try
            {
                p.PrintToCenter(" ", 1, ActionPriority.High);
            }
            catch { }
        }
    }

    /// <summary>
    /// Shows the challenge question HTML to all players.
    /// </summary>
    private void ShowChallengeHtml(int displayDuration)
    {
        foreach (var player in _plugin.GetValidPlayers())
        {
            try
            {
                string localizedHtml = $@"
                <font color='#FFD700' class='fontSize-s'>{_plugin.Localizer.ForPlayer(player.Controller, "k4.minigame.challenge.title")}</font><br>
                <font color='#FFFFFF' class='fontSize-m'>{_plugin.Localizer.ForPlayer(player.Controller, "k4.minigame.challenge.question", _currentExpression!)}</font><br>
                <font color='#00FF00' class='fontSize-s'>{_plugin.Localizer.ForPlayer(player.Controller, "k4.minigame.challenge.hint")}</font>";

                player.PrintToCenter(localizedHtml, displayDuration, ActionPriority.High);
            }
            catch { }
        }
    }

    private string GenerateExpression()
    {
        int minOperands = _plugin._configAccessor.GetValue<int>("Minigame", "MinOperands");
        int maxOperands = _plugin._configAccessor.GetValue<int>("Minigame", "MaxOperands");
        int maxDifficulty = _plugin._configAccessor.GetValue<int>("Minigame", "MaxDifficulty");

        minOperands = Math.Max(2, minOperands);
        maxOperands = Math.Max(minOperands, maxOperands);
        maxDifficulty = Math.Max(10, maxDifficulty);

        int operandCount = _random.Next(minOperands, maxOperands + 1);

        var parts = new List<string>();

        for (int i = 0; i < operandCount; i++)
        {
            if (i > 0)
            {
                parts.Add(_operators[_random.Next(_operators.Length)]);
            }

            string lastOp = parts.Count >= 2 ? parts[^1] : "";

            if (lastOp == "*")
            {
                // Multiplier should be small (max 3)
                parts.Add(_random.Next(2, 4).ToString());
            }
            else
            {
                // Use lighter numbers for + and - for easier mental math
                int numChoice = _random.Next(10);
                if (numChoice < 5) // 50% small numbers (1-20)
                {
                    parts.Add(_random.Next(1, 21).ToString());
                }
                else if (numChoice < 8) // 30% medium numbers (20-50)
                {
                    parts.Add(_random.Next(20, Math.Min(maxDifficulty, 50) + 1).ToString());
                }
                else // 20% larger numbers (50-100)
                {
                    parts.Add(_random.Next(50, Math.Min(maxDifficulty, 100) + 1).ToString());
                }
            }
        }

        // Occasionally wrap a sub-expression in parentheses for more complexity
        if (parts.Count >= 5 && _random.Next(3) == 0)
        {
            // Find a suitable position to add parens (around 3 elements: num op num)
            int parenStart = _random.Next(0, (parts.Count - 2) / 2) * 2;
            if (parenStart + 2 < parts.Count)
            {
                parts[parenStart] = "(" + parts[parenStart];
                parts[parenStart + 2] = parts[parenStart + 2] + ")";
            }
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Evaluates a math expression locally using DataTable.Compute.
    /// Returns the integer result as a string, or null if evaluation fails.
    /// </summary>
    private static string? EvaluateLocal(string expression)
    {
        try
        {
            // DataTable.Compute can handle +, -, *, /, parentheses
            object result = _calculator.Compute(expression, null);

            if (result == null || result == DBNull.Value)
                return null;

            double value = Convert.ToDouble(result);

            // Must be a finite integer
            if (double.IsInfinity(value) || double.IsNaN(value))
                return null;

            // Check if result is integer (no decimals)
            if (value != Math.Floor(value))
                return null;

            return ((long)value).ToString();
        }
        catch
        {
            return null;
        }
    }
}
