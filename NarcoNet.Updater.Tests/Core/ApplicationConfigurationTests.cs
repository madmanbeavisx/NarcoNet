using NarcoNet.Updater.Core;
using NarcoNet.Utilities;

namespace NarcoNet.Updater.Tests.Core;

public class ApplicationConfigurationTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesConfiguration()
    {
        // Arrange
        var processId = 1234;
        var isSilent = true;

        // Act
        ApplicationConfiguration config = new(processId, isSilent);

        // Assert
        config.TargetProcessId.Should().Be(processId);
        config.IsSilentMode.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidProcessId_ThrowsArgumentException(int invalidProcessId)
    {
        // Act
        Action act = () => new ApplicationConfiguration(invalidProcessId, false);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("targetProcessId")
            .WithMessage("*positive*");
    }

    [Fact]
    public void TryParseFromArguments_WithValidArguments_ReturnsConfiguration()
    {
        // Arrange
        string[] args = ["1234"];

        // Act
        ApplicationConfiguration? config = ApplicationConfiguration.TryParseFromArguments(args, out string? error);

        // Assert
        config.Should().NotBeNull();
        config!.TargetProcessId.Should().Be(1234);
        config.IsSilentMode.Should().BeFalse();
        error.Should().BeNull();
    }

    [Fact]
    public void TryParseFromArguments_WithSilentFlag_SetsSilentMode()
    {
        // Arrange
        string[] args = ["--silent", "5678"];

        // Act
        ApplicationConfiguration? config = ApplicationConfiguration.TryParseFromArguments(args, out string? error);

        // Assert
        config.Should().NotBeNull();
        config!.TargetProcessId.Should().Be(5678);
        config.IsSilentMode.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryParseFromArguments_WithEmptyArgs_ReturnsNullWithError()
    {
        // Arrange
        string[] args = [
        ];

        // Act
        ApplicationConfiguration? config = ApplicationConfiguration.TryParseFromArguments(args, out string? error);

        // Assert
        config.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("No arguments");
    }

    [Fact]
    public void TryParseFromArguments_WithOnlyFlags_ReturnsNullWithError()
    {
        // Arrange
        string[] args = ["--silent"];

        // Act
        ApplicationConfiguration? config = ApplicationConfiguration.TryParseFromArguments(args, out string? error);

        // Assert
        config.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("Missing required process ID");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12.34")]
    [InlineData("")]
    public void TryParseFromArguments_WithInvalidProcessId_ReturnsNullWithError(string invalidPid)
    {
        // Arrange
        string[] args = [invalidPid];

        // Act
        ApplicationConfiguration? config = ApplicationConfiguration.TryParseFromArguments(args, out string? error);

        // Assert
        config.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("Invalid process ID");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void TryParseFromArguments_WithZeroOrNegativeProcessId_ReturnsNullWithError(string processId)
    {
        // Arrange
        string[] args = [processId];

        // Act
        ApplicationConfiguration? config = ApplicationConfiguration.TryParseFromArguments(args, out string? error);

        // Assert
        config.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("Must be a positive integer");
    }

    [Fact]
    public void TryParseFromArguments_WithMultiplePositionalArgs_UsesLastOne()
    {
        // Arrange
        string[] args = ["1111", "2222", "3333"];

        // Act
        ApplicationConfiguration? config = ApplicationConfiguration.TryParseFromArguments(args, out string? error);

        // Assert
        config.Should().NotBeNull();
        config!.TargetProcessId.Should().Be(3333);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        ApplicationConfiguration config = new(1234, true);

        // Act
        var result = config.ToString();

        // Assert
        result.Should().Contain("1234");
        result.Should().Contain("ProcessId");
        result.Should().Contain("SilentMode");
        result.Should().Contain("True");
    }

    [Fact]
    public void UsageMessage_ContainsRequiredInformation()
    {
        // Act
        string message = ApplicationConfiguration.UsageMessage;

        // Assert
        message.Should().Contain("Usage:");
        message.Should().Contain("Process ID");
        message.Should().Contain("--silent");
        message.Should().Contain(NarcoNetConstants.UpdaterExecutableName);
    }
}
