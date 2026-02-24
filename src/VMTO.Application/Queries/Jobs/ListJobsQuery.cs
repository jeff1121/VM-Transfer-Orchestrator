using VMTO.Domain.Enums;

namespace VMTO.Application.Queries.Jobs;

public sealed record ListJobsQuery(int Page, int PageSize, JobStatus? Status);
