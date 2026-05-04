using System;
using System.IO;
using System.Threading.Tasks;
using NuGet.Versioning;
using Nuke.Common.ChangeLog;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tools.GitHub;
using Nuke.Common.Tools.GitVersion;
using Octokit;
using Octokit.Internal;
using Serilog;

public class GitHubApi
{
	readonly IReleasesClient ReleaseApi;

	public GitHubApi(string token)
	{
		var client = new GitHubClient(
			new ProductHeaderValue(nameof(GitHubApi)),
			new InMemoryCredentialStore(new Credentials(token)));
		ReleaseApi = client.Repository.Release;
	}

	async Task UploadReleaseAssetToGithub(Release release, string asset)
	{
		await using var artifactStream = File.OpenRead(asset);
		var fileName = Path.GetFileName(asset);
		var assetUpload = new ReleaseAssetUpload {
			FileName = fileName,
			ContentType = "application/octet-stream",
			RawData = artifactStream,
		};
		Log.Information("Uploading {FileName}...", fileName);
		await ReleaseApi.UploadAsset(release, assetUpload);
	}

	async Task<bool> ReleaseExists(
		string repositoryOwner, string repositoryName, string releaseTag)
	{
		try
		{
			_ = await ReleaseApi.Get(repositoryOwner, repositoryName, releaseTag);
			return true;
		}
		catch (NotFoundException)
		{
			return false;
		}
	}

	public async Task<bool> Release(
		NuGetVersion packageVersion,
		GitRepository gitRepository,
		GitVersion gitVersion,
		ReleaseNotes releaseNotes,
		AbsolutePath[] artifacts)
	{
		var releaseTag = packageVersion.ToString();
		var repositoryOwner = gitRepository.GetGitHubOwner();
		var repositoryName = gitRepository.GetGitHubName();
		
		var releaseExists = await ReleaseExists(repositoryOwner, repositoryName, releaseTag);
		if (releaseExists)
		{
			Log.Warning("Release {ReleaseTag} already exists, skipping...", releaseTag);
			return false;
		}

		Log.Information("Creating draft release {ReleaseTag}...", releaseTag);

		var newRelease = new NewRelease(releaseTag) {
			TargetCommitish = gitVersion.Sha,
			Draft = true,
			Name = $"v{releaseTag}",
			Prerelease = packageVersion.IsPrerelease,
			Body = releaseNotes.Notes.Join("\n"),
		};

		var createdRelease = await ReleaseApi.Create(repositoryOwner, repositoryName, newRelease);

		foreach (var artifact in artifacts)
			await UploadReleaseAssetToGithub(createdRelease, artifact);

		Log.Information("Publishing release {ReleaseTag}...", releaseTag);
		await ReleaseApi.Edit(
			repositoryOwner, repositoryName, createdRelease.Id,
			new ReleaseUpdate { Draft = false });

		return true;
	}
}
