namespace PdfTool.App.Helpers;

public static class PageRangeParser
{
    public static List<List<int>> Parse(string input, int maxPage)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Page ranges cannot be empty.");
        }

        var result = new List<List<int>>();
        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (part.Contains('-'))
            {
                var bounds = part.Split('-', StringSplitOptions.TrimEntries);
                if (bounds.Length != 2)
                {
                    throw new ArgumentException($"Invalid range: {part}");
                }

                if (!int.TryParse(bounds[0], out var start) || !int.TryParse(bounds[1], out var end))
                {
                    throw new ArgumentException($"Invalid range: {part}");
                }

                if (start < 1 || end > maxPage || start > end)
                {
                    throw new ArgumentException($"Range out of bounds: {part}");
                }

                result.Add(Enumerable.Range(start, end - start + 1).ToList());
            }
            else
            {
                if (!int.TryParse(part, out var page))
                {
                    throw new ArgumentException($"Invalid page number: {part}");
                }

                if (page < 1 || page > maxPage)
                {
                    throw new ArgumentException($"Page out of bounds: {part}");
                }

                result.Add(new List<int> { page });
            }
        }

        return result;
    }
}
