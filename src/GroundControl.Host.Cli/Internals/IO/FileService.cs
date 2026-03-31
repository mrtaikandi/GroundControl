using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace GroundControl.Host.Cli.Internals.IO;

[SuppressMessage("Design", "CA1031:Do not catch general exception types")]
internal sealed class FileService : IFileService
{
    private readonly IShell _shell;

    public FileService(IShell shell)
    {
        _shell = shell;
    }

    public async Task CreateAsync(string content, string directory, string filename)
    {
        try
        {
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(Path.Combine(directory, filename), content);
        }
        catch (Exception ex)
        {
            _shell.DisplayError($"Unable to create file {filename}. {ex.Message}");
        }
    }

    public async Task CreateAsync(Stream stream, string directory, string filename)
    {
        try
        {
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var filePath = Path.Combine(directory, filename);
            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream);
        }
        catch (Exception ex)
        {
            _shell.DisplayError($"Unable to create file {filename}. {ex.Message}");
        }
    }

    public bool Delete(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (Exception ex)
        {
            _shell.DisplayError($"Unable to delete {path}. {ex.Message}");

            return false;
        }

        return true;
    }

    public async Task<string?> ReadAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _shell.DisplayError($"Unable to read file. Path does not exist {filePath}.");

            return null;
        }

        return await File.ReadAllTextAsync(filePath);
    }

    public void Update(string filePath, string content)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _shell.DisplayError($"Unable to update file. Path does not exist {filePath}.");

                return;
            }

            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _shell.DisplayError($"Unable to update file {filePath}. {ex.Message}");
        }
    }

    public async Task UpdateAsync(string filePath, string content)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _shell.DisplayError($"Unable to update file. Path does not exist {filePath}.");

                return;
            }

            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _shell.DisplayError($"Unable to update file {filePath}. {ex.Message}");
        }
    }

    public bool FilesExist(string directory, string? fileExtension = null)
    {
        if (!Directory.Exists(directory))
        {
            return false;
        }

        var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        var comparer = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return string.IsNullOrWhiteSpace(fileExtension)
            ? files.Length != 0
            : files.Any(x => x.EndsWith(fileExtension, comparer));
    }

    public List<string> GetFilenames(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory
            .GetFiles(directory)
            .Select(Path.GetFileName)
            .ToList()!;
    }
}