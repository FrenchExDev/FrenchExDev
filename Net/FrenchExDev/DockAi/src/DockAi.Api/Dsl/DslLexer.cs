using System.Text;

namespace DockAi.Api.Dsl;

/// <summary>
/// Tokenizes DSL source text into a stream of <see cref="DslToken"/>.
/// </summary>
public sealed class DslLexer
{
    private readonly string _source;
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    public DslLexer(string source) => _source = source;

    public List<DslToken> Tokenize()
    {
        var tokens = new List<DslToken>();
        while (_pos < _source.Length)
        {
            SkipWhitespaceExceptNewline();
            if (_pos >= _source.Length) break;

            var ch = _source[_pos];

            if (ch == '\n')
            {
                tokens.Add(Emit(DslTokenType.Newline, "\n"));
                Advance();
                continue;
            }
            if (ch == '\r')
            {
                Advance();
                if (_pos < _source.Length && _source[_pos] == '\n') Advance();
                tokens.Add(new DslToken(DslTokenType.Newline, "\n", _line - 1, _col));
                continue;
            }
            if (ch == '#')
            {
                tokens.Add(ReadComment());
                continue;
            }
            if (ch == '{') { tokens.Add(Emit(DslTokenType.LeftBrace, "{")); Advance(); continue; }
            if (ch == '}') { tokens.Add(Emit(DslTokenType.RightBrace, "}")); Advance(); continue; }
            if (ch == '[') { tokens.Add(Emit(DslTokenType.LeftBracket, "[")); Advance(); continue; }
            if (ch == ':') { tokens.Add(Emit(DslTokenType.Colon, ":")); Advance(); continue; }
            if (ch == ',') { tokens.Add(Emit(DslTokenType.Comma, ",")); Advance(); continue; }
            if (ch == '"') { tokens.Add(ReadQuotedString()); continue; }

            // -[ or -> or ]->
            if (ch == '-' && Peek(1) == '[')
            {
                tokens.Add(Emit(DslTokenType.DashBracket, "-["));
                Advance(); Advance();
                continue;
            }
            if (ch == ']' && Peek(1) == '-' && Peek(2) == '>')
            {
                tokens.Add(Emit(DslTokenType.BracketDash, "]->"));
                Advance(); Advance(); Advance();
                continue;
            }
            if (ch == ']')
            {
                tokens.Add(Emit(DslTokenType.RightBracket, "]"));
                Advance();
                continue;
            }
            if (ch == '-' && Peek(1) == '>')
            {
                tokens.Add(Emit(DslTokenType.Arrow, "->"));
                Advance(); Advance();
                continue;
            }

            // @keyword
            if (ch == '@')
            {
                tokens.Add(ReadKeyword());
                continue;
            }

            // Number
            if (char.IsDigit(ch) || (ch == '-' && _pos + 1 < _source.Length && char.IsDigit(_source[_pos + 1])))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            // Identifier (word)
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                tokens.Add(ReadIdentifier());
                continue;
            }

            // Unknown char — skip
            Advance();
        }

        tokens.Add(new DslToken(DslTokenType.Eof, "", _line, _col));
        return tokens;
    }

    private DslToken ReadComment()
    {
        var start = (_line, _col);
        var sb = new StringBuilder();
        Advance(); // skip #
        while (_pos < _source.Length && _source[_pos] != '\n' && _source[_pos] != '\r')
        {
            sb.Append(_source[_pos]);
            Advance();
        }
        return new DslToken(DslTokenType.Comment, sb.ToString().Trim(), start.Item1, start.Item2);
    }

    private DslToken ReadQuotedString()
    {
        var start = (_line, _col);
        Advance(); // skip opening "
        var sb = new StringBuilder();
        while (_pos < _source.Length && _source[_pos] != '"')
        {
            if (_source[_pos] == '\\' && _pos + 1 < _source.Length)
            {
                Advance();
                sb.Append(_source[_pos]);
            }
            else
            {
                sb.Append(_source[_pos]);
            }
            Advance();
        }
        if (_pos < _source.Length) Advance(); // skip closing "
        return new DslToken(DslTokenType.QuotedString, sb.ToString(), start.Item1, start.Item2);
    }

    private DslToken ReadKeyword()
    {
        var start = (_line, _col);
        Advance(); // skip @
        var sb = new StringBuilder("@");
        while (_pos < _source.Length && (char.IsLetterOrDigit(_source[_pos]) || _source[_pos] == '_' || _source[_pos] == '.'))
        {
            sb.Append(_source[_pos]);
            Advance();
        }

        var kw = sb.ToString();
        var type = kw switch
        {
            "@type" => DslTokenType.KwType,
            "@entity" => DslTokenType.KwEntity,
            "@relation.type" => DslTokenType.KwRelationType,
            "@relation" => DslTokenType.KwRelation,
            "@taxonomy" => DslTokenType.KwTaxonomy,
            "@rule" => DslTokenType.KwRule,
            _ => DslTokenType.Identifier
        };

        return new DslToken(type, kw, start.Item1, start.Item2);
    }

    private DslToken ReadNumber()
    {
        var start = (_line, _col);
        var sb = new StringBuilder();
        if (_source[_pos] == '-') { sb.Append('-'); Advance(); }
        while (_pos < _source.Length && (char.IsDigit(_source[_pos]) || _source[_pos] == '.'))
        {
            sb.Append(_source[_pos]);
            Advance();
        }
        return new DslToken(DslTokenType.Number, sb.ToString(), start.Item1, start.Item2);
    }

    private DslToken ReadIdentifier()
    {
        var start = (_line, _col);
        var sb = new StringBuilder();
        while (_pos < _source.Length && (char.IsLetterOrDigit(_source[_pos]) || _source[_pos] == '_' || _source[_pos] == '.'))
        {
            sb.Append(_source[_pos]);
            Advance();
        }
        var val = sb.ToString();

        if (val is "true" or "false")
            return new DslToken(DslTokenType.Bool, val, start.Item1, start.Item2);

        return new DslToken(DslTokenType.Identifier, val, start.Item1, start.Item2);
    }

    private DslToken Emit(DslTokenType type, string value) => new(type, value, _line, _col);

    private char Peek(int offset)
    {
        var idx = _pos + offset;
        return idx < _source.Length ? _source[idx] : '\0';
    }

    private void Advance()
    {
        if (_pos < _source.Length)
        {
            if (_source[_pos] == '\n')
            {
                _line++;
                _col = 1;
            }
            else
            {
                _col++;
            }
            _pos++;
        }
    }

    private void SkipWhitespaceExceptNewline()
    {
        while (_pos < _source.Length && _source[_pos] is ' ' or '\t')
            Advance();
    }
}
