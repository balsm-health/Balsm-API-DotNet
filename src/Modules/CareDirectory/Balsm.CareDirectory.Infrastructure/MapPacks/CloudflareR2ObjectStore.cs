using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Balsm.CareDirectory.Infrastructure.MapPacks;

/// <summary>
/// Talks to the same R2 bucket tools/map-packs/publish.py uploads basemaps
/// to. R2 is S3-compatible, so this is AWSSDK.S3 pointed at R2's endpoint
/// rather than a Cloudflare-specific client — the same choice publish.py's
/// own docstring makes with boto3.
/// </summary>
public sealed class CloudflareR2ObjectStore(IOptions<MapPackR2Options> options) : IMapPackObjectStore
{
    // Object names carry their version (basemap filename) or date (places
    // filename), so a given key's bytes never change once written. That
    // makes them safe to cache permanently — the same reasoning and the same
    // value as publish.py's CACHE_CONTROL.
    private const string CacheControl = "public, max-age=31536000, immutable";

    private readonly MapPackR2Options _options = options.Value;

    public async Task<IReadOnlyList<R2Object>> ListAsync(string prefix, CancellationToken ct)
    {
        using var client = CreateClient();
        var result = new List<R2Object>();
        string? continuationToken = null;

        do
        {
            var response = await client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _options.Bucket,
                Prefix = prefix,
                ContinuationToken = continuationToken,
            }, ct).ConfigureAwait(false);

            foreach (var entry in response.S3Objects)
            {
                // sha256 travels as object metadata (set at upload time) so a
                // download can be verified even by something that never saw
                // a manifest — HeadObject is the only way to read it back.
                var meta = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = _options.Bucket,
                    Key = entry.Key,
                }, ct).ConfigureAwait(false);

                var sha256 = meta.Metadata["sha256"];
                result.Add(new R2Object(entry.Key, entry.Size ?? 0, sha256));
            }

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (continuationToken is not null);

        return result;
    }

    public async Task PutAsync(string key, byte[] content, string contentType, string sha256, CancellationToken ct)
    {
        using var client = CreateClient();
        using var stream = new MemoryStream(content);

        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false,
            Headers = { CacheControl = CacheControl },
        };
        request.Metadata.Add("sha256", sha256);

        await client.PutObjectAsync(request, ct).ConfigureAwait(false);
    }

    private AmazonS3Client CreateClient() => new(
        new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey),
        new AmazonS3Config
        {
            ServiceURL = $"https://{_options.AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true,
            AuthenticationRegion = "auto",
        });
}
