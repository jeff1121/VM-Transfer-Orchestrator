using VMTO.Domain.Aggregates.Artifact;

namespace VMTO.Application.DTOs;

public sealed record ArtifactDto(
    Guid Id,
    string FileName,
    ArtifactFormat Format,
    string ChecksumAlgorithm,
    string ChecksumValue,
    long SizeBytes,
    string StorageUri,
    DateTime CreatedAt);
