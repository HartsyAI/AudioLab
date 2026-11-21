namespace AudioLab.Conventions.Tests;

/// <summary>The decisions behind the package half of the JavaScript policy, free of the file system so tests can feed them any listing.</summary>
public static class PackageScriptRules
{
    /// <summary>One line for each restored package whose script files are not all approved, by id, exact version and path.</summary>
    public static List<string> Unlisted(IEnumerable<PackageListing> restored, IEnumerable<PackageScriptAllowance> allowed)
    {
        List<PackageScriptAllowance> entries = [.. allowed];
        List<string> unlisted = [];
        foreach (PackageListing package in restored)
        {
            List<string> missing = [.. ScriptsOf(package).Where(script => !entries.Any(entry => Approves(entry, package, script)))];
            if (missing.Count > 0)
            {
                unlisted.Add(Describe(package, missing, entries));
            }
        }
        return unlisted;
    }

    /// <summary>One line for each entry that is blank, repeated, or names a script that its package version does not ship.</summary>
    public static List<string> EntryProblems(IEnumerable<PackageListing> restored, IEnumerable<PackageScriptAllowance> allowed)
    {
        List<PackageListing> packages = [.. restored];
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<string> problems = [];
        foreach (PackageScriptAllowance entry in allowed)
        {
            string name = $"{entry.PackageId} {entry.Version} {entry.Script}";
            if (string.IsNullOrWhiteSpace(entry.PackageId) || string.IsNullOrWhiteSpace(entry.Version)
                || string.IsNullOrWhiteSpace(entry.Script) || string.IsNullOrWhiteSpace(entry.Reason))
            {
                problems.Add($"'{name}' needs a package id, a version, a script path and a reason");
            }
            else if (!seen.Add($"{entry.PackageId}|{entry.Version}|{Normalise(entry.Script)}"))
            {
                problems.Add($"{name} is listed twice");
            }
            else
            {
                string? problem = Check(entry, name, packages);
                if (problem is not null)
                {
                    problems.Add(problem);
                }
            }
        }
        return problems;
    }

    private static string? Check(PackageScriptAllowance entry, string name, List<PackageListing> packages)
    {
        List<PackageListing> sameId = [.. packages.Where(package => IsSameId(package.Id, entry.PackageId))];
        PackageListing? exact = sameId.FirstOrDefault(package => string.Equals(package.Version, entry.Version, StringComparison.Ordinal));
        string? problem = null;
        if (exact is null)
        {
            string versions = string.Join(", ", sameId.Select(package => package.Version));
            string restored = sameId.Count == 0 ? "no version of it is restored" : $"restored: {versions}";
            problem = $"{name}: {entry.PackageId} {entry.Version} is not restored by any project ({restored}); remove or update the entry";
        }
        else if (!ScriptsOf(exact).Contains(Normalise(entry.Script), StringComparer.Ordinal))
        {
            problem = $"{name}: {entry.PackageId} {entry.Version} does not ship that script file; remove or correct the entry";
        }
        return problem;
    }

    private static bool Approves(PackageScriptAllowance entry, PackageListing package, string script)
    {
        return IsSameId(entry.PackageId, package.Id)
            && string.Equals(entry.Version, package.Version, StringComparison.Ordinal)
            && string.Equals(Normalise(entry.Script), script, StringComparison.Ordinal);
    }

    private static string Describe(PackageListing package, List<string> missing, List<PackageScriptAllowance> entries)
    {
        string projects = string.Join(", ", package.Projects);
        string fix = string.Join(", ", missing.Select(script => Line(package, script)));
        List<string> approvedAt = [.. entries.Where(entry => IsSameId(entry.PackageId, package.Id)).Select(entry => entry.Version).Distinct()];
        string note = approvedAt.Count == 0
            ? ""
            : $" ScriptAllowlist.Packages has entries for it at {string.Join(", ", approvedAt)}; they never carry over.";
        string shipped = $"ships {missing.Count} script file(s) that are not approved: {string.Join(", ", missing)}.";
        return $"{package.Id} {package.Version} (restored by {projects}) {shipped}{note} Add {fix} to ScriptAllowlist.Packages, or drop the package.";
    }

    private static string Line(PackageListing package, string script)
    {
        return $"new(\"{package.Id}\", \"{package.Version}\", \"{script}\", \"why C# cannot do it\")";
    }

    private static bool IsSameId(string left, string right)
    {
        return string.Equals(left, right, StringComparison.Ordinal);
    }

    private static List<string> ScriptsOf(PackageListing package)
    {
        return [.. package.Files.Where(ScriptFiles.IsScript).Select(Normalise)];
    }

    private static string Normalise(string path)
    {
        return path.Replace('\\', '/');
    }
}
