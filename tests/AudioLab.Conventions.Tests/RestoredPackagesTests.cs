namespace AudioLab.Conventions.Tests;

/// <summary>Holds the reader of restore output to failing loudly, so the package gate cannot pass on a guess.</summary>
public sealed class RestoredPackagesTests
{
    private const string Project = "src/Acme/Acme.csproj";

    private const string Fixture = """
        {
          "version": 4,
          "libraries": {
            "Acme.Widgets/1.2.0": {
              "type": "package",
              "files": [ "lib/net10.0/Acme.Widgets.dll", "staticwebassets/widgets.js" ]
            },
            "Acme.Plain/3.0.0": { "type": "package", "files": [] },
            "Acme.Core/1.0.0": { "type": "project", "path": "../Core/Core.csproj" }
          }
        }
        """;

    [Fact]
    public void ReadsPackagesAndTheirFilesAndSkipsProjects()
    {
        List<PackageListing> packages = RestoredPackages.Parse(Fixture, Project);
        Assert.Equal(["Acme.Widgets", "Acme.Plain"], packages.Select(package => package.Id));
        PackageListing widgets = packages[0];
        Assert.Equal("1.2.0", widgets.Version);
        Assert.Equal(["lib/net10.0/Acme.Widgets.dll", "staticwebassets/widgets.js"], widgets.Files);
        Assert.Equal([Project], widgets.Projects);
        Assert.Empty(packages[1].Files);
    }

    [Fact]
    public void ParsedPackageShippingAScriptFlowsIntoTheRule()
    {
        List<string> unlisted = PackageScriptRules.Unlisted(RestoredPackages.Parse(Fixture, Project), []);
        string line = Assert.Single(unlisted);
        Assert.Contains("Acme.Widgets 1.2.0", line, StringComparison.Ordinal);
        Assert.Contains(Project, line, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageWithoutAFileListFailsInsteadOfCountingAsEmpty()
    {
        const string Json = """{ "version": 4, "libraries": { "Acme.Widgets/1.2.0": { "type": "package" } } }""";
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RestoredPackages.Parse(Json, Project));
        Assert.Contains("Acme.Widgets/1.2.0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingLibrariesSectionFails()
    {
        Assert.Throws<InvalidOperationException>(() => RestoredPackages.Parse("""{ "version": 4 }""", Project));
    }

    [Fact]
    public void UnknownRestoreOutputVersionFails()
    {
        const string Json = """{ "version": 9, "libraries": {} }""";
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RestoredPackages.Parse(Json, Project));
        Assert.Contains("version 9", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyDocumentFails()
    {
        Assert.Throws<InvalidOperationException>(() => RestoredPackages.Parse("null", Project));
    }

    [Fact]
    public void PackageKeyWithoutAVersionFails()
    {
        const string Json = """{ "version": 4, "libraries": { "Acme.Widgets": { "type": "package", "files": [] } } }""";
        Assert.Throws<InvalidOperationException>(() => RestoredPackages.Parse(Json, Project));
    }

    [Fact]
    public void ReadFindsThisTestProjectsPackagesWithTheirFiles()
    {
        PackageListing xunit = RestoredPackages.Read().First(package => package.Id == "xunit.core");
        Assert.NotEmpty(xunit.Files);
        Assert.Contains(xunit.Projects, project => project.EndsWith("AudioLab.Conventions.Tests.csproj", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Package")]
    [InlineData("weird")]
    [InlineData("")]
    public void LibraryOfAnUnknownTypeFailsInsteadOfBeingSkipped(string type)
    {
        string json = $$"""{ "version": 4, "libraries": { "Acme.Widgets/1.2.0": { "type": "{{type}}", "files": ["a.js"] } } }""";
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RestoredPackages.Parse(json, Project));
        Assert.Contains("Acme.Widgets/1.2.0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LibraryWithoutATypeFails()
    {
        const string Json = """{ "version": 4, "libraries": { "Acme.Widgets/1.2.0": { "files": ["a.js"] } } }""";
        Assert.Throws<InvalidOperationException>(() => RestoredPackages.Parse(Json, Project));
    }

    [Fact]
    public void NullLibraryEntryFailsWithAMessageNamingIt()
    {
        const string Json = """{ "version": 4, "libraries": { "Acme.Widgets/1.2.0": null } }""";
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RestoredPackages.Parse(Json, Project));
        Assert.Contains("Acme.Widgets/1.2.0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadFindsAProjectNestedBelowTheFirstLevel()
    {
        using TempRepository repository = new();
        repository.AddProject("src/Deep/Inner/Inner.csproj", Fixture);
        repository.AddProject("tests/Top/Top.csproj", Fixture);
        List<PackageListing> packages = RestoredPackages.Read(repository.Root);
        PackageListing widgets = packages.Single(package => package.Id == "Acme.Widgets");
        Assert.Equal(["src/Deep/Inner/Inner.csproj", "tests/Top/Top.csproj"], widgets.Projects.Order());
    }

    [Fact]
    public void ReadFailsForAProjectWithoutRestoreOutput()
    {
        using TempRepository repository = new();
        repository.AddProject("src/Deep/Inner/Inner.csproj", null);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RestoredPackages.Read(repository.Root));
        Assert.Contains("src/Deep/Inner/Inner.csproj has no obj/project.assets.json", error.Message, StringComparison.Ordinal);
        Assert.Contains("AudioLab.slnx", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadFailsWhenThereAreNoProjects()
    {
        using TempRepository repository = new();
        Assert.Throws<InvalidOperationException>(() => RestoredPackages.Read(repository.Root));
    }

    [Fact]
    public void ReadIgnoresALeftoverObjFolderThatHasNoProject()
    {
        using TempRepository repository = new();
        repository.AddProject("src/Live/Live.csproj", Fixture);
        repository.AddAssets("src/Gone", Fixture.Replace("Acme.Widgets", "Acme.Stale", StringComparison.Ordinal));
        Assert.DoesNotContain(RestoredPackages.Read(repository.Root), package => package.Id == "Acme.Stale");
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("[1, 2]")]
    [InlineData("""{ "version": "four", "libraries": {} }""")]
    [InlineData("""{ "version": 4, "libraries": 5 }""")]
    [InlineData("""{ "version": 4, "libraries": { "Acme.Widgets/1.2.0": { "type": "package", "files": "a.js" } } }""")]
    public void UnreadableRestoreOutputFailsNamingTheProjectAndWhatToRun(string json)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RestoredPackages.Parse(json, Project));
        Assert.Contains(Project, error.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet restore", error.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(error.InnerException);
    }

    [Fact]
    public void RepeatedLibraryKeyFailsInsteadOfTheLastOneWinning()
    {
        const string Json = """
            {
              "version": 4,
              "libraries": {
                "Acme.Widgets/1.2.0": { "type": "package", "files": [ "staticwebassets/widgets.js" ] },
                "Acme.Widgets/1.2.0": { "type": "project", "path": "../Widgets/Widgets.csproj" }
              }
            }
            """;
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RestoredPackages.Parse(Json, Project));
        Assert.Contains(Project, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "version": 4, "libraries": { "Acme.Plain/3.0.0": { "type": "package", "files": [] } }, "libraries": {} }""")]
    [InlineData("""{ "version": 4, "libraries": {}, "Libraries": { "Acme.Widgets/1.2.0": { "type": "package", "files": [] } } }""")]
    [InlineData("""{ "version": 4, "version": 4, "libraries": {} }""")]
    [InlineData("""{ "version": 4, "libraries": { "Acme.Widgets/1.2.0": { "type": "project", "type": "package", "files": [] } } }""")]
    public void RepeatedPropertyFails(string json)
    {
        Assert.Throws<InvalidOperationException>(() => RestoredPackages.Parse(json, Project));
    }

    [Fact]
    public void EveryProjectInTheSolutionIsFoundByTheScan()
    {
        Assert.Empty(RepoFiles.SolutionProjectsTheScanMisses(RepoFiles.Root));
    }

    [Theory]
    [InlineData("src/node_modules/Hidden/Hidden.csproj")]
    [InlineData("src/Hidden/obj/Hidden.csproj")]
    [InlineData("tests/artifacts/Hidden/Hidden.csproj")]
    [InlineData("tools/Outside/Outside.csproj")]
    public void SolutionProjectTheScanSkipsOrNeverReachesIsReported(string hidden)
    {
        using TempRepository repository = new();
        repository.AddProject("src/Live/Live.csproj", Fixture);
        repository.AddProject(hidden, Fixture);
        repository.AddSolution("src/Live/Live.csproj", hidden);
        Assert.Equal([hidden], RepoFiles.SolutionProjectsTheScanMisses(repository.Root));
    }

    [Fact]
    public void SolutionProjectsWrittenWithBackslashesAreMatched()
    {
        using TempRepository repository = new();
        repository.AddProject("src/Live/Live.csproj", Fixture);
        repository.AddSolution("src\\Live\\Live.csproj");
        Assert.Empty(RepoFiles.SolutionProjectsTheScanMisses(repository.Root));
    }
}
