using System.Collections.Generic;
using System.Linq;

namespace FrenchExDev.Net.GitLab.Ci.Yaml.SourceGenerator;

internal static class SchemaVersionMerger
{
    public static UnifiedSchema Merge(List<SchemaModel> schemas)
    {
        if (schemas.Count == 0)
            return new UnifiedSchema();

        // Sort by version
        schemas.Sort((a, b) => CompareVersions(a.Version, b.Version));

        var unified = new UnifiedSchema
        {
            Versions = schemas.Select(s => s.Version).ToList()
        };

        var firstVersion = schemas[0].Version;
        var lastVersion = schemas[schemas.Count - 1].Version;

        // Merge definitions
        var allDefNames = schemas.SelectMany(s => s.Definitions.Keys).Distinct().ToList();
        foreach (var defName in allDefNames)
        {
            var firstAppearance = schemas.FirstOrDefault(s => s.Definitions.ContainsKey(defName));
            var lastAppearance = schemas.LastOrDefault(s => s.Definitions.ContainsKey(defName));
            if (firstAppearance is null) continue;

            var latestDef = schemas.Last(s => s.Definitions.ContainsKey(defName)).Definitions[defName];

            var uniDef = new UnifiedDefinition
            {
                Name = defName,
                Description = latestDef.Description,
                SinceVersion = firstAppearance.Version == firstVersion ? null : firstAppearance.Version,
                UntilVersion = lastAppearance!.Version == lastVersion ? null : lastAppearance.Version
            };

            // Merge properties across versions
            var allPropNames = schemas
                .Where(s => s.Definitions.ContainsKey(defName))
                .SelectMany(s => s.Definitions[defName].Properties.Select(p => p.JsonName))
                .Distinct()
                .ToList();

            foreach (var propName in allPropNames)
            {
                var firstPropVersion = schemas
                    .FirstOrDefault(s => s.Definitions.ContainsKey(defName) &&
                        s.Definitions[defName].Properties.Any(p => p.JsonName == propName));
                var lastPropVersion = schemas
                    .LastOrDefault(s => s.Definitions.ContainsKey(defName) &&
                        s.Definitions[defName].Properties.Any(p => p.JsonName == propName));

                if (firstPropVersion is null) continue;

                // Use the latest definition of the property
                var latestProp = schemas
                    .Last(s => s.Definitions.ContainsKey(defName) &&
                        s.Definitions[defName].Properties.Any(p => p.JsonName == propName))
                    .Definitions[defName].Properties.First(p => p.JsonName == propName);

                uniDef.Properties.Add(new UnifiedProperty
                {
                    Property = latestProp,
                    SinceVersion = firstPropVersion.Version == firstVersion ? null : firstPropVersion.Version,
                    UntilVersion = lastPropVersion!.Version == lastVersion ? null : lastPropVersion.Version
                });
            }

            unified.Definitions[defName] = uniDef;
        }

        // Merge root properties
        var allRootProps = schemas.SelectMany(s => s.RootProperties.Select(p => p.JsonName)).Distinct().ToList();
        foreach (var propName in allRootProps)
        {
            var firstPropVersion = schemas.FirstOrDefault(s => s.RootProperties.Any(p => p.JsonName == propName));
            var lastPropVersion = schemas.LastOrDefault(s => s.RootProperties.Any(p => p.JsonName == propName));
            if (firstPropVersion is null) continue;

            var latestProp = schemas
                .Last(s => s.RootProperties.Any(p => p.JsonName == propName))
                .RootProperties.First(p => p.JsonName == propName);

            unified.RootProperties.Add(new UnifiedProperty
            {
                Property = latestProp,
                SinceVersion = firstPropVersion.Version == firstVersion ? null : firstPropVersion.Version,
                UntilVersion = lastPropVersion!.Version == lastVersion ? null : lastPropVersion.Version
            });
        }

        return unified;
    }

    internal static int CompareVersions(string a, string b)
    {
        var partsA = a.Split('.');
        var partsB = b.Split('.');
        for (var i = 0; i < System.Math.Max(partsA.Length, partsB.Length); i++)
        {
            var va = i < partsA.Length && int.TryParse(partsA[i], out var ia) ? ia : 0;
            var vb = i < partsB.Length && int.TryParse(partsB[i], out var ib) ? ib : 0;
            if (va != vb) return va.CompareTo(vb);
        }
        return 0;
    }
}
