namespace ClaudeCommit.Services
{
    internal interface IPromptTemplateService
    {
        string GitTemplate  { get; set; }
        string TfvcTemplate { get; set; }
        string BuildPrompt(DiffResult diff);
    }
}
