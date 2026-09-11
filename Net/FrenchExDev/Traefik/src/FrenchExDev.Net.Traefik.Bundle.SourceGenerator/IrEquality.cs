using System.Collections.Generic;
using System.Linq;

namespace FrenchExDev.Net.Traefik.Bundle.SourceGenerator;

/// <summary>
/// Structural equality helpers for the IR types so that the incremental
/// source-generator pipeline can cache parsed schema models. Without this,
/// every keystroke that touches an AdditionalText would force the emit
/// stage to re-run even when the parsed shape is identical.
/// </summary>
internal static class IrEquality
{
    public static bool ListEqual<T>(List<T>? a, List<T>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        var cmp = EqualityComparer<T>.Default;
        for (var i = 0; i < a.Count; i++)
        {
            if (!cmp.Equals(a[i], b[i])) return false;
        }
        return true;
    }

    public static bool DictEqual<TKey, TValue>(
        Dictionary<TKey, TValue>? a,
        Dictionary<TKey, TValue>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        var cmp = EqualityComparer<TValue>.Default;
        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var other)) return false;
            if (!cmp.Equals(kvp.Value, other)) return false;
        }
        return true;
    }

    public static int ListHash<T>(List<T>? items)
    {
        if (items is null) return 0;
        var hash = 17;
        var cmp = EqualityComparer<T>.Default;
        foreach (var item in items)
        {
            hash = unchecked(hash * 31 + (item is null ? 0 : cmp.GetHashCode(item!)));
        }
        return hash;
    }

    public static int DictHash<TKey, TValue>(Dictionary<TKey, TValue>? dict)
    {
        if (dict is null) return 0;
        // Order-independent hash: sum of per-entry hashes.
        var hash = 0;
        var keyCmp = EqualityComparer<TKey>.Default;
        var valCmp = EqualityComparer<TValue>.Default;
        foreach (var kvp in dict)
        {
            var entryHash = 17;
            entryHash = unchecked(entryHash * 31 + (kvp.Key is null ? 0 : keyCmp.GetHashCode(kvp.Key!)));
            entryHash = unchecked(entryHash * 31 + (kvp.Value is null ? 0 : valCmp.GetHashCode(kvp.Value!)));
            hash = unchecked(hash + entryHash);
        }
        return hash;
    }

    public static int Combine(params int[] hashes)
    {
        var hash = 17;
        foreach (var h in hashes)
        {
            hash = unchecked(hash * 31 + h);
        }
        return hash;
    }

    public static int HashOf<T>(T? value) where T : class
        => value is null ? 0 : EqualityComparer<T>.Default.GetHashCode(value);

    public static int HashOfString(string? value)
        => value is null ? 0 : value.GetHashCode();
}
