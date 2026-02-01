using System.Collections.Generic;
using System.Linq;

public static class GameResultData
{
    public struct PlayerResult
    {
        public string Name;
        public int FirstPlaceCount;
    }

    private static List<PlayerResult> lastResults = new();

    public static IReadOnlyList<PlayerResult> LastResults => lastResults;

    public static void SetResults(IEnumerable<PlayerResult> results)
    {
        lastResults = results?.ToList() ?? new List<PlayerResult>();
    }

    public static void Clear()
    {
        lastResults.Clear();
    }
}
