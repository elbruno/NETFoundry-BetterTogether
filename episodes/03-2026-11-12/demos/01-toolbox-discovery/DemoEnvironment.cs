// Purpose: Load this project's development bindings, with existing environment overrides.
// Preparation stores only named noncredential values; authentication still uses Azure identity.
using Microsoft.Extensions.Configuration;

internal static class DemoEnvironment
{
    public static string Read(string name)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddUserSecrets(typeof(DemoEnvironment).Assembly, optional: true);

        // Double underscores remain compatible with the original Decision environment bindings.
        string key = name.Replace("__", ":", StringComparison.Ordinal);
        string? value = Environment.GetEnvironmentVariable(name) ?? configuration[key];
        return !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException(
                $"Configure {key} with setup.ps1 or set {name} before running this demo.");
    }
}
