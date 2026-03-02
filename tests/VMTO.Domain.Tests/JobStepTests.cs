using FluentAssertions;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Events;
using VMTO.Domain.ValueObjects;

namespace VMTO.Domain.Tests;

public sealed class JobStepTests
{
    private static JobStep CreateRunningStep()
    {
        var step = new JobStep(Guid.NewGuid(), "TestStep", 0, 3);
        step.Start();
        return step;
    }

    [Fact]
    public void CompleteShouldRaiseStepCompletedEvent()
    {
        var step = CreateRunningStep();

        var result = step.Complete();

        result.IsSuccess.Should().BeTrue();
        step.DomainEvents.Should().ContainSingle(e => e is StepCompletedEvent);
    }

    [Fact]
    public void FailShouldRaiseStepFailedEvent()
    {
        var step = CreateRunningStep();

        var result = step.Fail("something went wrong");

        result.IsSuccess.Should().BeTrue();
        step.DomainEvents.Should().ContainSingle(e => e is StepFailedEvent);
        var evt = (StepFailedEvent)step.DomainEvents[0];
        evt.Error.Should().Be("something went wrong");
    }

    [Fact]
    public void RetryShouldResetProgressToZero()
    {
        var step = CreateRunningStep();
        step.UpdateProgress(75);
        step.Fail("error");

        var result = step.Retry();

        result.IsSuccess.Should().BeTrue();
        step.Progress.Should().Be(0);
    }

    [Fact]
    public void SetLogsUriWhenRunningShouldSucceed()
    {
        var step = CreateRunningStep();

        var result = step.SetLogsUri("https://logs.example.com/step1");

        result.IsSuccess.Should().BeTrue();
        step.LogsUri.Should().Be("https://logs.example.com/step1");
    }

    [Fact]
    public void SetLogsUriWhenPendingShouldFail()
    {
        var step = new JobStep(Guid.NewGuid(), "TestStep", 0, 3);

        var result = step.SetLogsUri("https://logs.example.com/step1");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ClearDomainEventsShouldEmptyEventList()
    {
        var step = CreateRunningStep();
        step.Complete();
        step.DomainEvents.Should().HaveCount(1);

        step.ClearDomainEvents();

        step.DomainEvents.Should().BeEmpty();
    }
}

public sealed class ChecksumTests
{
    [Fact]
    public void ConstructorWithEmptyAlgorithmShouldThrow()
    {
        var act = () => new Checksum("", "abc123");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConstructorWithEmptyValueShouldThrow()
    {
        var act = () => new Checksum("SHA256", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConstructorWithValidArgsShouldSucceed()
    {
        var checksum = new Checksum("SHA256", "abc123");
        checksum.Algorithm.Should().Be("SHA256");
        checksum.Value.Should().Be("abc123");
    }
}

public sealed class StorageTargetTests
{
    [Fact]
    public void ConstructorWithEmptyEndpointShouldThrow()
    {
        var act = () => new StorageTarget(StorageType.S3, "", "bucket");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConstructorWithEmptyBucketOrPathShouldThrow()
    {
        var act = () => new StorageTarget(StorageType.S3, "https://s3.example.com", "");
        act.Should().Throw<ArgumentException>();
    }
}

public sealed class EncryptedSecretTests
{
    [Fact]
    public void ToStringShouldReturnRedacted()
    {
        var secret = new EncryptedSecret("super-secret-cipher-text");
        secret.ToString().Should().Be("[REDACTED]");
    }
}
