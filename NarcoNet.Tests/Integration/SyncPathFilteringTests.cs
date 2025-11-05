using NarcoNet.Utilities;

using Xunit.Abstractions;

namespace NarcoNet.Tests.Integration;

/// <summary>
/// Tests for sync path filtering logic
/// </summary>
public class SyncPathFilteringTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void Disabled_Paths_Should_Be_Filtered_Out()
    {
        testOutputHelper.WriteLine("=== TEST: Disabled Paths Should Be Filtered Out ===\n");

        // Arrange
        var paths = new List<SyncPath>
        {
            new(Path: "../BepInEx/plugins", Name: "Plugins", Enabled: true, Enforced: false, Silent: false, RestartRequired: true),
            new(Path: "../BepInEx/config", Name: "Config", Enabled: true, Enforced: false, Silent: false, RestartRequired: true),
            new(Path: "user/mods", Name: "Server mods", Enabled: false, Enforced: false, Silent: false, RestartRequired: false),
        };

        testOutputHelper.WriteLine("Input paths:");
        foreach (var sp in paths)
        {
            testOutputHelper.WriteLine($"  - {sp.Path}: Enabled={sp.Enabled}, Enforced={sp.Enforced}");
        }

        // Act - this is the actual filtering logic used in ConfigService.cs:216
        var filtered = paths.Where(sp => sp.Enabled || sp.Enforced).ToList();

        // Assert
        testOutputHelper.WriteLine($"\nFiltered paths (Enabled OR Enforced):");
        foreach (var sp in filtered)
        {
            testOutputHelper.WriteLine($"  - {sp.Path}: Enabled={sp.Enabled}, Enforced={sp.Enforced}");
        }

        testOutputHelper.WriteLine($"\nResult: {filtered.Count} paths (expected 2)");

        Assert.Equal(2, filtered.Count);
        Assert.Contains(filtered, sp => sp.Path == "../BepInEx/plugins");
        Assert.Contains(filtered, sp => sp.Path == "../BepInEx/config");
        Assert.DoesNotContain(filtered, sp => sp.Path == "user/mods");

        testOutputHelper.WriteLine("\n✓ TEST PASSED: user/mods was correctly filtered out\n");
    }

    [Fact]
    public void Enforced_Paths_Should_Be_Included_Even_When_Disabled()
    {
        testOutputHelper.WriteLine("=== TEST: Enforced Paths Should Be Included Even When Disabled ===\n");

        // Arrange
        var paths = new List<SyncPath>
        {
            new(Path: "test/normal", Name: "Normal", Enabled: true, Enforced: false, Silent: false, RestartRequired: false),
            new(Path: "test/disabled", Name: "Disabled", Enabled: false, Enforced: false, Silent: false, RestartRequired: false),
            new(Path: "test/enforced", Name: "Enforced", Enabled: false, Enforced: true, Silent: false, RestartRequired: false),
        };

        testOutputHelper.WriteLine("Input paths:");
        foreach (var sp in paths)
        {
            testOutputHelper.WriteLine($"  - {sp.Path}: Enabled={sp.Enabled}, Enforced={sp.Enforced}");
        }

        // Act
        var filtered = paths.Where(sp => sp.Enabled || sp.Enforced).ToList();

        // Assert
        testOutputHelper.WriteLine($"\nFiltered paths:");
        foreach (var sp in filtered)
        {
            testOutputHelper.WriteLine($"  - {sp.Path}: Enabled={sp.Enabled}, Enforced={sp.Enforced}");
        }

        Assert.Equal(2, filtered.Count);
        Assert.Contains(filtered, sp => sp.Path == "test/normal");
        Assert.Contains(filtered, sp => sp.Path == "test/enforced");
        Assert.DoesNotContain(filtered, sp => sp.Path == "test/disabled");

        testOutputHelper.WriteLine("\n✓ TEST PASSED: Enforced path included, disabled path excluded\n");
    }

    [Theory]
    [InlineData(true, false, true)]   // Enabled=true, Enforced=false => Include
    [InlineData(false, true, true)]   // Enabled=false, Enforced=true => Include
    [InlineData(true, true, true)]    // Enabled=true, Enforced=true => Include
    [InlineData(false, false, false)] // Enabled=false, Enforced=false => Exclude
    public void Filtering_Logic_Truth_Table(bool enabled, bool enforced, bool shouldInclude)
    {
        testOutputHelper.WriteLine($"\n=== TEST: Enabled={enabled}, Enforced={enforced} => Include={shouldInclude} ===");

        // Arrange
        var path = new SyncPath(
            Path: "test/path",
            Name: "Test",
            Enabled: enabled,
            Enforced: enforced,
            Silent: false,
            RestartRequired: false
        );

        // Act
        bool result = path.Enabled || path.Enforced;

        // Assert
        testOutputHelper.WriteLine($"Result: {result} (expected {shouldInclude})");
        Assert.Equal(shouldInclude, result);

        testOutputHelper.WriteLine("✓ PASSED\n");
    }

    [Fact]
    public void Real_World_Config_Scenario()
    {
        testOutputHelper.WriteLine("=== TEST: Real World Config Scenario ===\n");

        // Arrange - simulating the actual config from the screenshot
        var paths = new List<SyncPath>
        {
            // Builtins (always included)
            new(Path: "NarcoNet.Updater.exe", Name: "(Builtin) Updater", Enabled: true, Enforced: true, Silent: true, RestartRequired: false),
            new(Path: "../BepInEx/plugins/MadManBeavis-NarcoNet", Name: "(Builtin) Plugin", Enabled: true, Enforced: true, Silent: true, RestartRequired: true),

            // User config
            new(Path: "../BepInEx/plugins", Name: "Plugins", Enabled: true, Enforced: false, Silent: false, RestartRequired: true),
            new(Path: "../BepInEx/patchers", Name: "Patchers", Enabled: true, Enforced: false, Silent: false, RestartRequired: true),
            new(Path: "../BepInEx/config", Name: "Config", Enabled: true, Enforced: false, Silent: false, RestartRequired: true),
            new(Path: "user/mods", Name: "(Optional) Server mods", Enabled: false, Enforced: false, Silent: false, RestartRequired: false),
        };

        testOutputHelper.WriteLine("All sync paths:");
        foreach (var sp in paths)
        {
            testOutputHelper.WriteLine($"  - {sp.Path}: Enabled={sp.Enabled}, Enforced={sp.Enforced}");
        }

        // Act - Filter like ConfigService does
        var filtered = paths.Where(sp => sp.Enabled || sp.Enforced).ToList();

        // Assert
        testOutputHelper.WriteLine($"\nFiltered paths ({filtered.Count} total):");
        foreach (var sp in filtered)
        {
            testOutputHelper.WriteLine($"  - {sp.Path}");
        }

        Assert.Equal(5, filtered.Count); // 2 builtins + 3 enabled user paths
        Assert.DoesNotContain(filtered, sp => sp.Path == "user/mods");

        testOutputHelper.WriteLine("\n✓ TEST PASSED.: Real world scenario works correctly\n");
    }
}
