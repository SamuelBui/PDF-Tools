namespace PdfTool.App.Models;

public class MergeValidationSummary
{
    public int TotalFiles { get; set; }
    public int ReadyFiles { get; set; }
    public int DuplicateFiles { get; set; }
    public int LockedFiles { get; set; }
    public int PasswordRequiredFiles { get; set; }
    public int InvalidPasswordFiles { get; set; }
    public int InvalidPdfFiles { get; set; }
    public bool HasBlockingIssues => DuplicateFiles > 0
                                     || LockedFiles > 0
                                     || PasswordRequiredFiles > 0
                                     || InvalidPasswordFiles > 0
                                     || InvalidPdfFiles > 0;
    public string SummaryText =>
        $"{ReadyFiles} ready, {DuplicateFiles} duplicates, {LockedFiles} locked, {PasswordRequiredFiles} need password, {InvalidPasswordFiles} wrong password, {InvalidPdfFiles} invalid.";
}
