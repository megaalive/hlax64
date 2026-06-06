namespace HlaX64.Compiler.Lexing;

public enum TokenType
{
    // Keywords
    Program,
    Begin,
    End,
    Include,
    
    // Procedure keywords
    Procedure,
    Var,
    Const,
    Endconst,
    Export,
    Returns,
    
    // Control flow
    If,
    Then,
    Else,
    Endif,
    While,
    Do,
    Endwhile,
    
    // Pragmas and directives
    Pragma,
    
    // Identifiers & literals
    Identifier,
    StringLiteral,
    IntegerLiteral,
    
    // Registers (64-bit)
    RAX, RBX, RCX, RDX,
    RSI, RDI, RBP, RSP,
    R8, R9, R10, R11, R12, R13, R14, R15,
    
    // Registers (32-bit)
    EAX, EBX, ECX, EDX,
    ESI, EDI, EBP, ESP,
    R8D, R9D, R10D, R11D, R12D, R13D, R14D, R15D,
    
    // Registers (16-bit)
    AX, BX, CX, DX,
    
    // Registers (8-bit)
    AL, BL, CL, DL,
    
    // Symbols
    LeftParen,
    RightParen,
    Semicolon,
    Comma,
    Dot,
    At,
    Hash,
    Colon,
    ColonAssign,
    
    // Operators
    Star,
    Slash,
    Percent,
    Pipe,
    Caret,
    Tilde,
    ShiftLeft,
    ShiftRight,
    Equals,
    LessThan,
    GreaterThan,
    LessThanUnsigned,
    GreaterThanUnsigned,
    Plus,
    Minus,
    Ampersand,
    LeftBracket,
    RightBracket,
    
    // Special
    NewLine,
    EndOfFile,
    Unknown
}