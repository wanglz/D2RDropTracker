using System.IO;

namespace D2RDropTracker.Data;

public static class AppDataPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        AppContext.BaseDirectory,
        "统计数据");

    public static string GetPath(params string[] parts)
    {
        Directory.CreateDirectory(DataDirectory);
        return parts.Length == 0
            ? DataDirectory
            : Path.Combine([DataDirectory, .. parts]);
    }
}
