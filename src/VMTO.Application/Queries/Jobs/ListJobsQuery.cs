using VMTO.Domain.Enums;

namespace VMTO.Application.Queries.Jobs;

public sealed record ListJobsQuery
{
    public int Page { get; }
    public int PageSize { get; }
    public JobStatus? Status { get; }

    public ListJobsQuery(int page, int pageSize, JobStatus? status = null)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be >= 1.");
        if (pageSize < 1 || pageSize > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be between 1 and 100.");

        Page = page;
        PageSize = pageSize;
        Status = status;
    }
}
