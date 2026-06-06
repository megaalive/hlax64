using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Types;

namespace HlaX64.Compiler.Semantic;

/// <summary>
/// Semantic analyzer for HLA-X64 programs.
/// Validates program structure, registers, instructions, types, and procedure declarations.
/// </summary>
public sealed class SemanticAnalyzer
{
    private readonly CompilerWarnings _warnings;
    private readonly DiagnosticCollection _diagnostics = new();
    private readonly Dictionary<string, IntegerTypeSymbol> _variableTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _arrayElementCounts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> KnownRegisters = new(StringComparer.OrdinalIgnoreCase)
    {
        // 64-bit
        "rax", "rbx", "rcx", "rdx",
        "rsi", "rdi", "rbp", "rsp",
        "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15",
        // 32-bit
        "eax", "ebx", "ecx", "edx",
        "esi", "edi", "ebp", "esp",
        "r8d", "r9d", "r10d", "r11d", "r12d", "r13d", "r14d", "r15d",
        // 16-bit
        "ax", "bx", "cx", "dx",
        // 8-bit
        "al", "bl", "cl", "dl",
    };

    private static readonly HashSet<string> KnownMnemonics = new(StringComparer.OrdinalIgnoreCase)
    {
        "mov", "add", "sub", "imul", "xor", "and", "or", "cmp",
        "lea", "push", "pop", "inc", "dec", "neg", "not",
        "shl", "shr", "sar", "rol", "ror",
        "jmp", "call", "ret", "syscall",
        "nop", "int3", "hlt",
    };

    private static readonly Dictionary<string, (int MinOps, int MaxOps)> InstructionOperandCounts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mov"] = (2, 2),
        ["add"] = (2, 2),
        ["sub"] = (2, 2),
        ["imul"] = (1, 3),
        ["xor"] = (2, 2),
        ["and"] = (2, 2),
        ["or"] = (2, 2),
        ["cmp"] = (2, 2),
        ["lea"] = (2, 2),
        ["push"] = (1, 1),
        ["pop"] = (1, 1),
        ["inc"] = (1, 1),
        ["dec"] = (1, 1),
        ["neg"] = (1, 1),
        ["not"] = (1, 1),
        ["shl"] = (2, 2),
        ["shr"] = (2, 2),
        ["sar"] = (2, 2),
        ["jmp"] = (1, 1),
        ["call"] = (1, 1),
        ["ret"] = (0, 0),
        ["syscall"] = (0, 0),
        ["nop"] = (0, 0),
        ["hlt"] = (0, 0),
    };

    public SemanticAnalyzer(CompilerWarnings? warnings = null)
    {
        _warnings = warnings ?? new CompilerWarnings();
    }

    public DiagnosticCollection Analyze(ProgramNode program)
    {
        _diagnostics.Clear();

        CheckProgramNameMatch(program);
        CheckDuplicateProcedures(program);

        foreach (var stmt in program.Statements)
        {
            AnalyzeStatement(stmt);
        }

        return _diagnostics;
    }

    private void CheckProgramNameMatch(ProgramNode program)
    {
        // TODO: The parser currently doesn't track begin/end names separately.
        // For now, we check that the program has a valid name.
        if (string.IsNullOrWhiteSpace(program.Name))
        {
            _diagnostics.Error("HLAX0001", "Program name is empty", 1, 1);
        }
    }

    private void CheckDuplicateProcedures(ProgramNode program)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stmt in program.Statements)
        {
            if (stmt is ProcedureNode proc)
            {
                if (!seen.Add(proc.Name))
                {
                    _diagnostics.Error(
                        "HLAX0007",
                        $"Duplicate procedure declaration '{proc.Name}'",
                        proc.Line, proc.Column);
                }
            }
        }
    }

    private void AnalyzeStatement(AstNode node)
    {
        switch (node)
        {
            case InstructionNode instr:
                AnalyzeInstruction(instr);
                break;
            case CallNode call:
                AnalyzeCall(call);
                break;
            case IfNode ifNode:
                AnalyzeIf(ifNode);
                break;
            case WhileNode whileNode:
                AnalyzeWhile(whileNode);
                break;
            case ProcedureNode proc:
                AnalyzeProcedure(proc);
                break;
        }
    }

    private void AnalyzeInstruction(InstructionNode instr)
    {
        var mnemonic = instr.Mnemonic.ToLowerInvariant();

        // Check if mnemonic is known
        if (!KnownMnemonics.Contains(mnemonic))
        {
            var suggestion = FindClosestRegisterOrInstruction(mnemonic);
            _diagnostics.Error(
                "HLAX0003",
                $"Unknown instruction '{instr.Mnemonic}'",
                instr.Line, instr.Column,
                suggestion);
            return;
        }

        // Check operand count
        if (InstructionOperandCounts.TryGetValue(mnemonic, out var spec))
        {
            if (instr.Operands.Count < spec.MinOps || instr.Operands.Count > spec.MaxOps)
            {
                _diagnostics.Error(
                    "HLAX0004",
                    $"Instruction '{instr.Mnemonic}' expects {spec.MinOps}-{spec.MaxOps} operands, but got {instr.Operands.Count}",
                    instr.Line, instr.Column);
            }
        }

        // Check operands
        foreach (var op in instr.Operands)
        {
            AnalyzeOperand(op);
        }

        // Narrowing check for mov: storing into a narrower variable
        if (mnemonic == "mov" && instr.Operands.Count == 2)
        {
            var dest = instr.Operands[1];
            var src = instr.Operands[0];
            if (dest is IdentifierNode destIdent && _variableTypes.TryGetValue(destIdent.Name, out var destType))
            {
                // Check if source is a wider variable
                if (src is IdentifierNode srcIdent && _variableTypes.TryGetValue(srcIdent.Name, out var srcType))
                {
                    if (!TypeRegistry.CanImplicitlyConvert(srcType, destType))
                    {
                        _diagnostics.Error("HLAX0021",
                            $"Cannot implicitly store {srcType.Name} into {destType.Name}. " +
                            $"Use explicit narrowing if intended.",
                            instr.Line, instr.Column);
                    }
                }
            }
        }
    }

    private void AnalyzeCall(CallNode call)
    {
        // Check arguments
        foreach (var arg in call.Arguments)
        {
            AnalyzeOperand(arg);
        }
    }

    private void AnalyzeIf(IfNode ifNode)
    {
        AnalyzeCondition(ifNode.Condition);

        foreach (var stmt in ifNode.ThenBody)
            AnalyzeStatement(stmt);
        foreach (var stmt in ifNode.ElseBody)
            AnalyzeStatement(stmt);
    }

    private void AnalyzeWhile(WhileNode whileNode)
    {
        AnalyzeCondition(whileNode.Condition);

        foreach (var stmt in whileNode.Body)
            AnalyzeStatement(stmt);
    }

    private void AnalyzeCondition(AstNode condition)
    {
        if (condition is ComparisonNode comp)
        {
            AnalyzeOperand(comp.Left);
            AnalyzeOperand(comp.Right);
        }
    }

    private void AnalyzeProcedure(ProcedureNode proc)
    {
        _variableTypes.Clear();
        _arrayElementCounts.Clear();

        // Resolve variable and parameter types
        foreach (var variable in proc.Variables)
        {
            if (variable is VariableNode varNode)
            {
                if (varNode.ElementCount < 1)
                {
                    _diagnostics.Error("HLAX0025",
                        $"Array length must be at least 1, got {varNode.ElementCount} for '{varNode.Name}'",
                        varNode.Line, varNode.Column);
                }

                var type = TypeRegistry.Lookup(varNode.Type);
                if (type == null)
                {
                    _diagnostics.Error("HLAX0020",
                        $"Unknown type '{varNode.Type}' for variable '{varNode.Name}'",
                        varNode.Line, varNode.Column);
                }
                else
                {
                    if (varNode.ElementCount > 1 && !IsSupportedArrayElementType(type))
                    {
                        _diagnostics.Error("HLAX0024",
                            $"Array element type '{varNode.Type}' is not supported; use byte, word, dword, int64, uint64, qword, or ptr",
                            varNode.Line, varNode.Column);
                    }

                    _variableTypes[varNode.Name] = type;
                    if (varNode.ElementCount > 1)
                        _arrayElementCounts[varNode.Name] = varNode.ElementCount;
                }
            }
        }

        foreach (var param in proc.Parameters)
        {
            var type = TypeRegistry.Lookup(param.Type);
            if (type == null)
            {
                _diagnostics.Error("HLAX0020",
                    $"Unknown type '{param.Type}' for parameter '{param.Name}'",
                    param.Line, param.Column);
            }
            else
            {
                _variableTypes[param.Name] = type;
            }
        }

        foreach (var stmt in proc.Body)
        {
            AnalyzeStatement(stmt);
        }
    }

    private static readonly HashSet<string> KnownIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "nl", "true", "false", "null", "nil", "stdout", "stderr", "stdin",
    };

    private void AnalyzeOperand(AstNode node)
    {
        if (node is RegisterNode reg)
        {
            if (!KnownRegisters.Contains(reg.Name))
            {
                var suggestion = FindClosestRegisterOrInstruction(reg.Name);
                _diagnostics.Error(
                    "HLAX0012",
                    $"Unknown register '{reg.Name}'",
                    reg.Line, reg.Column,
                    suggestion);
            }
        }
        else if (node is AddressOfNode addr)
        {
            if (!_variableTypes.ContainsKey(addr.VariableName))
            {
                _diagnostics.Error("HLAX0023",
                    $"Address-of requires a local variable or parameter, not '{addr.VariableName}'",
                    addr.Line, addr.Column);
            }
        }
        else if (node is MemoryRefNode mem)
        {
            if (!KnownRegisters.Contains(mem.Register))
            {
                _diagnostics.Error("HLAX0012", $"Unknown register '{mem.Register}'", mem.Line, mem.Column);
            }
        }
        else if (node is AddressOfStringNode)
        {
            // rodata label — always valid
        }
        else if (node is ArrayIndexNode arr)
        {
            if (!_variableTypes.ContainsKey(arr.ArrayName))
            {
                _diagnostics.Error("HLAX0027",
                    $"Unknown array variable '{arr.ArrayName}'",
                    arr.Line, arr.Column);
            }
            else if (!_arrayElementCounts.ContainsKey(arr.ArrayName))
            {
                _diagnostics.Error("HLAX0026",
                    $"'{arr.ArrayName}' is not an array; use type[count] in the var block",
                    arr.Line, arr.Column);
            }
            else
            {
                CheckArrayIndexBounds(arr);
            }

            AnalyzeOperand(arr.Index);
        }
        else if (node is IdentifierNode ident)
        {
            // Skip known identifiers like "nl" and known variables
            if (KnownIdentifiers.Contains(ident.Name))
                return;
            if (_variableTypes.ContainsKey(ident.Name))
                return;

            // Check if this identifier looks like a misspelled register (e.g., "raxz")
            // Only flag if it closely matches a known register
            if (KnownRegisters.Any(r => LevenshteinDistance(ident.Name, r) <= 1))
            {
                var suggestion = FindClosestRegisterOrInstruction(ident.Name);
                _diagnostics.Error(
                    "HLAX0012",
                    $"Unknown register '{ident.Name}'",
                    ident.Line, ident.Column,
                    suggestion);
            }
        }
    }

    private void CheckArrayIndexBounds(ArrayIndexNode arr)
    {
        if (!_warnings.Bounds)
            return;
        if (arr.Index is not IntegerLiteralNode lit)
            return;
        if (!_arrayElementCounts.TryGetValue(arr.ArrayName, out var length))
            return;

        if (lit.Value < 0 || lit.Value >= length)
        {
            _diagnostics.Warning("HLAX0030",
                $"Array index {lit.Value} may be out of bounds for '{arr.ArrayName}' (length {length})",
                arr.Line, arr.Column);
        }
    }

    private static bool IsSupportedArrayElementType(IntegerTypeSymbol type)
        => type.BitWidth is 8 or 16 or 32 or 64;

    private void AnalyzeMemoryRef(AstNode inner, int line, int column)
    {
        if (inner is RegisterNode reg)
        {
            if (!KnownRegisters.Contains(reg.Name))
                _diagnostics.Error("HLAX0012", $"Unknown register '{reg.Name}'", line, column);
            return;
        }

        _diagnostics.Error("HLAX0022",
            "Memory dereference [..] requires a register holding an address",
            line, column);
    }

    private static string? FindClosestRegisterOrInstruction(string input)
    {
        // Simple fuzzy matching using Levenshtein distance
        string? bestMatch = null;
        int bestDistance = int.MaxValue;

        foreach (var known in KnownRegisters)
        {
            int dist = LevenshteinDistance(input, known);
            if (dist < bestDistance && dist <= 3)
            {
                bestDistance = dist;
                bestMatch = known;
            }
        }

        if (bestMatch != null)
            return bestMatch;

        foreach (var known in KnownMnemonics)
        {
            int dist = LevenshteinDistance(input, known);
            if (dist < bestDistance && dist <= 3)
            {
                bestDistance = dist;
                bestMatch = known;
            }
        }

        return bestMatch;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}