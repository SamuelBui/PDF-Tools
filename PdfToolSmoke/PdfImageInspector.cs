using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace PdfToolSmoke;

internal static class PdfImageInspector
{
    public static int Inspect(string inputPath)
    {
        try
        {
            using var document = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            var visitedObjects = new HashSet<string>(StringComparer.Ordinal);
            var totalImages = 0;

            for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
            {
                var resources = document.Pages[pageIndex].Elements.GetDictionary("/Resources");
                InspectResources(resources, pageIndex + 1, visitedObjects, ref totalImages);
            }

            Console.WriteLine($"Total image XObjects: {totalImages}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 2;
        }
    }

    private static void InspectResources(
        PdfDictionary? resources,
        int pageNumber,
        HashSet<string> visitedObjects,
        ref int totalImages)
    {
        var xObjects = resources?.Elements.GetDictionary("/XObject");
        if (xObjects == null)
        {
            return;
        }

        foreach (var element in xObjects.Elements)
        {
            var dictionary = ResolveDictionary(element.Value, out var objectKey);
            if (dictionary == null || !visitedObjects.Add(objectKey))
            {
                continue;
            }

            var subtype = Describe(dictionary.Elements["/Subtype"]);
            if (subtype == "/Image")
            {
                totalImages++;
                PrintImage(pageNumber, element.Key, dictionary);
            }
            else if (subtype == "/Form")
            {
                InspectResources(dictionary.Elements.GetDictionary("/Resources"), pageNumber, visitedObjects, ref totalImages);
            }
        }
    }

    private static void PrintImage(int pageNumber, string name, PdfDictionary image)
    {
        var width = image.Elements.GetInteger("/Width");
        var height = image.Elements.GetInteger("/Height");
        var bpc = image.Elements.GetInteger("/BitsPerComponent");
        var bytes = image.Stream?.Value?.LongLength ?? 0;

        Console.WriteLine(
            $"Page {pageNumber} {name}: {width}x{height}, {FormatBytes(bytes)}, BPC={bpc}, " +
            $"Filter={Describe(image.Elements["/Filter"])}, ColorSpace={Describe(image.Elements["/ColorSpace"])}, " +
            $"Decode={Describe(image.Elements["/Decode"])}, DecodeParms={Describe(image.Elements["/DecodeParms"])}, " +
            $"SMask={image.Elements.ContainsKey("/SMask")}, Mask={image.Elements.ContainsKey("/Mask")}");
    }

    private static PdfDictionary? ResolveDictionary(PdfItem? item, out string objectKey)
    {
        if (item is PdfReference reference && reference.Value is PdfDictionary referencedDictionary)
        {
            objectKey = reference.ObjectID.ToString();
            return referencedDictionary;
        }

        if (item is PdfDictionary dictionary)
        {
            objectKey = $"direct:{dictionary.GetHashCode()}";
            return dictionary;
        }

        objectKey = string.Empty;
        return null;
    }

    private static string Describe(PdfItem? item)
    {
        item = ResolveReference(item);
        if (item == null)
        {
            return "-";
        }

        if (item is PdfName name)
        {
            return name.Value;
        }

        if (item is PdfInteger integer)
        {
            return integer.Value.ToString();
        }

        if (item is PdfReal real)
        {
            return real.Value.ToString("0.###");
        }

        if (item is PdfArray array)
        {
            var values = array.Elements.Select(Describe);
            return $"[{string.Join(" ", values)}]";
        }

        if (item is PdfDictionary dictionary)
        {
            if (dictionary.Elements.ContainsKey("/N"))
            {
                return $"<<N={Describe(dictionary.Elements["/N"])} Length={dictionary.Stream?.Value?.LongLength ?? 0}>>";
            }

            return "<<dictionary>>";
        }

        return item.ToString() ?? item.GetType().Name;
    }

    private static PdfItem? ResolveReference(PdfItem? item)
        => item is PdfReference reference ? reference.Value : item;

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB"];
        var unitIndex = 0;
        double value = bytes;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            unitIndex++;
            value /= 1024;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
