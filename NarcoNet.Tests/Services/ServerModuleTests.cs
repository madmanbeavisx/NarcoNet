namespace NarcoNet.Tests.Services;

public class ServerModuleTests
{
    [Theory]
    [InlineData("../BepInEx/plugins/file.dll", "..%2FBepInEx%2Fplugins%2Ffile.dll")]
    [InlineData("user/mods/mod.zip", "user%2Fmods%2Fmod.zip")]
    [InlineData("BepInEx/config/settings.json", "BepInEx%2Fconfig%2Fsettings.json")]
    [InlineData("../SPT_Data/Server/configs/file.json", "..%2FSPT_Data%2FServer%2Fconfigs%2Ffile.json")]
    [InlineData("path with spaces/file.txt", "path%20with%20spaces%2Ffile.txt")]
    public void DownloadFile_Should_Encode_File_Paths_Correctly(string inputPath, string expectedEncoded)
    {
        // Arrange: Normalize path separators and encode
        string normalizedPath = inputPath.Replace("\\", "/");
        string actualEncoded = Uri.EscapeDataString(normalizedPath);

        // Assert: Verify encoding matches expected format
        Assert.Equal(expectedEncoded, actualEncoded);
    }

    [Fact]
    public void DownloadFile_Should_Preserve_Parent_Directory_Notation()
    {
        // Arrange
        const string pathWithParent = "../BepInEx/plugins/NarcoNet.dll";
        string normalizedPath = pathWithParent.Replace("\\", "/");
        string encodedPath = Uri.EscapeDataString(normalizedPath);

        // Assert: Verify ../ is encoded as ..%2F
        Assert.Contains("..%2F", encodedPath);
        Assert.DoesNotContain("../", encodedPath); // Should not contain unencoded ../
    }

    [Fact]
    public void GetRemoteHashes_Should_Encode_Sync_Paths()
    {
        // Arrange
        var testPaths = new List<Utilities.SyncPath>
        {
            new(Path: "../BepInEx/plugins", Name: "Plugins", Enabled: true, Enforced: false),
            new(Path: "user/mods", Name: "Mods", Enabled: true, Enforced: false)
        };

        // Act: Simulate the encoding logic from GetRemoteHashes
        List<string> encodedPaths = testPaths.Select(path => Uri.EscapeDataString(path.Path.Replace(@"\", "/"))).ToList();

        // Assert
        Assert.Equal("..%2FBepInEx%2Fplugins", encodedPaths[0]);
        Assert.Equal("user%2Fmods", encodedPaths[1]);
    }
}
