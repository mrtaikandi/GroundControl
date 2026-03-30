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
    /// Gets or sets the file system path to the X.509 certificate.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the certificate password.
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Gets or sets the Azure Blob URL for certificate download.
    /// </summary>
    public Uri? CertificateAzureBlobUrl { get; set; }

    /// <summary>
    /// Gets or sets the Redis-specific options.
    /// </summary>
    public RedisOptions Redis { get; set; } = new();

    /// <summary>
    /// Gets or sets the Azure-specific options.
    /// </summary>
    public AzureOptions Azure { get; set; } = new();

    /// <summary>
    /// Validates <see cref="DataProtectionOptions"/> including cross-property constraints.
    /// </summary>
    internal sealed class Validator : IValidateOptions<DataProtectionOptions>
    {
        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, DataProtectionOptions options)
        {
            var failures = new List<string>();

            if (string.IsNullOrEmpty(options.KeyStorePath))
            {
                failures.Add("DataProtection:KeyStorePath is required.");
            }

            if (options.Mode is DataProtectionMode.Certificate or DataProtectionMode.Redis)
            {
                if (!options.CertificateProvider.HasValue)
                {
                    failures.Add($"DataProtection:CertificateProvider must be configured when Mode is '{options.Mode}'.");
                }
            }

            if (options.CertificateProvider is CertificateProviderMode.FileSystem && string.IsNullOrWhiteSpace(options.CertificatePath))
            {
                failures.Add("DataProtection:CertificatePath is required when CertificateProvider is 'FileSystem'.");
            }

            if (options is { CertificateProvider: CertificateProviderMode.AzureBlob, CertificateAzureBlobUrl: null })
            {
                failures.Add("DataProtection:AzureBlobUrl is required when CertificateProvider is 'AzureBlob'.");
            }

            if (options.Mode is DataProtectionMode.Redis)
            {
                if (RedisOptions.Validator.TryValidate(options.Redis, out var redisFailures, nameof(options.Redis)))
                {
                    failures.AddRange(redisFailures);
                }
            }

            if (options.Mode is DataProtectionMode.Azure)
            {
                if (AzureOptions.Validator.TryValidate(options.Azure, out var azureFailures, nameof(options.Azure)))
                {
                    failures.AddRange(azureFailures);
                }
            }

            return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
        }
    }
}