namespace Yggdrasilnet.Server.Utils;

public static class ContentPaths {
    public static string Resolve(string relativePath) {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null) {
            var contentDir = Path.Combine(dir.FullName, "Content");
            if (Directory.Exists(contentDir)) {
                return Path.Combine(contentDir, relativePath);
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate a 'Content' folder above {AppContext.BaseDirectory}");
    }
}
