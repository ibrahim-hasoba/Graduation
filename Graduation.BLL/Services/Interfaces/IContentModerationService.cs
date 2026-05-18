namespace Graduation.BLL.Services.Interfaces
{
    public interface IContentModerationService
    {
        ContentModerationResult Moderate(string text);
    }

    public class ContentModerationResult
    {
        public bool IsClean { get; set; } = true;
        public List<string> Flags { get; set; } = new();
    }
}
