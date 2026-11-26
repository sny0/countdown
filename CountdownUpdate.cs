using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Countdown
{
    internal sealed class CountdownUpdate
    {
        private const string InstallerName = "Countdown-Setup.msi";
        private const int MaxInstallerBytes = 32 * 1024 * 1024;
        private static readonly Uri LatestReleaseUri = new Uri("https://api.github.com/repos/sny0/countdown/releases/latest");
        private static readonly HttpClient Client = CreateClient();

        private readonly Uri installerUri;
        private readonly string sha256;
        private readonly long installerSize;

        internal Version ReleaseVersion { get; }

        private CountdownUpdate(Version version, string digest, long size)
        {
            ReleaseVersion = version;
            installerUri = new Uri($"https://github.com/sny0/countdown/releases/download/v{version}/{InstallerName}");
            sha256 = digest;
            installerSize = size;
        }

        internal static async Task<CountdownUpdate> CheckAsync(Version installedVersion, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri))
            {
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                using (var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    using (var content = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        return ReadRelease(content, installedVersion);
                    }
                }
            }
        }

        internal static CountdownUpdate ReadRelease(Stream json, Version installedVersion)
        {
            var serializer = new DataContractJsonSerializer(typeof(GitHubRelease));
            var release = (GitHubRelease)serializer.ReadObject(json);
            if (release?.Tag == null || !release.Tag.StartsWith("v", StringComparison.Ordinal)
                || !Version.TryParse(release.Tag.Substring(1), out Version version)
                || version.Build < 0 || version.Revision >= 0 || release.Tag != "v" + version.ToString(3)
                || release.Prerelease || release.Draft)
            {
                throw new InvalidDataException("The latest stable release does not have a supported version tag.");
            }

            if (version <= installedVersion)
            {
                return null;
            }

            var asset = release.Assets?.FirstOrDefault(item => item?.Name == InstallerName);
            if (asset == null || asset.Size <= 0 || asset.Size > MaxInstallerBytes || asset.Digest == null
                || !Regex.IsMatch(asset.Digest, @"\Asha256:[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant))
            {
                throw new InvalidDataException("The release does not provide a verifiable Windows installer.");
            }

            return new CountdownUpdate(version, asset.Digest.Substring("sha256:".Length), asset.Size);
        }

        internal async Task<string> DownloadInstallerAsync(CancellationToken cancellationToken)
        {
            var uri = installerUri;
            for (int request = 0; request < 4; request++)
            {
                using (var response = await Client.GetAsync(uri, cancellationToken).ConfigureAwait(false))
                {
                    int status = (int)response.StatusCode;
                    if (status == 301 || status == 302 || status == 303 || status == 307 || status == 308)
                    {
                        var location = response.Headers.Location;
                        if (location == null || !Uri.TryCreate(uri, location, out Uri next) || !IsReleaseAssetHost(next))
                        {
                            throw new InvalidDataException("The installer URL redirected outside GitHub's release storage.");
                        }

                        uri = next;
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    VerifyInstaller(bytes);
                    cancellationToken.ThrowIfCancellationRequested();

                    string path = Path.Combine(Path.GetTempPath(), $"Countdown-{ReleaseVersion}-{Guid.NewGuid():N}.msi");
                    using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                    {
                        destination.Write(bytes, 0, bytes.Length);
                    }

                    return path;
                }
            }

            throw new InvalidDataException("The installer URL redirected too many times.");
        }

        internal void VerifyInstaller(byte[] bytes)
        {
            using (var hasher = SHA256.Create())
            {
                string actual = BitConverter.ToString(hasher.ComputeHash(bytes)).Replace("-", "");
                if (bytes.LongLength != installerSize || !string.Equals(actual, sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The installer does not match the published SHA-256 digest.");
                }
            }
        }

        private static bool IsReleaseAssetHost(Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && uri.UserInfo.Length == 0
                && (uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com");
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            {
                Timeout = TimeSpan.FromSeconds(25),
                MaxResponseContentBufferSize = MaxInstallerBytes
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Countdown-Windows");
            return client;
        }

        [DataContract]
        private sealed class GitHubRelease
        {
            [DataMember(Name = "tag_name")] public string Tag { get; set; }
            [DataMember(Name = "draft")] public bool Draft { get; set; }
            [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
            [DataMember(Name = "assets")] public GitHubAsset[] Assets { get; set; }
        }

        [DataContract]
        private sealed class GitHubAsset
        {
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "digest")] public string Digest { get; set; }
            [DataMember(Name = "size")] public long Size { get; set; }
        }
    }
}
