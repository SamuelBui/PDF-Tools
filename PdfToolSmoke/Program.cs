namespace PdfToolSmoke;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 3 && args[0].Equals("--compress-file", StringComparison.OrdinalIgnoreCase))
        {
            var singleFileHarness = new RegressionHarness();
            return singleFileHarness.CompressFile(args[1], args[2], args.Length >= 4 ? args[3] : "balanced");
        }

        if (args.Length >= 2 && args[0].Equals("--inspect-images", StringComparison.OrdinalIgnoreCase))
        {
            return PdfImageInspector.Inspect(args[1]);
        }

        var mode = args.Contains("--generate-only", StringComparer.OrdinalIgnoreCase)
            ? HarnessMode.GenerateOnly
            : HarnessMode.GenerateAndRun;

        var harness = new RegressionHarness();
        return harness.Execute(mode);
    }
}

internal enum HarnessMode
{
    GenerateOnly,
    GenerateAndRun
}
