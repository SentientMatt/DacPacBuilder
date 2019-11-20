using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;

namespace DacPacBuilder
{
	static class Program
	{
		static async Task Main(string[] args)
		{
			var command = CreateRootCommand();

			await command.InvokeAsync(args);
		}

		private static RootCommand CreateRootCommand()
		{
			Command buildCommand = CreateBuildCommand();

			return new RootCommand()
			{
				buildCommand
			};
		}

		private static Command CreateBuildCommand()
		{
			var buildCommand = new Command("build")
			{
				new Option(new []{"--output-path", "-o"})
				{
					Argument = new Argument<string>()
					{
						Arity = ArgumentArity.ExactlyOne
					},
					Description = "The path where the dacpac should be output to"
				},
				new Option(new []{"--target-version", "-t"})
				{
					Argument = new Argument<SqlServerVersion>(() => SqlServerVersion.Sql150)
					{
						Arity = ArgumentArity.ExactlyOne,
					},
					Description = "The Sql Server version to target",
				},
				new Argument<string>("project-path")
				{
					Arity = ArgumentArity.ExactlyOne,
					Description = "The path to the Sql Project file or folder containing the SQL scripts",
				},
			};

			buildCommand.Handler = CommandHandler.Create<string, string, SqlServerVersion>((projectPath, outputPath, targetVersion) => Build(projectPath, outputPath, targetVersion));

			return buildCommand;

		}

		private static void Build(string projectPath, string outputPath, SqlServerVersion targetVersion)
		{
			var outputFilePath = EnusreOutputPath(projectPath, outputPath);
			IEnumerable<string> dbFiles = GetInputFiles(projectPath);

			var sqlModel = new TSqlModel(targetVersion, new TSqlModelOptions()
			{

			});

			foreach (var scriptFile in dbFiles)
			{
				Console.WriteLine($"Loading file into model: {scriptFile}");
				var script = File.ReadAllText(scriptFile);
				sqlModel.AddObjects(script);
			}

			Console.WriteLine($"Building Dac Package to {outputFilePath}");
			DacPackageExtensions.BuildPackage(outputFilePath, sqlModel, new PackageMetadata()
			{

			});
		}

		private static IEnumerable<string> GetInputFiles(string projectPath)
		{
			Console.WriteLine("Getting input files for Sql Model");
			if (TryGetInputFilesFromProject(projectPath, out var filesFromProject))
				return filesFromProject;

			Console.WriteLine("No project file found, getting all sql files from project folder");
			return Directory.GetFiles(projectPath, "*.sql", SearchOption.AllDirectories)
							.Where(filePath => !filePath.Contains("/bin/") && !filePath.Contains(@"\bin\"));
		}

		private static bool TryGetInputFilesFromProject(string projectPath, out IEnumerable<string> inputFiles)
		{
			if (projectPath.EndsWith(".sqlproj") && File.Exists(projectPath))
			{
				inputFiles = GetInputFilesFromProject(Path.GetDirectoryName(projectPath), projectPath);
				return true;
			}

			var projectFiles = Directory.GetFiles(projectPath, "*.sqlproj");
			if (projectFiles.Length == 1)
			{
				inputFiles = GetInputFilesFromProject(projectPath, projectFiles[0]);
				return true;
			}

			inputFiles = Enumerable.Empty<string>();
			return false;
		}

		private static IEnumerable<string> GetInputFilesFromProject(string projectPath, string projectFilePath)
		{
			Console.WriteLine($"Getting input files from {projectFilePath}");
			var projectFile = XDocument.Load(projectFilePath);
			var projectFilesNodes = projectFile.Descendants().Where(node => node.Name.LocalName == "Build");
			return projectFilesNodes
				.Select(element => element.Attribute("Include").Value)
				.Select(relativePath => Path.GetFullPath(relativePath, projectPath));
		}

		private static string EnusreOutputPath(string projectPath, string outputPath)
		{
			outputPath ??= GetDefaultOutputPath(projectPath);
			if (outputPath.EndsWith(".dacpac", StringComparison.InvariantCultureIgnoreCase))
			{
				var directoryPath = Path.GetDirectoryName(outputPath);
				Directory.CreateDirectory(directoryPath);
				return outputPath;
			}

			Directory.CreateDirectory(outputPath);
			string projectName = GetProjectName(projectPath);
			return Path.Combine(outputPath, $"{projectName}.dacpac");
		}

		private static string GetDefaultOutputPath(string projectPath)
		{
			string basePath;
			if (projectPath.EndsWith(".sqlproj"))
				basePath = Path.GetDirectoryName(projectPath);
			else
				basePath = projectPath;

			return Path.Combine(basePath, "bin");
		}

		private static string GetProjectName(string projectPath)
		{
			return projectPath.Split(new[] { '\\', '/' }).Last().Replace(".sqlproj", "");
		}
	}
}
