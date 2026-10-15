using System.Security.Cryptography;

namespace DBTool.AutoCAD.Template;

public sealed class TemplateResourceLoader
{
    public string ResolveReadOnly(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Template resource was not found.", fullPath);
        return fullPath;
    }

    public string HashBytes(string path)
    {
        var fullPath = ResolveReadOnly(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
