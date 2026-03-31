namespace GroundControl.Host.Cli.Internals.IO;

/// <summary>
/// Provides an abstraction for file operations, allowing for easier testing and separation of concerns in file handling logic.
/// </summary>
internal interface IFileService
{
    /// <summary>
    /// Creates a file with the provided text content in the specified directory.
    /// </summary>
    /// <param name="content">The text content to write.</param>
    /// <param name="directory">The target directory path.</param>
    /// <param name="filename">The name of the file to create.</param>
    /// <returns>A task that represents the asynchronous create operation.</returns>
    Task CreateAsync(string content, string directory, string filename);

    /// <summary>
    /// Creates a file with the provided stream content in the specified directory.
    /// </summary>
    /// <param name="stream">The content stream to write.</param>
    /// <param name="directory">The target directory path.</param>
    /// <param name="filename">The name of the file to create.</param>
    /// <returns>A task that represents the asynchronous create operation.</returns>
    Task CreateAsync(Stream stream, string directory, string filename);

    /// <summary>
    /// Deletes the file at the specified path.
    /// </summary>
    /// <param name="path">The full path of the file to delete.</param>
    /// <returns><see langword="true"/> if the file was deleted; otherwise, <see langword="false"/>.</returns>
    bool Delete(string path);

    /// <summary>
    /// Reads the contents of a file asynchronously.
    /// </summary>
    /// <param name="filePath">The full path of the file to read.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation and contains the file content,
    /// or <see langword="null"/> if the file cannot be read.
    /// </returns>
    Task<string?> ReadAsync(string filePath);

    /// <summary>
    /// Updates an existing file with the provided text content.
    /// </summary>
    /// <param name="filePath">The full path of the file to update.</param>
    /// <param name="content">The text content to write.</param>
    void Update(string filePath, string content);

    /// <summary>
    /// Updates an existing file with the provided text content asynchronously.
    /// </summary>
    /// <param name="filePath">The full path of the file to update.</param>
    /// <param name="content">The text content to write.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    Task UpdateAsync(string filePath, string content);

    /// <summary>
    /// Determines whether files exist in a directory, optionally filtered by extension.
    /// </summary>
    /// <param name="directory">The directory path to inspect.</param>
    /// <param name="fileExtension">An optional file extension filter.</param>
    /// <returns><see langword="true"/> if one or more matching files exist; otherwise, <see langword="false"/>.</returns>
    bool FilesExist(string directory, string? fileExtension = null);

    /// <summary>
    /// Gets file names from the specified directory.
    /// </summary>
    /// <param name="directory">The directory path to inspect.</param>
    /// <returns>A list of file names found in the directory.</returns>
    List<string> GetFilenames(string directory);
}