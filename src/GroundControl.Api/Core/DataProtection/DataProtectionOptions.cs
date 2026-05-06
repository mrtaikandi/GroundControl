using GroundControl.Api.Core.DataProtection.Certificate;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.DataProtection;

/// <summary>
/// Configuration options for Data Protection key ring management.
/// </summary>
internal sealed class DataProtectionOptions
{
    /// <summary>
    /// Gets or sets the key ring storage mode.
    /// </summary>
    public DataProtectionMode Mode { get; set; } = DataProtectionMode.FileSystem;

    /// <summary>
    /// Gets or sets the certificate provider mode.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="Mode"/> is <see cref="DataProtectionMode.Certificate"/>
    /// or <see cref="DataProtectionMode.Redis"/>.
    /// </remarks>
    public CertificateProviderMode? CertificateProvider { get; set; }

    /// <summary>
    /// Gets or sets the file system path for key storage.
    /// </summary>
    public string KeyStorePath { get; set; } = "./keys";

    /// <summary>
    /// Gets or sets a value indicating whether to protect keys with DPAPI on Windows.
    /// </summary>
    public bool UseDpapi { get; set; }

    /// <summary>
    /// Gets or sets options for the file system certificate provider.
    /// </summary>
    public FileSystemCertificateOptions FileSystemCertificate { get; set; } = new();

    /// <summary>
    /// Gets or sets options for the Azure Blob certificate provider.
    /// </summary>
    public AzureBlobCertificateOptions AzureBlobCertificate { get; set; } = new();

    /// <summary>
    /// Gets or sets the Redis-specific options.
    /// </summary>
    public RedisOptions Redis { get; set; } = new();

    /// <summary>
    /// Gets or sets the Azure-specific options.
    /// </summary>
    public AzureOptions Azure { get; set; } = new();

    /// <summary>
    /// Validates <see cref="DataProtectionOptions"/> by enforcing mode/provider consistency
    /// and dispatching to source-generated validators for the active sub-options.
    /// </summary>
    internal sealed class Validator : IValidateOptions<DataProtectionOptions>
    {
        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, DataProtectionOptions options)
        {
            var failures = new List<string>();

            if (string.IsNullOrWhiteSpace(options.KeyStorePath))
            {
                failures.Add($"{nameof(DataProtectionOptions)}:{nameof(KeyStorePath)} is required.");
            }

            if (options.Mode is DataProtectionMode.Certificate or DataProtectionMode.Redis && !options.CertificateProvider.HasValue)
            {
                failures.Add($"{nameof(DataProtectionOptions)}:{nameof(CertificateProvider)} must be configured when Mode is '{options.Mode}'.");
            }

            switch (options.CertificateProvider)
            {
                case CertificateProviderMode.FileSystem:
                    if (!FileSystemCertificateOptions.Validator.TryValidate(options.FileSystemCertificate, out var fsFailures, nameof(FileSystemCertificate)))
                    {
                        failures.AddRange(fsFailures);
                    }
                    break;

                case CertificateProviderMode.AzureBlob:
                    if (!AzureBlobCertificateOptions.Validator.TryValidate(options.AzureBlobCertificate, out var blobFailures, nameof(AzureBlobCertificate)))
                    {
                        failures.AddRange(blobFailures);
                    }
                    break;
            }

            switch (options.Mode)
            {
                case DataProtectionMode.Redis when !RedisOptions.Validator.TryValidate(options.Redis, out var redisFailures, nameof(Redis)):
                    failures.AddRange(redisFailures);
                    break;

                case DataProtectionMode.Azure when !AzureOptions.Validator.TryValidate(options.Azure, out var azureFailures, nameof(Azure)):
                    failures.AddRange(azureFailures);
                    break;
            }

            return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
        }
    }
}