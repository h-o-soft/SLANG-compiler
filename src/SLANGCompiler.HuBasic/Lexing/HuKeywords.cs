namespace SLANGCompiler.HuBasic.Lexing;

/// <summary>
/// Hu-BASIC の予約語表。MVP で対応しない語も含める (変数名の誤認を防ぐため。
/// 未対応の語は構文解析でエラーにする)。
/// </summary>
public static class HuKeywords
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "ABS", "AND", "ASC", "ATN", "ATTR$", "AUTO", "BASE", "BEEP", "BIN$", "BLOAD", "BSAVE",
        "CALL", "CDBL", "CFLASH", "CGEN", "CGPAT$", "CHAIN", "CHARACTER$", "CHR$", "CINT", "CIRCLE", "CLEAR",
        "CLICK", "CLOSE", "CLR", "CLS", "COLOR", "CONSOLE", "CONT", "COS", "CREV", "CSIZE", "CSNG",
        "CSRLIN", "CVD", "CVI", "CVS", "DATA", "DATE$", "DEF", "DEFCHR$", "DEFDBL", "DEFINT",
        "DEFSNG", "DEFSTR", "DELETE", "DEVF", "DIM", "DSKF", "EDIT", "ELSE", "END", "EOF", "EQV",
        "ERASE", "ERL", "ERR", "ERROR", "EXP", "FIELD", "FILES", "FIX", "FN", "FOR", "FPOS", "FRAC",
        "FRE", "GET", "GOSUB", "GOTO", "HEX$", "HEXCHR$", "IF", "IMP", "INIT", "INKEY$", "INP",
        "INPUT", "INPUT$", "INSTR", "INT", "KBUF", "KEY", "KILL", "LEFT$", "LEN", "LET", "LINE", "LIST",
        "LLIST", "LOAD", "LOC", "LOCATE", "LOF", "LOG", "LPOS", "LPRINT", "LSET", "MERGE", "MID$",
        "MKD$", "MKI$", "MKS$", "MOD", "MON", "MUSIC", "NAME", "NEW", "NEXT", "NOT", "OCT$", "OFF",
        "ON", "OPEN", "OPTION", "OR", "OUT", "PAINT", "PALET", "PAUSE", "PEEK", "PLAY", "POINT",
        "POKE", "POP", "POS", "PRESET", "PRINT", "PSET", "PUSH", "PUT", "RANDOMIZE", "READ", "REM",
        "RENUM", "REPEAT", "RESTORE", "RESUME", "RETURN", "RIGHT$", "RND", "RSET", "RUN", "SAVE",
        "SCREEN", "SET", "SGN", "SIN", "SOUND", "SPACE$", "SPC", "SQR", "STEP", "STICK", "STOP",
        "STR$", "STRIG", "STRING$", "SUB", "SWAP", "TAB", "TAN", "TEMPO", "THEN", "TIME", "TIME$",
        "TO", "TRACE", "UNTIL", "USING", "USR", "VAL", "VARPTR", "VDIM", "WAIT", "WEND", "WHILE",
        "WIDTH", "WINDOW", "XOR",
    };

    // 長い順 (最長一致のため)
    private static readonly string[] ByLength = All.OrderByDescending(k => k.Length).ThenBy(k => k).ToArray();

    /// <summary>text の pos から始まる最長の予約語を返す (大文字小文字は区別しない)。なければ null。</summary>
    public static string? MatchAt(string text, int pos)
    {
        foreach (var kw in ByLength)
        {
            if (pos + kw.Length > text.Length) continue;
            if (string.Compare(text, pos, kw, 0, kw.Length, StringComparison.OrdinalIgnoreCase) == 0)
                return kw;
        }
        return null;
    }
}
