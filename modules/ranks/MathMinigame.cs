using System.Net;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using Microsoft.Extensions.Logging;
using ZenithAPI;

namespace Zenith_Ranks;

public enum AnswerResult
{
    NotActive,
    Correct,
    Wrong
}

public class MathMinigame
{
    private const string MODULE_ID = "K4-Zenith-Ranks";
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly Plugin _plugin;
    private readonly Random _random = new();

    private string? _currentExpression;
    private string? _currentAnswer;
    private bool _isActive;
    private DateTime _lastChallengeTime = DateTime.MinValue;

    private readonly string[] _operators = ["+", "-", "*"];

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
        string expression = GenerateExpression();

        Task.Run(async () =>
        {
            try
            {
                string? answer = await EvaluateExpression(expression);
                if (answer == null)
                {
                    _plugin.Logger.LogWarning("[MathMinigame] Failed to evaluate expression: {Expression}", expression);
                    return;
                }

                Server.NextFrame(() =>
                {
                    _currentExpression = expression;
                    _currentAnswer = answer.Trim();
                    _isActive = true;
                    _lastChallengeTime = DateTime.Now;

                    int displayDuration = _plugin._configAccessor.GetValue<int>("Minigame", "DisplayDuration");

                    // Show center HTML to all players
                    string htmlMessage = $@"
                    <font color='#FFD700' class='fontSize-l'>{_plugin.Localizer.ForPlayer(null, "k4.minigame.challenge.title")}</font><br>
                    <font color='#FFFFFF' class='fontSize-m'>{_plugin.Localizer.ForPlayer(null, "k4.minigame.challenge.question", _currentExpression)}</font><br>
                    <font color='#00FF00' class='fontSize-s'>{_plugin.Localizer.ForPlayer(null, "k4.minigame.challenge.hint")}</font>";

                    foreach (var player in _plugin.GetValidPlayers())
                    {
                        try
                        {
                            string localizedHtml = $@"
                            <font color='#FFD700' class='fontSize-l'>{_plugin.Localizer.ForPlayer(player.Controller, "k4.minigame.challenge.title")}</font><br>
                            <font color='#FFFFFF' class='fontSize-m'>{_plugin.Localizer.ForPlayer(player.Controller, "k4.minigame.challenge.question", _currentExpression)}</font><br>
                            <font color='#00FF00' class='fontSize-s'>{_plugin.Localizer.ForPlayer(player.Controller, "k4.minigame.challenge.hint")}</font>";

                            player.PrintToCenter(localizedHtml, displayDuration, ActionPriority.High);
                        }
                        catch { }
                    }

                    // Chat announcement
                    _plugin._moduleServices?.PrintForAll(
                        _plugin.Localizer.ForPlayer(null, "k4.minigame.chat.announce", _currentExpression));

                    // Auto-expire after displayDuration
                    _plugin.AddTimer((float)displayDuration, () =>
                    {
                        if (_isActive)
                        {
                            _isActive = false;
                            _currentExpression = null;
                            _currentAnswer = null;

                            _plugin._moduleServices?.PrintForAll(
                                _plugin.Localizer.ForPlayer(null, "k4.minigame.expired"));
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogError("[MathMinigame] Error starting challenge: {Error}", ex.Message);
            }
        });
    }

    public AnswerResult TryAnswer(CCSPlayerController player, string message)
    {
        if (!_isActive || _currentAnswer == null)
            return AnswerResult.NotActive;

        string trimmedMessage = message.Trim();

        // Try to parse both answer and message as numbers for comparison
        bool isCorrect = false;
        if (double.TryParse(_currentAnswer, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double expectedValue) &&
            double.TryParse(trimmedMessage, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double playerValue))
        {
            // Compare with small tolerance for floating point
            isCorrect = Math.Abs(expectedValue - playerValue) <= 0.01;
        }
        else
        {
            // Fallback to string comparison
            isCorrect = string.Equals(trimmedMessage, _currentAnswer, StringComparison.OrdinalIgnoreCase);
        }

        if (!isCorrect)
        {
            // Wrong answer - deduct penalty points
            int penaltyPoints = _plugin._configAccessor.GetValue<int>("Minigame", "WrongAnswerPenalty");
            if (penaltyPoints > 0 && _plugin._playerCache.TryGetValue(player, out var penaltyPlayer))
            {
                _plugin.ModifyPlayerPoints(penaltyPlayer, -penaltyPoints, "k4.events.minigame.wrong");
            }

            // Notify the player
            _plugin._moduleServices?.PrintForPlayer(player,
                _plugin.Localizer.ForPlayer(player, "k4.minigame.wrong", penaltyPoints));

            return AnswerResult.Wrong;
        }

        // Correct answer!
        _isActive = false;
        string expression = _currentExpression ?? "";
        string answer = _currentAnswer;
        _currentExpression = null;
        _currentAnswer = null;

        int rewardPoints = _plugin._configAccessor.GetValue<int>("Minigame", "RewardPoints");
        int winnerDisplayDuration = _plugin._configAccessor.GetValue<int>("Minigame", "WinnerDisplayDuration");

        // Give points to the winner
        if (_plugin._playerCache.TryGetValue(player, out var playerServices))
        {
            _plugin.ModifyPlayerPoints(playerServices, rewardPoints, "k4.events.minigame");
        }

        // Show winner center HTML to all players
        foreach (var p in _plugin.GetValidPlayers())
        {
            try
            {
                string winnerHtml = $@"
                <font color='#00FF00' class='fontSize-l'>{_plugin.Localizer.ForPlayer(p.Controller, "k4.minigame.winner.title")}</font><br>
                <font color='#FFD700' class='fontSize-m'>{player.PlayerName}</font><br>
                <font color='#FFFFFF' class='fontSize-s'>{_plugin.Localizer.ForPlayer(p.Controller, "k4.minigame.reward", rewardPoints)}</font><br>
                <font color='#AAAAAA' class='fontSize-s'>{expression} = {answer}</font>";

                p.PrintToCenter(winnerHtml, winnerDisplayDuration, ActionPriority.High);
            }
            catch { }
        }

        // Chat announcement
        _plugin._moduleServices?.PrintForAll(
            _plugin.Localizer.ForPlayer(null, "k4.minigame.winner.chat", player.PlayerName, rewardPoints, expression, answer));

        return AnswerResult.Correct;
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
                // Pick operator - occasionally use power (^)
                int opChoice = _random.Next(10);
                if (opChoice < 2) // 20% chance for power
                {
                    parts.Add("^");
                }
                else
                {
                    parts.Add(_operators[_random.Next(_operators.Length)]);
                }
            }

            // Check if next operator is power - use smaller base
            string lastOp = parts.Count >= 2 ? parts[^1] : "";

            if (lastOp == "^")
            {
                // Exponent should be small (2-4) to avoid huge numbers
                parts.Add(_random.Next(2, 5).ToString());
            }
            else
            {
                // Check if next position might be a power base
                // Use varied number sizes for interesting expressions
                int numChoice = _random.Next(10);
                if (numChoice < 3) // 30% small numbers
                {
                    parts.Add(_random.Next(2, 30).ToString());
                }
                else if (numChoice < 7) // 40% medium numbers
                {
                    parts.Add(_random.Next(10, Math.Min(maxDifficulty, 500) + 1).ToString());
                }
                else // 30% large numbers
                {
                    parts.Add(_random.Next(100, maxDifficulty + 1).ToString());
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

    private static async Task<string?> EvaluateExpression(string expression)
    {
        try
        {
            string encoded = WebUtility.UrlEncode(expression);
            string url = $"http://api.mathjs.org/v4/?expr={encoded}";

            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
