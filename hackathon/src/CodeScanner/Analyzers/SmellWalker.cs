using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeScanner;

internal sealed class SmellWalker : CSharpSyntaxWalker
{
    private sealed class FunctionContext
    {
        public string Name = "";
        public int StartLine;
        public int EndLine;
        public int Lines;
        public int ParamCount;
        public int Depth;
        public int MaxDepth;
        public int DeepestDepthLine;
    }

    public const int LongFunctionThreshold = 50;
    public const int DeepNestingThreshold = 4;
    public const int LongParamListThreshold = 5;

    private readonly string _filePath;
    private readonly List<SmellFinding> _findings;
    private readonly Stack<FunctionContext> _stack = new();

    public SmellWalker(string filePath, List<SmellFinding> findings)
    {
        _filePath = filePath;
        _findings = findings;
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        EnterFunction(node, node.Identifier.ValueText, node.ParameterList.Parameters.Count);
        base.VisitMethodDeclaration(node);
        ExitFunction();
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        EnterFunction(node, node.Identifier.ValueText, node.ParameterList.Parameters.Count);
        base.VisitConstructorDeclaration(node);
        ExitFunction();
    }

    public override void VisitDestructorDeclaration(DestructorDeclarationSyntax node)
    {
        EnterFunction(node, "~" + node.Identifier.ValueText, paramCount: 0);
        base.VisitDestructorDeclaration(node);
        ExitFunction();
    }

    public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node)
    {
        EnterFunction(node, "operator " + node.OperatorToken.ValueText, node.ParameterList.Parameters.Count);
        base.VisitOperatorDeclaration(node);
        ExitFunction();
    }

    public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
    {
        EnterFunction(node, "conversion " + node.Type.ToString(), node.ParameterList.Parameters.Count);
        base.VisitConversionOperatorDeclaration(node);
        ExitFunction();
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        if (_stack.Count > 0)
        {
            IncrementCurrentDepth(node);
        }

        EnterFunction(node, node.Identifier.ValueText, node.ParameterList.Parameters.Count);
        base.VisitLocalFunctionStatement(node);
        ExitFunction();

        if (_stack.Count > 0)
        {
            _stack.Peek().Depth--;
        }
    }

    public override void VisitBlock(BlockSyntax node)
    {
        if (_stack.Count > 0)
        {
            IncrementCurrentDepth(node);
            base.VisitBlock(node);
            _stack.Peek().Depth--;
        }
        else
        {
            base.VisitBlock(node);
        }
    }

    private void IncrementCurrentDepth(SyntaxNode node)
    {
        var ctx = _stack.Peek();
        ctx.Depth++;
        if (ctx.Depth > ctx.MaxDepth)
        {
            ctx.MaxDepth = ctx.Depth;
            ctx.DeepestDepthLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        }
    }

    private void EnterFunction(SyntaxNode node, string name, int paramCount)
    {
        var span = node.GetLocation().GetLineSpan();
        var startLine = span.StartLinePosition.Line + 1;
        var endLine = span.EndLinePosition.Line + 1;
        var lines = endLine - startLine + 1;

        var ctx = new FunctionContext
        {
            Name = name,
            StartLine = startLine,
            EndLine = endLine,
            Lines = lines,
            ParamCount = paramCount,
            Depth = 0,
            MaxDepth = 0,
            DeepestDepthLine = startLine,
        };
        _stack.Push(ctx);

        if (lines > LongFunctionThreshold)
        {
            _findings.Add(new SmellFinding(
                Type: "long_function",
                Severity: ClassifyLongFunction(lines),
                File: _filePath,
                Name: name,
                StartLine: startLine,
                EndLine: endLine,
                Value: lines,
                Threshold: LongFunctionThreshold,
                Message: $"Function '{name}' is {lines} lines (threshold: {LongFunctionThreshold})"));
        }

        if (paramCount > LongParamListThreshold)
        {
            _findings.Add(new SmellFinding(
                Type: "long_parameter_list",
                Severity: ClassifyLongParamList(paramCount),
                File: _filePath,
                Name: name,
                StartLine: startLine,
                EndLine: startLine,
                Value: paramCount,
                Threshold: LongParamListThreshold,
                Message: $"Function '{name}' has {paramCount} parameters (threshold: {LongParamListThreshold})"));
        }
    }

    private void ExitFunction()
    {
        var ctx = _stack.Pop();
        if (ctx.MaxDepth > DeepNestingThreshold)
        {
            _findings.Add(new SmellFinding(
                Type: "deep_nesting",
                Severity: ClassifyDeepNesting(ctx.MaxDepth),
                File: _filePath,
                Name: ctx.Name,
                StartLine: ctx.DeepestDepthLine,
                EndLine: ctx.DeepestDepthLine,
                Value: ctx.MaxDepth,
                Threshold: DeepNestingThreshold,
                Message: $"Block depth {ctx.MaxDepth} inside '{ctx.Name}' (threshold: {DeepNestingThreshold})"));
        }
    }

    private static string ClassifyLongFunction(int lines) =>
        lines >= 151 ? "high" : lines >= 76 ? "medium" : "low";

    private static string ClassifyDeepNesting(int depth) =>
        depth >= 9 ? "high" : depth >= 7 ? "medium" : "low";

    private static string ClassifyLongParamList(int count) =>
        count >= 11 ? "high" : count >= 8 ? "medium" : "low";
}
