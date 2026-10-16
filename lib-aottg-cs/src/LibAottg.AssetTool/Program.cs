// SPDX-License-Identifier: MIT
using System.Globalization;
using LibAottg.AssetTool;

if (args.Length < 2 || args.Contains("--help"))
{
    Console.WriteLine("Usage: LibAottg.AssetTool <capture-directory> <package-directory> [--meters-per-unit <positive-number>] [--unlit]");
    return args.Contains("--help") ? 0 : 2;
}
try
{
    float metersPerUnit = 1;
    bool unlit = false;
    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--unlit") unlit = true;
        else if (args[i] == "--meters-per-unit" && ++i < args.Length)
            metersPerUnit = float.Parse(args[i], CultureInfo.InvariantCulture);
        else throw new ArgumentException("Unknown or incomplete option: " + args[i - (i == args.Length ? 1 : 0)]);
    }
    var result = CharacterConverter.Convert(args[0], args[1], metersPerUnit, unlit);
    Console.WriteLine($"Wrote {result.GlbPath}: {result.MeshCount} visible meshes, {result.ClipCount} clips, {result.ByteLength:N0} bytes.");
    Console.WriteLine($"Conversion notes: {result.WarningCount}. See manifest.json and bind-pose.json.");
    return 0;
}
catch (Exception error) when (error is IOException or ArgumentException or InvalidDataException or System.Text.Json.JsonException or OverflowException or KeyNotFoundException or InvalidOperationException or FormatException)
{
    Console.Error.WriteLine("Conversion failed: " + error.Message);
    return 1;
}
