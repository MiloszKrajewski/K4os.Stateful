using System;
using System.IO;
using System.Linq;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;

public class DockerTools(AbsolutePath dockerDirectory)
{
	readonly string TargetPrefix = GetDockerImagePrefix(dockerDirectory);

	static string GetDockerImagePrefix(AbsolutePath dockerDirectory)
	{
		var dockerEnvFile = dockerDirectory / ".env";
		if (!File.Exists(dockerEnvFile)) return null;
		
		return (
			from raw in File.ReadAllLines(dockerEnvFile)
			let line = RemoveComment(raw)
			where !string.IsNullOrWhiteSpace(line)
			let kv = line.Split('=', 2)
			where kv.Length > 1
			let k = kv[0].Trim()
			let v = kv[1].Trim()
			where k == "DOCKER_IMAGE_PREFIX"
			select v
		).SingleOrDefault();
	}

    static string RemoveComment(string raw) =>
		(raw.IndexOf('#') switch { < 0 => raw, var i => raw[..i] }).Trim();

	public AbsolutePath FindDockerFile(Project project)
	{
		return
			OnlyIfExists(dockerDirectory / $"{project.Name}.dockerfile") ??
			OnlyIfExists(dockerDirectory / "default.dockerfile");

		AbsolutePath OnlyIfExists(AbsolutePath path) => File.Exists(path) ? path : null;
	}

	public string GetDockerImageName(Project project)
	{
		var projectName = project.Name;
		var solutionName = project.Solution.Name;
		if (solutionName is not null && projectName.StartsWith(solutionName))
			projectName = projectName[(solutionName.Length + 1)..];
		return $"{TargetPrefix}/{GetDockerFriendlyName(projectName)}";
	}
	
	static string GetDockerFriendlyName(string name) => 
		name.Replace(".", "-").ToLowerInvariant();
}
