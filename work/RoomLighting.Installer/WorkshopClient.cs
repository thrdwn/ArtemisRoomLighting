using System.Net.Http.Json;
using System.Text.Json;

namespace ArtemisRoomLighting.Installer;

internal static class WorkshopClient
{
    private const string Endpoint = "https://workshop.artemis-rgb.com/graphql";

    public static async Task<List<WorkshopEntry>> LoadPluginsAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            query Catalog {
              entries(includeDefaults: true, where: { entryType: { eq: PLUGIN } }) {
                id
                name
                summary
                author
                pluginInfo { pluginGuid }
                categories { name }
                latestRelease { version }
              }
            }
            """;

        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            Endpoint,
            new { query },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        JsonElement entries = document.RootElement.GetProperty("data").GetProperty("entries");
        List<WorkshopEntry> result = [];
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            WorkshopEntry item = new()
            {
                Id = entry.GetProperty("id").GetInt32(),
                Name = entry.GetProperty("name").GetString() ?? "",
                Summary = entry.TryGetProperty("summary", out JsonElement summary) ? summary.GetString() ?? "" : "",
                Author = entry.TryGetProperty("author", out JsonElement author) ? author.GetString() ?? "" : "",
                PluginGuid = entry.GetProperty("pluginInfo").GetProperty("pluginGuid").GetString() ?? "",
                Version = entry.TryGetProperty("latestRelease", out JsonElement release) &&
                          release.ValueKind != JsonValueKind.Null &&
                          release.TryGetProperty("version", out JsonElement version)
                    ? version.GetString() ?? ""
                    : ""
            };
            foreach (JsonElement category in entry.GetProperty("categories").EnumerateArray())
                item.Categories.Add(category.GetProperty("name").GetString() ?? "Plugin");
            result.Add(item);
        }

        return result.OrderBy(item => item.Type).ThenBy(item => item.Name).ToList();
    }
}
