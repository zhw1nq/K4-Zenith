
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;
using ZenithAPI;

namespace Zenith_Ranks;

public sealed partial class Plugin : BasePlugin
{
    public List<Rank> Ranks = [];

    public void Initialize_Ranks()
    {
        string ranksFilePath = Path.Join(ModuleDirectory, "ranks.jsonc");

        string defaultRanksContent = @"[
    {
        ""Name"": ""The VuAVu"",
        ""Image"": """",
        ""Point"": -999999999,
        ""ChatColor"": ""darkred"",
        ""HexColor"": ""#4B0000""
    },
    {
        ""Name"": ""The Mici"",
        ""Image"": """",
        ""Point"": -6000,
        ""ChatColor"": ""darkred"",
        ""HexColor"": ""#6B0000""
    },
    {
        ""Name"": ""The 36"",
        ""Image"": """",
        ""Point"": -3600,
        ""ChatColor"": ""red"",
        ""HexColor"": ""#8B0000""
    },
    {
        ""Name"": ""The 18"",
        ""Image"": """",
        ""Point"": -1800,
        ""ChatColor"": ""red"",
        ""HexColor"": ""#AA0000""
    },
    {
        ""Name"": ""The Ghost"",
        ""Image"": """",
        ""Point"": -1000,
        ""ChatColor"": ""lightred"",
        ""HexColor"": ""#CC0000""
    },
    {
        ""Name"": ""Silver I"",
        ""Image"": """",
        ""Point"": 0,
        ""ChatColor"": ""grey"",
        ""HexColor"": ""#C0C0C0""
    },
    {
        ""Name"": ""Silver II"",
        ""Image"": """",
        ""Point"": 500,
        ""ChatColor"": ""grey"",
        ""HexColor"": ""#C0C0C0""
    },
    {
        ""Name"": ""Silver III"",
        ""Image"": """",
        ""Point"": 1000,
        ""ChatColor"": ""grey"",
        ""HexColor"": ""#C0C0C0""
    },
    {
        ""Name"": ""Silver IV"",
        ""Image"": """",
        ""Point"": 1500,
        ""ChatColor"": ""grey"",
        ""HexColor"": ""#C0C0C0""
    },
    {
        ""Name"": ""Silver Elite"",
        ""Image"": """",
        ""Point"": 2000,
        ""ChatColor"": ""grey"",
        ""HexColor"": ""#C0C0C0""
    },
    {
        ""Name"": ""Silver Elite Master"",
        ""Image"": """",
        ""Point"": 2700,
        ""ChatColor"": ""grey"",
        ""HexColor"": ""#C0C0C0""
    },
    {
        ""Name"": ""Gold Nova I"",
        ""Image"": """",
        ""Point"": 3200,
        ""ChatColor"": ""gold"",
        ""HexColor"": ""#FFD700""
    },
    {
        ""Name"": ""Gold Nova II"",
        ""Image"": """",
        ""Point"": 4300,
        ""ChatColor"": ""gold"",
        ""HexColor"": ""#FFD700""
    },
    {
        ""Name"": ""Gold Nova III"",
        ""Image"": """",
        ""Point"": 5400,
        ""ChatColor"": ""gold"",
        ""HexColor"": ""#FFD700""
    },
    {
        ""Name"": ""Gold Nova Master"",
        ""Image"": """",
        ""Point"": 6400,
        ""ChatColor"": ""gold"",
        ""HexColor"": ""#FFD700""
    },
    {
        ""Name"": ""Master Guardian I"",
        ""Image"": """",
        ""Point"": 8000,
        ""ChatColor"": ""green"",
        ""HexColor"": ""#00FF00""
    },
    {
        ""Name"": ""Master Guardian II"",
        ""Image"": """",
        ""Point"": 9600,
        ""ChatColor"": ""green"",
        ""HexColor"": ""#00FF00""
    },
    {
        ""Name"": ""Master Guardian Elite"",
        ""Image"": """",
        ""Point"": 11800,
        ""ChatColor"": ""green"",
        ""HexColor"": ""#00FF00""
    },
    {
        ""Name"": ""Distinguished Master Guardian"",
        ""Image"": """",
        ""Point"": 13900,
        ""ChatColor"": ""green"",
        ""HexColor"": ""#00FF00""
    },
    {
        ""Name"": ""Legendary Eagle"",
        ""Image"": """",
        ""Point"": 17100,
        ""ChatColor"": ""blue"",
        ""HexColor"": ""#0000FF""
    },
    {
        ""Name"": ""Legendary Eagle Master"",
        ""Image"": """",
        ""Point"": 20400,
        ""ChatColor"": ""blue"",
        ""HexColor"": ""#0000FF""
    },
    {
        ""Name"": ""Supreme Master First Class"",
        ""Image"": """",
        ""Point"": 24600,
        ""ChatColor"": ""purple"",
        ""HexColor"": ""#800080""
    },
    {
        ""Name"": ""Global Elite"",
        ""Image"": """",
        ""Point"": 30000,
        ""ChatColor"": ""lightred"",
        ""HexColor"": ""#FF4040""
    }
]";

        try
        {
            if (!File.Exists(ranksFilePath))
            {
                File.WriteAllText(ranksFilePath, defaultRanksContent);
                Logger.LogInformation("Default ranks file created.");
            }

            string fileContent = File.ReadAllText(ranksFilePath);

            if (string.IsNullOrWhiteSpace(fileContent))
            {
                ResetToDefaultRanksFile(ranksFilePath, defaultRanksContent);
                fileContent = File.ReadAllText(ranksFilePath);
            }

            string jsonContent = RemoveComments(fileContent);

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                ResetToDefaultRanksFile(ranksFilePath, defaultRanksContent);
                jsonContent = RemoveComments(File.ReadAllText(ranksFilePath));
            }

            Ranks = JsonConvert.DeserializeObject<List<Rank>>(jsonContent)!;
            if (Ranks == null || Ranks.Count == 0)
            {
                ResetToDefaultRanksFile(ranksFilePath, defaultRanksContent);
                Ranks = JsonConvert.DeserializeObject<List<Rank>>(RemoveComments(File.ReadAllText(ranksFilePath)))!;
            }

            for (int i = 0; i < Ranks.Count; i++)
            {
                Ranks[i].Id = i + 1;
            }

            foreach (Rank rank in Ranks)
            {
                rank.ChatColor = ChatColorUtility.ApplyPrefixColors(rank.ChatColor);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("An error occurred: " + ex.Message);
        }
    }

    private void ResetToDefaultRanksFile(string filePath, string defaultContent)
    {
        File.WriteAllText(filePath, defaultContent);
        Logger.LogWarning("Invalid content found. Default ranks file regenerated.");
    }

    private string RemoveComments(string content)
    {
        return Regex.Replace(content, @"/\*(.*?)\*/|//(.*)", string.Empty, RegexOptions.Multiline);
    }

    public class Rank
    {
        public int Id { get; set; }

        [JsonPropertyName("Name")]
        public required string Name { get; set; }

        [JsonPropertyName("Point")]
        public int Point { get; set; }

        [JsonPropertyName("ChatColor")]
        public string ChatColor { get; set; } = "default";

        [JsonPropertyName("HexColor")]
        public string HexColor { get; set; } = "#FFFFFF";

        [JsonPropertyName("Permissions")]
        public List<Permission>? Permissions { get; set; }
    }

    public class Permission
    {
        [JsonPropertyName("DisplayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("PermissionName")]
        public string PermissionName { get; set; } = "";
    }
}