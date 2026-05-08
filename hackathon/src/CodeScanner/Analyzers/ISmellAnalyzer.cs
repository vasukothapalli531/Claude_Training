namespace CodeScanner;

public interface ISmellAnalyzer
{
    SmellAnalysisResult Analyze(string filePath, string content);
}
