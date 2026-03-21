using System.Text.RegularExpressions;

namespace FrenchExDev.Net.Packer.Bundle.Design;

/// <summary>
/// Parses a Go <c>.hcl2spec.go</c> file's <c>HCL2Spec()</c> method body,
/// extracting field names, cty types, and required status via regex.
/// No Go compiler needed.
/// </summary>
public static class Hcl2SpecParser
{
    // Matches: "field_name": &hcldec.AttrSpec{Name: "field_name", Type: cty.String, Required: false},
    private static readonly Regex AttrSpecPattern = new(
        @"""(\w+)"":\s*&hcldec\.AttrSpec\{Name:\s*""[^""]+"",\s*Type:\s*([^,]+),\s*Required:\s*(true|false)\}",
        RegexOptions.Compiled);

    // Matches: "block_name": &hcldec.BlockListSpec{TypeName: "block_name",
    private static readonly Regex BlockListPattern = new(
        @"""(\w+)"":\s*&hcldec\.BlockListSpec\{TypeName:\s*""([^""]+)""",
        RegexOptions.Compiled);

    // Matches: "block_name": &hcldec.BlockSpec{TypeName: "block_name",
    private static readonly Regex BlockSpecPattern = new(
        @"""(\w+)"":\s*&hcldec\.BlockSpec\{TypeName:\s*""([^""]+)""",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses the content of a <c>.hcl2spec.go</c> file and extracts all fields and nested blocks.
    /// </summary>
    public static (List<ScrapedField> Fields, List<ScrapedNestedBlock> NestedBlocks) Parse(string goSource)
    {
        var fields = new List<ScrapedField>();
        var nestedBlocks = new List<ScrapedNestedBlock>();

        foreach (Match match in AttrSpecPattern.Matches(goSource))
        {
            var hclName = match.Groups[1].Value;
            var ctyType = match.Groups[2].Value.Trim();
            var isRequired = match.Groups[3].Value == "true";

            fields.Add(new ScrapedField(hclName, ctyType, isRequired));
        }

        foreach (Match match in BlockListPattern.Matches(goSource))
        {
            var hclName = match.Groups[1].Value;
            var nestedTypeName = match.Groups[2].Value;
            nestedBlocks.Add(new ScrapedNestedBlock(hclName, nestedTypeName));
        }

        foreach (Match match in BlockSpecPattern.Matches(goSource))
        {
            var hclName = match.Groups[1].Value;
            var nestedTypeName = match.Groups[2].Value;
            if (!nestedBlocks.Exists(b => b.HclName == hclName))
                nestedBlocks.Add(new ScrapedNestedBlock(hclName, nestedTypeName));
        }

        return (fields, nestedBlocks);
    }
}
