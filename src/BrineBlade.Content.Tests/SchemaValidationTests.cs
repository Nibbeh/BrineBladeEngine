using Xunit;
using BrineBlade.Infrastructure.Content;

public class SchemaValidationTests
{
    [Fact]
    public void All_Content_Files_Pass_Schema_And_Lint()
    {
        // Discover content & schemas assuming repo layout
        var repoRoot = FindUp("Content");
        var contentRoot = System.IO.Path.Combine(repoRoot, "Content");
        var schemaRoot = System.IO.Path.Combine(repoRoot, "schemas");

        BrineBlade.Infrastructure.Content.ContentLinter.ValidateOrThrow(contentRoot, schemaRoot);
    }

    // Helper similar to ConsoleRunner.Program.FindUp()
    private static string FindUp(string leaf)
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var probe = System.IO.Path.Combine(dir, leaf);
            if (System.IO.Directory.Exists(probe) || System.IO.File.Exists(probe)) return System.IO.Path.GetFullPath(probe);
            dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, ".."));
        }
        return System.IO.Path.Combine(AppContext.BaseDirectory, leaf);
    }
}