using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Options;

namespace HlaX64.Compiler.Verification;

/// <summary>
/// Static verification passes: definite assignment (HLAX0060), CFG (HLAX0061/0062),
/// register liveness across calls (HLAX0063).
/// </summary>
public sealed class VerificationAnalyzer
{
    private static readonly HashSet<string> CallerSaved = new(StringComparer.OrdinalIgnoreCase)
    {
        "rax", "rcx", "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11"
    };

    private static readonly HashSet<string> DestWriteMnemonics = new(StringComparer.OrdinalIgnoreCase)
    {
        "mov", "movsd", "movss", "movd", "movq", "add", "sub", "imul", "xor", "and", "or",
        "lea", "pop", "inc", "dec", "neg", "not", "shl", "shr", "sar"
    };

    private readonly CompilerWarnings _warnings;
    private readonly DiagnosticCollection _diagnostics = new();

    public VerificationAnalyzer(CompilerWarnings warnings)
    {
        _warnings = warnings;
    }

    public DiagnosticCollection Analyze(ProgramNode program)
    {
        _diagnostics.Clear();
        foreach (var stmt in program.Statements)
        {
            if (stmt is ProcedureNode proc)
                AnalyzeProcedure(proc);
        }

        return _diagnostics;
    }

    private void AnalyzeProcedure(ProcedureNode proc)
    {
        var locals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in proc.Variables)
        {
            if (v is VariableNode varNode)
                locals.Add(varNode.Name);
        }

        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in proc.Parameters)
            assigned.Add(p.Name);

        var liveRegs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AnalyzeStatements(proc.Body, locals, assigned, liveRegs, reachable: true, proc);

        if (_warnings.Unreachable && proc.ReturnsRegister != null)
            CheckReturnPath(proc);
    }

    private void CheckReturnPath(ProcedureNode proc)
    {
        var retReg = proc.ReturnsRegister!.ToLowerInvariant();
        if (!ProcedureWritesRegister(proc.Body, retReg))
        {
            var line = proc.Body.Count > 0 ? proc.Body[^1].Line : proc.Line;
            var column = proc.Body.Count > 0 ? proc.Body[^1].Column : proc.Column;
            _diagnostics.Warning("HLAX0062",
                $"Procedure '{proc.Name}' declares @returns(\"{proc.ReturnsRegister}\") but never assigns to '{proc.ReturnsRegister}'",
                line, column);
        }
    }

    private static bool ProcedureWritesRegister(List<AstNode> body, string reg)
    {
        foreach (var stmt in body)
        {
            if (StatementWritesRegister(stmt, reg))
                return true;
        }

        return false;
    }

    private static bool StatementWritesRegister(AstNode stmt, string reg)
    {
        switch (stmt)
        {
            case InstructionNode instr when DestWriteMnemonics.Contains(instr.Mnemonic):
                if (instr.Operands.Count >= 2 && OperandIsRegister(instr.Operands[1], reg))
                    return true;
                if (instr.Operands.Count == 1 && OperandIsRegister(instr.Operands[0], reg))
                    return true;
                return false;
            case IfNode ifNode:
                return ifNode.ThenBody.Any(s => StatementWritesRegister(s, reg))
                    || ifNode.ElseBody.Any(s => StatementWritesRegister(s, reg));
            case WhileNode whileNode:
                return whileNode.Body.Any(s => StatementWritesRegister(s, reg));
            case AssignExprNode assign when assign.Target is RegisterNode r:
                return r.Name.Equals(reg, StringComparison.OrdinalIgnoreCase);
            default:
                return false;
        }
    }

    private void AnalyzeStatements(
        List<AstNode> statements,
        HashSet<string> locals,
        HashSet<string> assigned,
        HashSet<string> liveRegs,
        bool reachable,
        ProcedureNode proc)
    {
        foreach (var stmt in statements)
        {
            if (!reachable && _warnings.Unreachable)
            {
                _diagnostics.Warning("HLAX0061",
                    "Unreachable code",
                    stmt.Line, stmt.Column);
            }

            reachable = AnalyzeStatement(stmt, locals, assigned, liveRegs, reachable, proc);
        }
    }

    private bool AnalyzeStatement(
        AstNode stmt,
        HashSet<string> locals,
        HashSet<string> assigned,
        HashSet<string> liveRegs,
        bool reachable,
        ProcedureNode proc)
    {
        if (!reachable)
        {
            // Still recurse into nested blocks to avoid missing nested unreachable.
            switch (stmt)
            {
                case IfNode ifNode:
                    AnalyzeStatements(ifNode.ThenBody, locals, new HashSet<string>(assigned, StringComparer.OrdinalIgnoreCase),
                        new HashSet<string>(liveRegs, StringComparer.OrdinalIgnoreCase), false, proc);
                    AnalyzeStatements(ifNode.ElseBody, locals, new HashSet<string>(assigned, StringComparer.OrdinalIgnoreCase),
                        new HashSet<string>(liveRegs, StringComparer.OrdinalIgnoreCase), false, proc);
                    return false;
                case WhileNode whileNode:
                    AnalyzeStatements(whileNode.Body, locals, new HashSet<string>(assigned, StringComparer.OrdinalIgnoreCase),
                        new HashSet<string>(liveRegs, StringComparer.OrdinalIgnoreCase), false, proc);
                    return false;
            }

            return false;
        }

        switch (stmt)
        {
            case InstructionNode instr:
                CheckInstructionReads(instr, locals, assigned);
                CheckInstructionRegLiveness(instr, liveRegs);
                ApplyInstructionWrites(instr, locals, assigned, liveRegs);
                return !IsUnconditionalTerminator(instr);

            case AssignExprNode assign:
                CheckAssignReads(assign, locals, assigned);
                ApplyAssignWrite(assign, locals, assigned);
                return true;

            case CallNode call:
                CheckCallReads(call, locals, assigned);
                WarnLiveRegsBeforeCall(call, liveRegs);
                ClearCallerSaved(liveRegs);
                return true;

            case IfNode ifNode:
            {
                var beforeAssigned = new HashSet<string>(assigned, StringComparer.OrdinalIgnoreCase);
                var beforeLive = new HashSet<string>(liveRegs, StringComparer.OrdinalIgnoreCase);

                var thenAssigned = new HashSet<string>(beforeAssigned, StringComparer.OrdinalIgnoreCase);
                var thenLive = new HashSet<string>(beforeLive, StringComparer.OrdinalIgnoreCase);
                AnalyzeStatements(ifNode.ThenBody, locals, thenAssigned, thenLive, true, proc);

                var elseAssigned = new HashSet<string>(beforeAssigned, StringComparer.OrdinalIgnoreCase);
                var elseLive = new HashSet<string>(beforeLive, StringComparer.OrdinalIgnoreCase);
                AnalyzeStatements(ifNode.ElseBody, locals, elseAssigned, elseLive, true, proc);

                assigned.Clear();
                foreach (var name in thenAssigned)
                {
                    if (elseAssigned.Contains(name))
                        assigned.Add(name);
                }

                liveRegs.Clear();
                foreach (var reg in beforeLive)
                {
                    if (thenLive.Contains(reg) && elseLive.Contains(reg))
                        liveRegs.Add(reg);
                }

                return true;
            }

            case WhileNode whileNode:
            {
                var loopEntryAssigned = new HashSet<string>(assigned, StringComparer.OrdinalIgnoreCase);
                var loopEntryLive = new HashSet<string>(liveRegs, StringComparer.OrdinalIgnoreCase);
                var bodyAssigned = new HashSet<string>(loopEntryAssigned, StringComparer.OrdinalIgnoreCase);
                var bodyLive = new HashSet<string>(loopEntryLive, StringComparer.OrdinalIgnoreCase);
                AnalyzeStatements(whileNode.Body, locals, bodyAssigned, bodyLive, true, proc);

                assigned.Clear();
                foreach (var name in loopEntryAssigned)
                {
                    if (bodyAssigned.Contains(name))
                        assigned.Add(name);
                }

                liveRegs.Clear();
                foreach (var reg in loopEntryLive)
                {
                    if (bodyLive.Contains(reg))
                        liveRegs.Add(reg);
                }

                return true;
            }

            default:
                return true;
        }
    }

    private void CheckInstructionReads(InstructionNode instr, HashSet<string> locals, HashSet<string> assigned)
    {
        if (!_warnings.DefiniteAssignment)
            return;

        if (instr.Operands.Count >= 2)
        {
            CheckOperandRead(instr.Operands[0], locals, assigned, instr);
            return;
        }

        if (instr.Operands.Count == 1 && !DestWriteMnemonics.Contains(instr.Mnemonic))
            CheckOperandRead(instr.Operands[0], locals, assigned, instr);
    }

    private void CheckAssignReads(AssignExprNode assign, HashSet<string> locals, HashSet<string> assigned)
    {
        if (!_warnings.DefiniteAssignment)
            return;

        CheckRuntimeExprRead(assign.Expression, locals, assigned);
    }

    private void CheckCallReads(CallNode call, HashSet<string> locals, HashSet<string> assigned)
    {
        if (!_warnings.DefiniteAssignment)
            return;

        foreach (var arg in call.Arguments)
            CheckOperandRead(arg, locals, assigned, call);
    }

    private void CheckRuntimeExprRead(AstNode node, HashSet<string> locals, HashSet<string> assigned)
    {
        switch (node)
        {
            case IdentifierNode ident:
                WarnIfUnassignedLocal(ident.Name, locals, assigned, ident.Line, ident.Column);
                break;
            case BinaryExprNode bin:
                CheckRuntimeExprRead(bin.Left, locals, assigned);
                CheckRuntimeExprRead(bin.Right, locals, assigned);
                break;
            case UnaryExprNode unary:
                CheckRuntimeExprRead(unary.Operand, locals, assigned);
                break;
        }
    }

    private void CheckOperandRead(AstNode node, HashSet<string> locals, HashSet<string> assigned, AstNode context)
    {
        switch (node)
        {
            case IdentifierNode ident:
                WarnIfUnassignedLocal(ident.Name, locals, assigned, ident.Line, ident.Column);
                break;
            case ArrayIndexNode arr:
                if (arr.Index is IdentifierNode idxIdent)
                    WarnIfUnassignedLocal(idxIdent.Name, locals, assigned, idxIdent.Line, idxIdent.Column);
                else
                    CheckOperandRead(arr.Index, locals, assigned, context);
                break;
            case MemoryRefNode:
            case AddressOfNode:
            case RegisterNode:
            case IntegerLiteralNode:
            case DotAccessNode:
            case AddressOfStringNode:
                break;
            default:
                CheckRuntimeExprRead(node, locals, assigned);
                break;
        }
    }

    private void WarnIfUnassignedLocal(string name, HashSet<string> locals, HashSet<string> assigned, int line, int column)
    {
        if (!locals.Contains(name) || assigned.Contains(name))
            return;

        _diagnostics.Warning("HLAX0060",
            $"Local variable '{name}' may be read before definite assignment",
            line, column);
    }

    private void ApplyInstructionWrites(InstructionNode instr, HashSet<string> locals, HashSet<string> assigned, HashSet<string> liveRegs)
    {
        var mnemonic = instr.Mnemonic.ToLowerInvariant();
        if (!DestWriteMnemonics.Contains(mnemonic))
            return;

        if (instr.Operands.Count >= 2)
        {
            MarkWrite(instr.Operands[1], locals, assigned, liveRegs);
            return;
        }

        if (instr.Operands.Count == 1)
            MarkWrite(instr.Operands[0], locals, assigned, liveRegs);
    }

    private void ApplyAssignWrite(AssignExprNode assign, HashSet<string> locals, HashSet<string> assigned)
    {
        if (assign.Target is IdentifierNode ident && locals.Contains(ident.Name))
            assigned.Add(ident.Name);
    }

    private static void MarkWrite(AstNode dest, HashSet<string> locals, HashSet<string> assigned, HashSet<string> liveRegs)
    {
        switch (dest)
        {
            case IdentifierNode ident when locals.Contains(ident.Name):
                assigned.Add(ident.Name);
                break;
            case ArrayIndexNode arr when locals.Contains(arr.ArrayName):
                assigned.Add(arr.ArrayName);
                break;
            case RegisterNode reg when CallerSaved.Contains(reg.Name):
                liveRegs.Add(reg.Name.ToLowerInvariant());
                break;
        }
    }

    private void CheckInstructionRegLiveness(InstructionNode instr, HashSet<string> liveRegs)
    {
        if (!_warnings.Liveness)
            return;

        // pop rcx etc. defines register before potential use in same instruction stream
        if (instr.Mnemonic.Equals("pop", StringComparison.OrdinalIgnoreCase) && instr.Operands.Count == 1
            && instr.Operands[0] is RegisterNode popReg && CallerSaved.Contains(popReg.Name))
        {
            liveRegs.Add(popReg.Name.ToLowerInvariant());
        }
    }

    private void WarnLiveRegsBeforeCall(CallNode call, HashSet<string> liveRegs)
    {
        if (!_warnings.Liveness)
            return;

        foreach (var reg in liveRegs.Where(CallerSaved.Contains).ToList())
        {
            _diagnostics.Warning("HLAX0063",
                $"Register '{reg}' may hold a live value across call to '{call.Name}' (caller-saved registers are clobbered)",
                call.Line, call.Column);
        }
    }

    private static void ClearCallerSaved(HashSet<string> liveRegs)
    {
        liveRegs.RemoveWhere(r => CallerSaved.Contains(r));
    }

    private static bool IsUnconditionalTerminator(InstructionNode instr)
    {
        var m = instr.Mnemonic.ToLowerInvariant();
        return m is "jmp" or "ret" or "hlt";
    }

    private static bool OperandIsRegister(AstNode node, string reg)
        => node is RegisterNode r && r.Name.Equals(reg, StringComparison.OrdinalIgnoreCase);
}
