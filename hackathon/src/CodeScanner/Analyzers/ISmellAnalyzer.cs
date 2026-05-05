namespace CodeScanner;

public interface ISmellAnalyzer
{
    IReadOnlyList<SmellFinding> Analyze(string filePath, string content);
}
