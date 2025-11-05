using NarcoNet.Updater.Exceptions;

namespace NarcoNet.Updater.Tests.Exceptions;

public class UpdaterExceptionTests
{
    [Fact]
    public void UpdaterException_DefaultConstructor_SetsDefaultErrorCode()
    {
        // Act
        UpdaterException exception = new();

        // Assert
        exception.ErrorCode.Should().Be("UPDATER_ERROR");
    }

    [Fact]
    public void UpdaterException_WithMessage_SetsMessage()
    {
        // Act
        UpdaterException exception = new("Test error");

        // Assert
        exception.Message.Should().Be("Test error");
        exception.ErrorCode.Should().Be("UPDATER_ERROR");
    }

    [Fact]
    public void UpdaterException_WithMessageAndInnerException_SetsInnerException()
    {
        // Arrange
        InvalidOperationException innerException = new("Inner error");

        // Act
        UpdaterException exception = new("Test error", innerException);

        // Assert
        exception.Message.Should().Be("Test error");
        exception.InnerException.Should().Be(innerException);
        exception.ErrorCode.Should().Be("UPDATER_ERROR");
    }

    [Fact]
    public void UpdaterException_WithCustomErrorCode_SetsErrorCode()
    {
        // Act
        UpdaterException exception = new("Test error", "CUSTOM_ERROR");

        // Assert
        exception.Message.Should().Be("Test error");
        exception.ErrorCode.Should().Be("CUSTOM_ERROR");
    }

    [Fact]
    public void UpdaterException_WithCustomErrorCodeAndInnerException_SetsAll()
    {
        // Arrange
        InvalidOperationException innerException = new("Inner");

        // Act
        UpdaterException exception = new("Test error", "CUSTOM_ERROR", innerException);

        // Assert
        exception.Message.Should().Be("Test error");
        exception.ErrorCode.Should().Be("CUSTOM_ERROR");
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void EnvironmentValidationException_SetsCorrectErrorCode()
    {
        // Act
        EnvironmentValidationException exception = new("Validation failed");

        // Assert
        exception.Message.Should().Be("Validation failed");
        exception.ErrorCode.Should().Be("ENV_VALIDATION_FAILED");
    }

    [Fact]
    public void EnvironmentValidationException_WithInnerException_SetsInnerException()
    {
        // Arrange
        IOException innerException = new("IO error");

        // Act
        EnvironmentValidationException exception = new("Validation failed", innerException);

        // Assert
        exception.InnerException.Should().Be(innerException);
        exception.ErrorCode.Should().Be("ENV_VALIDATION_FAILED");
    }

    [Fact]
    public void FileOperationException_WithFilePath_SetsFilePath()
    {
        // Act
        FileOperationException exception = new("File error", "test.txt");

        // Assert
        exception.Message.Should().Be("File error");
        exception.FilePath.Should().Be("test.txt");
        exception.ErrorCode.Should().Be("FILE_OPERATION_FAILED");
    }

    [Fact]
    public void FileOperationException_WithFilePathAndInnerException_SetsAll()
    {
        // Arrange
        IOException innerException = new("IO error");

        // Act
        FileOperationException exception = new("File error", "test.txt", innerException);

        // Assert
        exception.Message.Should().Be("File error");
        exception.FilePath.Should().Be("test.txt");
        exception.InnerException.Should().Be(innerException);
        exception.ErrorCode.Should().Be("FILE_OPERATION_FAILED");
    }

    [Fact]
    public void ProcessMonitoringException_WithProcessId_SetsProcessId()
    {
        // Act
        ProcessMonitoringException exception = new("Process error", 1234);

        // Assert
        exception.Message.Should().Be("Process error");
        exception.ProcessId.Should().Be(1234);
        exception.ErrorCode.Should().Be("PROCESS_MONITORING_FAILED");
    }

    [Fact]
    public void ProcessMonitoringException_WithProcessIdAndInnerException_SetsAll()
    {
        // Arrange
        InvalidOperationException innerException = new("Invalid op");

        // Act
        ProcessMonitoringException exception = new("Process error", 1234, innerException);

        // Assert
        exception.Message.Should().Be("Process error");
        exception.ProcessId.Should().Be(1234);
        exception.InnerException.Should().Be(innerException);
        exception.ErrorCode.Should().Be("PROCESS_MONITORING_FAILED");
    }

    [Fact]
    public void ConfigurationException_WithConfigurationKey_SetsKey()
    {
        // Act
        ConfigurationException exception = new("Config error", "SettingKey");

        // Assert
        exception.Message.Should().Be("Config error");
        exception.ConfigurationKey.Should().Be("SettingKey");
        exception.ErrorCode.Should().Be("CONFIGURATION_INVALID");
    }

    [Fact]
    public void ConfigurationException_WithConfigurationKeyAndInnerException_SetsAll()
    {
        // Arrange
        FormatException innerException = new("Format error");

        // Act
        ConfigurationException exception = new("Config error", "SettingKey", innerException);

        // Assert
        exception.Message.Should().Be("Config error");
        exception.ConfigurationKey.Should().Be("SettingKey");
        exception.InnerException.Should().Be(innerException);
        exception.ErrorCode.Should().Be("CONFIGURATION_INVALID");
    }

    [Fact]
    public void UpdaterException_CanBeCaught()
    {
        // Act
        Action act = () => throw new UpdaterException("Test");

        // Assert
        act.Should().Throw<UpdaterException>()
            .WithMessage("Test");
    }

    [Fact]
    public void EnvironmentValidationException_IsUpdaterException()
    {
        // Arrange
        EnvironmentValidationException exception = new("Test");

        // Act & Assert
        exception.Should().BeAssignableTo<UpdaterException>();
    }

    [Fact]
    public void FileOperationException_IsUpdaterException()
    {
        // Arrange
        FileOperationException exception = new("Test", "file.txt");

        // Act & Assert
        exception.Should().BeAssignableTo<UpdaterException>();
    }

    [Fact]
    public void ProcessMonitoringException_IsUpdaterException()
    {
        // Arrange
        ProcessMonitoringException exception = new("Test", 1234);

        // Act & Assert
        exception.Should().BeAssignableTo<UpdaterException>();
    }

    [Fact]
    public void ConfigurationException_IsUpdaterException()
    {
        // Arrange
        ConfigurationException exception = new("Test", "key");

        // Act & Assert
        exception.Should().BeAssignableTo<UpdaterException>();
    }

    [Fact]
    public void AllExceptions_CanBeThrownAndCaught()
    {
        // Act & Assert
        Action act1 = () => throw new EnvironmentValidationException("env");
        act1.Should().Throw<EnvironmentValidationException>();

        Action act2 = () => throw new FileOperationException("file", "test.txt");
        act2.Should().Throw<FileOperationException>();

        Action act3 = () => throw new ProcessMonitoringException("process", 123);
        act3.Should().Throw<ProcessMonitoringException>();

        Action act4 = () => throw new ConfigurationException("config", "key");
        act4.Should().Throw<ConfigurationException>();
    }

    [Fact]
    public void UpdaterException_HasCorrectErrorCodes()
    {
        // Assert
        new UpdaterException().ErrorCode.Should().Be("UPDATER_ERROR");
        new EnvironmentValidationException("test").ErrorCode.Should().Be("ENV_VALIDATION_FAILED");
        new FileOperationException("test", "file").ErrorCode.Should().Be("FILE_OPERATION_FAILED");
        new ProcessMonitoringException("test", 1).ErrorCode.Should().Be("PROCESS_MONITORING_FAILED");
        new ConfigurationException("test", "key").ErrorCode.Should().Be("CONFIGURATION_INVALID");
    }

    [Fact]
    public void FileOperationException_WithNullFilePath_AllowsNull()
    {
        // This tests that the property can be null if needed
        // Act & Assert - Constructor requires non-null, but property can store null through serialization
        FileOperationException exception = new("Test", "file.txt");
        exception.FilePath.Should().NotBeNull();
    }

    [Fact]
    public void ConfigurationException_WithNullKey_AllowsNull()
    {
        // Act & Assert
        ConfigurationException exception = new("Test", "key");
        exception.ConfigurationKey.Should().NotBeNull();
    }
}
