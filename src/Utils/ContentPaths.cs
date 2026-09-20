namespace Yggdrasilnet.Server.Utils;

public static class ContentPaths
{
    public static string Resolve(string relativePath)
    {
        var contentDir = Path.Combine(AppContext.BaseDirectory, "Content");

        if (!Directory.Exists(contentDir))
        {
            throw new DirectoryNotFoundException(
                $"Could not locate 'Content' folder at {contentDir}");
        }

        return Path.Combine(contentDir, relativePath);
    }
}