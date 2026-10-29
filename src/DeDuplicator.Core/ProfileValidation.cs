namespace DeDuplicator.Core;

public static class ProfileValidation
{
    public static ScanProfile Validate(ScanProfile profile, bool requireRoots = true)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Roots is null || profile.ExcludedDirectories is null || profile.ProtectedDirectories is null || profile.IncludedExtensions is null || profile.ExcludedExtensions is null)
            throw new ArgumentException("Profile lists must be arrays, not null.");
        if (requireRoots && profile.Roots.Length == 0) throw new ArgumentException("Choose at least one scan location.");
        if (profile.MinimumSize < 0 || profile.MaximumSize < profile.MinimumSize) throw new ArgumentException("Minimum and maximum sizes must be nonnegative and ordered.");
        static string[] Paths(string[] paths)
        {
            if (paths.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Profile paths cannot be empty.");
            return paths.Select(path => PathPolicy.Normalize(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        static string[] Extensions(string[] extensions)
        {
            if (extensions.Any(x => string.IsNullOrWhiteSpace(x) || x.IndexOfAny(['/', '\\', ':', '*', '?']) >= 0))
                throw new ArgumentException("Extensions must be literal extensions such as .pdf, without wildcards or paths.");
            return extensions.Select(x => x.StartsWith('.') ? x : "." + x).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        return profile with { Roots = PathPolicy.Roots(Paths(profile.Roots)), ExcludedDirectories = Paths(profile.ExcludedDirectories), ProtectedDirectories = Paths(profile.ProtectedDirectories), IncludedExtensions = Extensions(profile.IncludedExtensions), ExcludedExtensions = Extensions(profile.ExcludedExtensions) };
    }
}
