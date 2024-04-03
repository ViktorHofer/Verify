namespace VerifyTests;

public static class Scrubbers
{
    static (Dictionary<string, string> exact, Dictionary<string, string> replace) machineNameReplacements;
    static (Dictionary<string, string> exact, Dictionary<string, string> replace) userNameReplacements;

    static Scrubbers() =>
        ResetReplacements(Environment.MachineName, Environment.UserName);

    internal static void ResetReplacements(string machineName, string userName)
    {
        machineNameReplacements = CreateWrappedReplacements(machineName, "TheMachineName");
        userNameReplacements = CreateWrappedReplacements(userName, "TheUserName");
    }

    static char[] validWrappingChars =
    [
        ' ',
        '\t',
        '\n',
        '\r'
    ];

    static (Dictionary<string, string> exact, Dictionary<string, string> replace) CreateWrappedReplacements(string toReplace, string toReplaceWith)
    {
        var replace = new Dictionary<string, string>(validWrappingChars.Length * 2);
        foreach (var wrappingChar in validWrappingChars)
        {
            replace[wrappingChar + toReplace] = wrappingChar + toReplaceWith;
            replace[toReplace + wrappingChar] = toReplaceWith + wrappingChar;
        }

        var exact = new Dictionary<string, string>(2 + validWrappingChars.Length * validWrappingChars.Length)
        {
            {
                toReplace, toReplaceWith
            }
        };
        foreach (var beforeChar in validWrappingChars)
        foreach (var afterChar in validWrappingChars)
        {
            exact[beforeChar + toReplace + afterChar] = beforeChar + toReplaceWith + afterChar;
        }

        return (exact, replace);
    }

    public static void ScrubMachineName(StringBuilder builder) =>
        PerformReplacements(builder, machineNameReplacements);

    public static void ScrubUserName(StringBuilder builder) =>
        PerformReplacements(builder, userNameReplacements);

    static void PerformReplacements(StringBuilder builder, (Dictionary<string, string> exact, Dictionary<string, string> replace) replacements)
    {
        var value = builder.ToString();
        foreach (var exact in replacements.exact)
        {
            if (value == exact.Key)
            {
                builder.Clear();
                builder.Append(exact.Value);
                return;
            }
        }

        foreach (var replace in replacements.replace)
        {
            builder.ReplaceIfLonger(replace.Key, replace.Value);
        }
    }

    public static string ScrubStackTrace(string stackTrace, bool removeParams = false)
    {
        var builder = new StringBuilder();
        foreach (var line in stackTrace
                     .AsSpan()
                     .EnumerateLines())
        {
            ProcessLine(removeParams, line, builder);
        }

        builder.TrimEnd();
        return builder.ToString();
    }

    static void ProcessLine(bool removeParams, CharSpan span, StringBuilder builder)
    {
        if (IgnoreLine(span))
        {
            return;
        }

        Span<char> buffer = stackalloc char[span.Length];
        span.CopyTo(buffer);
        buffer = buffer.TrimStart();
        if (!buffer.StartsWith("at "))
        {
            builder.AppendLineN(buffer);
            return;
        }

        if (buffer.StartsWith("at InnerVerifier.Throws") ||
            buffer.StartsWith("at InnerVerifier.<Throws"))
        {
            return;
        }

        if (removeParams)
        {
            var indexOfLeft = buffer.IndexOf('(');
            if (indexOfLeft > -1)
            {
                var c = buffer[indexOfLeft + 1];
                if (c == ')')
                {
                    buffer = buffer[..(indexOfLeft + 2)];
                }
                else
                {
                    buffer = buffer[..(indexOfLeft + 1)] + "...)";
                }
            }
        }
        else
        {
            var indexOfRight = buffer.IndexOf(')');
            if (indexOfRight > -1)
            {
                buffer = buffer[..(indexOfRight + 1)];
            }
        }

        buffer.Replace('+', '.');
        buffer.Replace(" (", "(");
        builder.AppendLineN(buffer);
    }

    static bool IgnoreLine(CharSpan span) =>
        IsStateMachine(span) ||
        span.Contains("System.Runtime.CompilerServices.TaskAwaiter".AsSpan(), StringComparison.Ordinal) ||
        span.Contains("End of stack trace from previous location where exception was thrown".AsSpan(), StringComparison.Ordinal);

#if NET8_0_OR_GREATER

    static SearchValues<string> anglesLookup = SearchValues.Create(["<>"], StringComparison.Ordinal);
    static SearchValues<string> moveNextLookup = SearchValues.Create([".MoveNext()"], StringComparison.Ordinal);

    static bool IsStateMachine(CharSpan span) =>
        span.ContainsAny(anglesLookup) &&
        span.ContainsAny(moveNextLookup);

#else

    static bool IsStateMachine(CharSpan span) =>
        span.Contains("<>".AsSpan(), StringComparison.Ordinal) &&
        span.Contains(".MoveNext()".AsSpan(), StringComparison.Ordinal);

#endif
}