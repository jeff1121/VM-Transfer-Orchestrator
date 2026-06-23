namespace VMTO.Infrastructure.Resilience;

public enum ErrorCategory
{
    Unknown = 0,
    Transient = 1,
    Permanent = 2
}

public interface IErrorClassifier
{
    ErrorCategory Classify(string? errorMessage);
}
