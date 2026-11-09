using RaceDay.Mvc.Models;

namespace RaceDay.Mvc.Helpers;

public record ResultRow(ResultDto Result, int Position, int TotalInCategory);

public record CategoryResults(int CategoryId, string CategoryName, List<ResultRow> Rows);

public static class ResultsHelper
{
    /// <summary>
    /// Groups results by category and orders each group from the fastest to the slowest time.
    /// The finishing position saved by the organiser is used when there is one, otherwise the rank by finish time.
    /// </summary>
    public static List<CategoryResults> GroupByCategory(IEnumerable<ResultDto> results)
    {
        return results
            .GroupBy(r => new { r.CategoryId, r.CategoryName })
            .OrderBy(g => g.Key.CategoryName)
            .Select(g =>
            {
                var ordered = g.OrderBy(r => r.FinishTime ?? TimeSpan.MaxValue).ToList();
                var total = ordered.Count;
                var rows = ordered
                    .Select((r, i) => new ResultRow(r, r.FinishPosition ?? i + 1, total))
                    .ToList();
                return new CategoryResults(g.Key.CategoryId, g.Key.CategoryName, rows);
            })
            .ToList();
    }
}
