namespace FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;

public static class NamingHelper
{
    public static string Pluralize(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        if (name.EndsWith("y", System.StringComparison.Ordinal) && name.Length > 1
            && !IsVowel(name[name.Length - 2]))
            return name.Substring(0, name.Length - 1) + "ies";

        if (name.EndsWith("s", System.StringComparison.Ordinal)
            || name.EndsWith("x", System.StringComparison.Ordinal)
            || name.EndsWith("z", System.StringComparison.Ordinal)
            || name.EndsWith("ch", System.StringComparison.Ordinal)
            || name.EndsWith("sh", System.StringComparison.Ordinal))
            return name + "es";

        return name + "s";
    }

    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && !char.IsUpper(name[i - 1]))
                    sb.Append('_');
                else if (i > 0 && i < name.Length - 1 && char.IsUpper(name[i - 1]) && !char.IsUpper(name[i + 1]))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static bool IsVowel(char c)
    {
        return "aeiouAEIOU".IndexOf(c) >= 0;
    }
}
