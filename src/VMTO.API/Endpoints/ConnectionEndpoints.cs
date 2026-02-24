using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Connection;

namespace VMTO.API.Endpoints;

public static class ConnectionEndpoints
{
    public static void MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/connections").WithTags("Connections");

        group.MapGet("/", ListConnections);
        group.MapGet("/{id:guid}", GetConnection);
        group.MapPost("/", CreateConnection);
        group.MapPost("/{id:guid}/validate", ValidateConnection);
        group.MapDelete("/{id:guid}", DeleteConnection);
    }

    private static async Task<IResult> ListConnections(
        IConnectionRepository repo,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var connections = await repo.ListAsync(page, pageSize, ct);
        var total = await repo.CountAsync(ct);
        return Results.Ok(new { items = connections.Select(MapToDto), total, page, pageSize });
    }

    private static async Task<IResult> GetConnection(Guid id, IConnectionRepository repo, CancellationToken ct)
    {
        var connection = await repo.GetByIdAsync(id, ct);
        if (connection is null) return Results.NotFound();
        return Results.Ok(MapToDto(connection));
    }

    private static async Task<IResult> CreateConnection(
        CreateConnectionRequest request,
        IConnectionRepository repo,
        IEncryptionService encryption,
        CancellationToken ct)
    {
        var encryptedSecret = encryption.Encrypt(request.Secret);
        var connection = new Connection(request.Name, request.Type, request.Endpoint, encryptedSecret);
        await repo.AddAsync(connection, ct);
        return Results.Created($"/api/connections/{connection.Id}", MapToDto(connection));
    }

    private static async Task<IResult> ValidateConnection(
        Guid id,
        IConnectionRepository repo,
        CancellationToken ct)
    {
        var connection = await repo.GetByIdAsync(id, ct);
        if (connection is null) return Results.NotFound();
        connection.MarkValidated();
        await repo.UpdateAsync(connection, ct);
        return Results.Ok(MapToDto(connection));
    }

    private static async Task<IResult> DeleteConnection(Guid id, IConnectionRepository repo, CancellationToken ct)
    {
        var connection = await repo.GetByIdAsync(id, ct);
        if (connection is null) return Results.NotFound();
        await repo.DeleteAsync(id, ct);
        return Results.NoContent();
    }

    private static ConnectionDto MapToDto(Connection c) =>
        new(c.Id, c.Name, c.Type, c.Endpoint, c.ValidatedAt, c.CreatedAt);
}

public sealed record CreateConnectionRequest(
    string Name,
    ConnectionType Type,
    string Endpoint,
    string Secret);
