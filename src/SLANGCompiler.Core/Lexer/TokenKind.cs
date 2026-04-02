namespace SLANGCompiler.Lexer;

/// <summary>
/// SLANGのトークン種別
/// </summary>
public enum TokenKind
{
    // -- Special --
    EOF,
    Error,

    // -- Literals --
    IntegerLiteral,     // 123, $FF, 0xFF, 0FFh, 01b
    FloatLiteral,       // 1.5
    StringLiteral,      // "hello"
    CharLiteral,        // 'A'

    // -- Identifiers & Keywords --
    Identifier,         // foo, bar, 日本語識別子

    // Keywords - declarations
    Var,
    Byte,
    Word,
    Float,
    Array,
    Const,
    Machine,
    Asm,                // ASM (CONST ASM等で使用)

    // Keywords - control flow
    If,
    Then,
    Else,
    Elif,
    EndIf,
    While,
    Do,
    Wend,
    Repeat,
    Until,
    Case,
    Others,
    Of,
    Loop,
    For,
    To,
    DownTo,
    Next,
    Exit,
    Continue,
    Return,
    Goto,
    Begin,
    End,

    // Keywords - special
    Org,
    Work,
    Offset,
    Module,
    Print,
    Code,
    High,
    Low,
    Not,
    Cpl,
    Mod,                // MOD (算術剰余)

    // Keywords - bitwise (SLANG uses keywords, not symbols)
    And,                // AND
    Or,                 // OR
    Xor,                // XOR
    Ef,                 // EF (ELSE IF の略)

    // -- Operators --
    Plus,               // +
    Minus,              // -
    Star,               // *
    Slash,              // /
    Percent,            // %
    Ampersand,          // &
    Pipe,               // |  (reserved)
    Caret,              // ^  (reserved, part of identifier)
    Tilde,              // ~  (reserved)
    Exclamation,        // !

    Eq,                 // =
    EqEq,               // ==
    NotEq,              // != or <>
    Lt,                 // <
    Gt,                 // >
    Le,                 // <=
    Ge,                 // >=

    // Signed comparison/shift operators (.op.)
    SignedLt,           // .<.
    SignedGt,           // .>.
    SignedLe,           // .<=.
    SignedGe,           // .>=.
    SignedMul,          // .*.
    SignedDiv,          // ./.
    SignedMod,          // .MOD.
    SignedShl,          // .<<.
    SignedShr,          // .>>.

    Shl,                // <<
    Shr,                // >>
    LogAnd,             // &&
    LogOr,              // ||

    PlusPlus,           // ++
    MinusMinus,         // --
    PlusEq,             // +=
    MinusEq,            // -=
    StarEq,             // *=
    SlashEq,            // /=

    Question,           // ?

    // -- Delimiters --
    LParen,             // (
    RParen,             // )
    LBracket,           // [
    RBracket,           // ]
    LBrace,             // {
    RBrace,             // }
    LAngleBracket,      // ｢ (full-width)
    RAngleBracket,      // ｣ (full-width)

    Comma,              // ,
    Colon,              // :
    Semicolon,          // ;

    // -- Special (context-dependent) --
    ArrayBracketOpen,   // [ when preceded by identifier/]

    // -- String functions (PRINT format) --
    StringFunc,         // FORM$, DECI$, HEX2$, etc.

    // -- Preprocessor --
    PreprocInclude,     // #INCLUDE
    PreprocIf,          // #IF
    PreprocElse,        // #ELSE
    PreprocEnd,         // #END / #ENDIF
    PreprocAsm,         // #ASM ... #END (inline assembly block)

    // -- Inline assembly --
    Plain,              // raw assembly text from #ASM..#END
}
