using System.Diagnostics;
using System.Text.RegularExpressions;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Shared;

namespace VMTO.Infrastructure.Clients;

public sealed partial class QemuImgService : IQemuImgService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

    public async Task<Result> ConvertAsync(string inputPath, string outputPath, ArtifactFormat targetFormat, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var format = targetFormat switch
        {
            ArtifactFormat.Qcow2 => "qcow2",
            ArtifactFormat.Raw => "raw",
            ArtifactFormat.Vmdk => "vmdk",
            _ => "qcow2"
        };

        var args = $"convert -p -O {format} \"{inputPath}\" \"{outputPath}\"";
        return await RunQemuImgAsync(args, progress, ct);
    }

    public async Task<Result<string>> GetInfoAsync(string imagePath, CancellationToken ct = default)
    {
        var args = $"info --output=json \"{imagePath}\"";
        var (exitCode, stdout, stderr) = await RunProcessAsync("qemu-img", args, null, ct);

        if (exitCode != 0)
            return Result<string>.Failure(ErrorCodes.General.ExternalCommandFailed, $"qemu-img info failed: {stderr}");

        return Result<string>.Success(stdout);
    }

    private static async Task<Result> RunQemuImgAsync(string args, IProgress<int>? progress, CancellationToken ct)
    {
        var (exitCode, _, stderr) = await RunProcessAsync("qemu-img", args, progress, ct);

        if (exitCode != 0)
            return Result.Failure(ErrorCodes.General.ExternalCommandFailed, $"qemu-img failed (exit {exitCode}): {stderr}");

        return Result.Success();
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(
        string fileName, string arguments, IProgress<int>? progress, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();

        using var registration = ct.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        });

        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        var stdoutBuilder = new System.Text.StringBuilder();
        while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
        {
            stdoutBuilder.AppendLine(line);

            if (progress is not null)
            {
                var match = ProgressPattern().Match(line);
                if (match.Success && double.TryParse(match.Groups[1].Value, out var pct))
                {
                    progress.Report(Math.Clamp((int)pct, 0, 100));
                }
            }
        }

        var stderr = await stderrTask;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(DefaultTimeout);
        await process.WaitForExitAsync(cts.Token);

        return (process.ExitCode, stdoutBuilder.ToString(), stderr);
    }

    [GeneratedRegex(@"(\d+\.?\d*)")]
    private static partial Regex ProgressPattern();
}
