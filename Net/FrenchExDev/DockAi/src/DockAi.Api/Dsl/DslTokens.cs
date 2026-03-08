namespace DockAi.Api.Dsl;

public enum DslTokenType
{
    // Structural
    LeftBrace,      // {
    RightBrace,     // }
    LeftBracket,    // [
    RightBracket,   // ]
    Colon,          // :
    Comma,          // ,
    Arrow,          // ->
    DashBracket,    // -[
    BracketDash,    // ]->

    // Keywords (schema)
    KwType,         // @type
    KwEntity,       // @entity
    KwRelationType, // @relation.type
    KwRelation,     // @relation
    KwTaxonomy,     // @taxonomy
    KwRule,         // @rule

    // Literals
    Identifier,     // unquoted word: person, erard, voie_a
    QuotedString,   // "Stephane Erard"
    Number,         // 25000000, 0.7
    Bool,           // true, false

    // Special
    Comment,        // # ...
    Newline,
    Eof
}

public readonly record struct DslToken(DslTokenType Type, string Value, int Line, int Column)
{
    public override string ToString() => $"[{Type} '{Value}' @{Line}:{Column}]";
}
