namespace CodeScanner;

public interface ISecurityScanner
{
    IReadOnlyList<SecurityFinding> Scan(string filePath, string content);
}
