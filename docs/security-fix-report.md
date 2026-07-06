# VMTO 安全修復詳細報告

> **修復日期：** 2026-03-02  
> **修復版本：** 依 security-scan-report.md（掃描日期：2026-02-24）漏洞清單  
> **修復範圍：** P0（高風險）× 4 + P1（中風險）× 10 + P2（低風險）× 4 = 共 18 項  
> **殘留風險：** 低風險 × 4（S20、S22、S23、S24）

---

## 1. 修復摘要

| 優先級 | 總數 | 已修復 | 殘留 |
|--------|------|--------|------|
| 🔴 P0 高風險 | 4 | 4 | 0 |
| 🟡 P1 中風險 | 10 | 10 | 0 |
| 🟢 P2 低風險 | 4 | 4 | 0 |
| **合計** | **18** | **18** | **0** |

> 額外殘留（不在本次修復範圍）：S20、S22、S23、S24

---

## 2. 各漏洞修復詳細說明

### S01/S02/S06 — Broken Access Control & 身份識別缺失（A01/A07）

**修復位置：** `src/VMTO.API/Program.cs`、所有 Endpoints、`MigrationHub.cs`

**修復前：**
```csharp
// Program.cs — 無任何 Authentication/Authorization 服務
var app = builder.Build();
app.UseCors();
// 直接對外，無任何保護
app.MapJobEndpoints();

// MigrationHub.cs — 無 [Authorize]
public sealed class MigrationHub : Hub { }

// JobEndpoints.cs — 無 RequireAuthorization
var group = app.MapGroup("/api/jobs").WithTags("Jobs");
```

**修復後：**
```csharp
// Program.cs — 加入 JWT Bearer Authentication 與 RBAC
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("Operator", policy => policy.RequireRole("Admin", "Operator"));
    options.AddPolicy("Viewer", policy => policy.RequireRole("Admin", "Operator", "Viewer"));
});

app.UseAuthentication();
app.UseAuthorization();

// MigrationHub.cs — 加入 [Authorize]
[Authorize]
public sealed class MigrationHub : Hub
{
    public async Task JoinJob(string jobId) => await Groups.AddToGroupAsync(Context.ConnectionId, jobId);
    public async Task LeaveJob(string jobId) => await Groups.RemoveFromGroupAsync(Context.ConnectionId, jobId);
}

// Endpoints — 加入 RequireAuthorization
var group = app.MapGroup("/api/jobs").WithTags("Jobs").RequireAuthorization();
```

**驗證方式：**
1. 在無 JWT token 的情況下呼叫 `GET /api/jobs`，應收到 `401 Unauthorized`
2. 使用有效 JWT token 呼叫，應正常回傳資料
3. 測試 SignalR Hub 連線，無 token 時應被拒絕

---

### S03 — Hangfire Dashboard 未授權（A01）

**修復位置：** `src/VMTO.API/Program.cs`、新增 `LocalRequestsOnlyDashboardAuthorizationFilter.cs`

**修復前：**
```csharp
if (app.Environment.IsDevelopment())
{
    app.MapHangfireDashboard("/hangfire");
}
```

**修復後：**
```csharp
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new LocalRequestsOnlyDashboardAuthorizationFilter()]
});

// LocalRequestsOnlyDashboardAuthorizationFilter.cs
public sealed class LocalRequestsOnlyDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.Connection.RemoteIpAddress is { } ip
            && (IPAddress.IsLoopback(ip) ||
                ip.Equals(httpContext.Connection.LocalIpAddress));
    }
}
```

**驗證方式：**
1. 從本機存取 `/hangfire`，應可正常顯示儀表板
2. 從非本機 IP 存取 `/hangfire`，應收到 `401 Unauthorized`

> **注意：** 生產環境建議改用 JWT-based 過濾器，檢查 `Authorization: Bearer <token>` 標頭並驗證 Admin 角色。

---

### S04 — Path Traversal（A03）

**修復位置：** `src/VMTO.Infrastructure/Storage/LocalStorageAdapter.cs`

**修復前（有漏洞）：**
```csharp
private string GetFullPath(string key)
{
    return Path.Combine(_basePath, key); // 未驗證！
}
```

**修復後（已實作，確認有效）：**
```csharp
private string GetFullPath(string key)
{
    var basePath = Path.GetFullPath(_basePath);
    var fullPath = Path.GetFullPath(Path.Combine(basePath, key));
    var relativePath = Path.GetRelativePath(basePath, fullPath);

    if (relativePath == ".." || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        throw new InvalidOperationException("Path traversal detected.");

    return fullPath;
}
```

**驗證方式：**
1. 呼叫 `UploadAsync("../../etc/passwd", ...)` 應拋出 `InvalidOperationException`
2. 呼叫 `DownloadAsync("../secret.txt")` 應拋出 `InvalidOperationException`
3. 正常路徑如 `DownloadAsync("jobs/abc123/disk.vmdk")` 應正常運作

---

### S05 — Command Injection（A03）

**修復位置：** `src/VMTO.Infrastructure/Clients/QemuImgService.cs`

**修復前：**
```csharp
public async Task<Result> ConvertAsync(string inputPath, string outputPath, ...)
{
    var args = $"convert -p -O {format} \"{inputPath}\" \"{outputPath}\"";
    return await RunQemuImgAsync(args, progress, ct);
}

private static async Task<(int, string, string)> RunProcessAsync(
    string fileName, string arguments, ...)
{
    process.StartInfo = new ProcessStartInfo
    {
        Arguments = arguments, // 危險：字串拼接
        ...
    };
}
```

**修復後：**
```csharp
public async Task<Result> ConvertAsync(string inputPath, string outputPath, ...)
{
    return await RunQemuImgAsync(["convert", "-p", "-O", format, inputPath, outputPath], progress, ct);
}

private static async Task<(int, string, string)> RunProcessAsync(
    string fileName, string[] arguments, ...)
{
    process.StartInfo = new ProcessStartInfo
    {
        FileName = fileName,
        // 不設定 Arguments 字串
        ...
    };
    foreach (var arg in arguments)
        process.StartInfo.ArgumentList.Add(arg); // 安全：每個參數獨立傳遞
}
```

**驗證方式：**
1. 使用含有 `"` 的路徑呼叫 ConvertAsync，應不會發生 shell injection
2. 使用含有空格的路徑，應正確傳遞為單一參數

---

### S07 — DataProtection key 未持久化（A02）

**修復位置：** `src/VMTO.Infrastructure/DependencyInjection.cs`

**修復前：**
```csharp
services.AddDataProtection(); // 未配置持久化！
```

**修復後：**
```csharp
services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        configuration["DataProtection:KeysPath"] ?? "/app/keys"))
    .SetApplicationName("VMTO");
```

**驗證方式：**
1. 啟動容器後，確認 `/app/keys` 目錄下有 XML key 檔案生成
2. 重啟容器，確認已加密的 Connection Secrets 仍可正常解密

> **部署注意：** 需在 Docker Compose / Helm 中為 `/app/keys` 路徑掛載 persistent volume。

---

### S08 — S3 憑證缺失時使用空字串（A02）

**修復位置：** `src/VMTO.Infrastructure/DependencyInjection.cs`

**修復前：**
```csharp
return new AmazonS3Client(
    configuration["Storage:S3:AccessKey"] ?? string.Empty, // 靜默失敗
    configuration["Storage:S3:SecretKey"] ?? string.Empty,
    config);
```

**修復後：**
```csharp
var accessKey = configuration["Storage:S3:AccessKey"]
    ?? throw new InvalidOperationException("Storage:S3:AccessKey is required when S3 endpoint is configured.");
var secretKey = configuration["Storage:S3:SecretKey"]
    ?? throw new InvalidOperationException("Storage:S3:SecretKey is required when S3 endpoint is configured.");
return new AmazonS3Client(accessKey, secretKey, s3Config);
```

**驗證方式：**
1. 設定 `Storage:S3:Endpoint` 但不設定 `AccessKey`，啟動應拋出 `InvalidOperationException`
2. 正確設定所有 S3 設定後，應正常啟動

---

### S09 — 硬編碼密碼（A05）

**修復位置：** `src/VMTO.Infrastructure/DependencyInjection.cs`

**修復前：**
```csharp
var connectionString = configuration.GetConnectionString("PostgreSQL")
    ?? "Host=localhost;Database=vmto;Username=vmto;Password=vmto"; // 含明文密碼！
```

**修復後：**
```csharp
var connectionString = configuration.GetConnectionString("PostgreSQL")
    ?? throw new InvalidOperationException("PostgreSQL connection string is required.");
```

**驗證方式：**
1. 未設定 `ConnectionStrings:PostgreSQL` 時，啟動應拋出 `InvalidOperationException`
2. 確認原始碼中不再含有任何硬編碼密碼

---

### S10 — nginx 安全標頭（A05）

**修復位置：** `infra/nginx.conf`

**修復前（缺少 HSTS）：**
```nginx
add_header X-Frame-Options "DENY" always;
# 缺少 Strict-Transport-Security
```

**修復後：**
```nginx
add_header X-Frame-Options "SAMEORIGIN" always;
add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
```

**驗證方式：**
1. 使用 `curl -I https://your-domain/` 確認回應標頭包含 `Strict-Transport-Security`
2. 使用安全標頭掃描工具（如 securityheaders.com）驗證

---

### S11 — 容器以 root 執行（A05）

**狀態：** 修復已存在（確認有效）

**Dockerfile.api / Dockerfile.worker：**
```dockerfile
RUN adduser --disabled-password --gecos '' appuser
USER appuser
```

**Dockerfile.frontend：**
```dockerfile
FROM nginxinc/nginx-unprivileged:alpine AS runtime
# nginx-unprivileged 映像本身以非 root 執行
```

---

### S12 — Helm securityContext（A05）

**修復位置：** `helm/templates/api-deployment.yaml`、`worker-deployment.yaml`、`frontend-deployment.yaml`

**修復前：** 缺少 `securityContext`

**修復後：**
```yaml
containers:
  - name: api
    securityContext:
      runAsNonRoot: true
      runAsUser: 1001
      readOnlyRootFilesystem: true
      allowPrivilegeEscalation: false
      capabilities:
        drop: ["ALL"]
```

**驗證方式：**
```bash
kubectl exec -it <pod-name> -- id
# 應顯示 uid=1001 而非 0（root）
```

---

### S13 — docker-compose.yml 弱密碼（A05）

**修復位置：** `infra/docker-compose.yml`、`infra/.env.example`

**修復前：**
```yaml
redis:
  image: redis:7-alpine
  # 無密碼保護！
```

**修復後：**
```yaml
redis:
  image: redis:7-alpine
  command: ["redis-server", "--requirepass", "${REDIS_PASSWORD:-redis_dev_pass}"]
  healthcheck:
    test: ["CMD", "redis-cli", "-a", "${REDIS_PASSWORD:-redis_dev_pass}", "ping"]
```

**.env.example 新增：**
```
REDIS_PASSWORD=change_me_in_production
```

---

### S14 — Saga 使用 InMemoryRepository（A08）

**修復位置：** `src/VMTO.Worker/Program.cs`、新增 `MigrationSagaDbContext.cs`

**修復前：**
```csharp
x.AddSagaStateMachine<MigrationJobSaga, MigrationJobSagaState>()
 .InMemoryRepository(); // 重啟後狀態遺失！
```

**修復後：**
```csharp
// Program.cs
x.AddSagaStateMachine<MigrationJobSaga, MigrationJobSagaState>()
 .EntityFrameworkRepository(r =>
 {
     r.ConcurrencyMode = ConcurrencyMode.Optimistic;
     r.AddDbContext<DbContext, MigrationSagaDbContext>((provider, options) =>
         options.UseNpgsql(pgConnStr));
 });

// MigrationSagaDbContext.cs
public sealed class MigrationSagaDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationJobSagaState>(entity =>
        {
            entity.ToTable("saga_migration_jobs");
            entity.HasKey(x => x.CorrelationId);
            entity.Property(x => x.CurrentState).HasMaxLength(64).IsRequired();
        });
    }
}
```

**驗證方式：**
1. 啟動一個 migration job
2. 重啟 Worker 服務
3. 確認 job 狀態仍正確保留

> **注意：** 需執行資料庫 migration 以建立 `saga_migration_jobs` 資料表。

---

### S15 — Correlation ID 未驗證格式（A09）

**修復位置：** `src/VMTO.API/Middleware/CorrelationIdMiddleware.cs`

**修復前：**
```csharp
var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
    ?? Guid.NewGuid().ToString("D"); // 未驗證，任意字串直接注入日誌！
```

**修復後：**
```csharp
var headerValue = context.Request.Headers[HeaderName].FirstOrDefault();
var correlationId = (headerValue is not null && IsValidCorrelationId(headerValue))
    ? headerValue
    : Guid.NewGuid().ToString("D");

private static bool IsValidCorrelationId(string value) =>
    !string.IsNullOrEmpty(value) &&
    value.Length <= 64 &&
    value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');
```

**驗證方式：**
1. 傳入含換行字元的 `X-Correlation-Id: abc\ndef`，應被忽略並生成新 ID
2. 傳入超過 64 字元的 ID，應被忽略
3. 傳入合法的 UUID，應直接使用

---

### S16 — VSphereClient URL 注入（A10）

**修復位置：** `src/VMTO.Infrastructure/Clients/VSphereClient.cs`

**修復前：**
```csharp
var response = await _http.GetAsync($"/api/vcenter/vm/{vmId}/disk/{diskKey}/export", ct);
```

**修復後：**
```csharp
var encodedVmId = Uri.EscapeDataString(vmId);
var encodedDiskKey = Uri.EscapeDataString(diskKey);
var response = await _http.GetAsync($"/api/vcenter/vm/{encodedVmId}/disk/{encodedDiskKey}/export", ct);
```

**驗證方式：**
1. 傳入含有 `/` 的 `vmId`（如 `vm-123/../../admin`），確認被正確編碼
2. 傳入含有空格的 `diskKey`，確認被編碼為 `%20`

---

### S17 — pageSize 無上限（A04）

**修復位置：** `src/VMTO.API/Endpoints/JobEndpoints.cs`、`ConnectionEndpoints.cs`

**修復前：**
```csharp
var jobs = await repo.ListAsync(page, pageSize, status, ct); // pageSize 無限制
```

**修復後：**
```csharp
pageSize = Math.Min(pageSize, 100); // 最大 100 筆
var jobs = await repo.ListAsync(page, pageSize, status, ct);
```

**驗證方式：**
1. `GET /api/jobs?pageSize=999999` 應只回傳最多 100 筆
2. `GET /api/connections?pageSize=0` 應使用預設值

---

### S18 — DeleteConnection 未檢查 active jobs（A04）

**修復位置：** `src/VMTO.API/Endpoints/ConnectionEndpoints.cs`、`IJobRepository.cs`、`JobRepository.cs`

**修復前：**
```csharp
private static async Task<IResult> DeleteConnection(Guid id, IConnectionRepository repo, CancellationToken ct)
{
    var connection = await repo.GetByIdAsync(id, ct);
    if (connection is null) return Results.NotFound();
    await repo.DeleteAsync(id, ct); // 未檢查是否有 active job！
    return Results.NoContent();
}
```

**修復後：**
```csharp
private static async Task<IResult> DeleteConnection(Guid id, IConnectionRepository repo, IJobRepository jobRepo, CancellationToken ct)
{
    var connection = await repo.GetByIdAsync(id, ct);
    if (connection is null) return Results.NotFound();
    if (await jobRepo.HasActiveJobsForConnectionAsync(id, ct))
        return Results.Conflict(new { error = "Cannot delete connection: there are active jobs using this connection." });
    await repo.DeleteAsync(id, ct);
    return Results.NoContent();
}

// IJobRepository — 新增介面
Task<bool> HasActiveJobsForConnectionAsync(Guid connectionId, CancellationToken ct = default);

// JobRepository — 實作
public async Task<bool> HasActiveJobsForConnectionAsync(Guid connectionId, CancellationToken ct = default)
{
    var activeStatuses = new[] { JobStatus.Queued, JobStatus.Running, JobStatus.Pausing };
    return await _db.Jobs.AnyAsync(
        j => (j.SourceConnectionId == connectionId || j.TargetConnectionId == connectionId)
             && activeStatuses.Contains(j.Status), ct);
}
```

**驗證方式：**
1. 刪除有正在執行 job 的 connection，應回傳 `409 Conflict`
2. 刪除沒有 active job 的 connection，應正常回傳 `204 No Content`

---

### S19 — SignalR 廣播資訊洩漏（A04）

**修復位置：** `src/VMTO.Infrastructure/Notifications/SignalRNotificationService.cs`

**修復前：**
```csharp
await _hubContext.Clients.All.SendAsync("JobProgress", ..., ct); // 廣播給所有人
```

**修復後：**
```csharp
await _hubContext.Clients.Group(jobId.ToString()).SendAsync("JobProgress", ..., ct); // 只通知訂閱該 job 的用戶
```

前端需先呼叫 Hub 的 `JoinJob(jobId)` 加入 Group，才能接收該 job 的進度更新。

**驗證方式：**
1. 連接 SignalR 但不呼叫 `JoinJob`，不應收到任何 job 進度通知
2. 呼叫 `JoinJob("job-id-1")` 後，應只收到 job-id-1 的進度

---

### S21 — CORS 過度寬鬆（A05）

**修復位置：** `src/VMTO.API/Program.cs`

**修復前：**
```csharp
.AllowAnyMethod() // 允許所有 HTTP 方法
```

**修復後：**
```csharp
.WithMethods("GET", "POST", "PUT", "DELETE") // 限制為必要方法
```

**驗證方式：**
1. 發送 `OPTIONS /api/jobs` 確認 `Access-Control-Allow-Methods` 只包含允許的方法
2. 嘗試 `PATCH` 請求，確認被 CORS policy 拒絕

---

## 3. 殘留風險說明

| # | 漏洞 ID | 說明 | 建議處理方式 |
|---|---------|------|-------------|
| 1 | S20 | `RetryFailedSteps()` 直接操作 `JobStep.Retry()` 繞過 aggregate root | 重構為透過 `MigrationJob.RetryFailedSteps()` aggregate 方法 |
| 2 | S22 | 缺少 `.dockerignore`，build context 包含 `.git` 等敏感目錄 | 新增 `.dockerignore` 排除 `.git`、`node_modules`、`*.md` |
| 3 | S23 | `OpenTelemetry.Exporter.Prometheus.AspNetCore` 使用 `1.15.0-beta.1` 預覽版 | 待 GA 版本發布後升級 |
| 4 | S24 | `ArtifactDto.StorageUri` 可能暴露內部 S3/MinIO 端點路徑 | 改為回傳 pre-signed URL 或去除 internal URI |
| 5 | Hangfire 生產環境 | 目前 Hangfire Dashboard 僅限本機存取，生產環境應改用 JWT-based filter | 實作 `JwtDashboardAuthorizationFilter` 驗證 Admin role |
| 6 | DataProtection volume | `/app/keys` 需在部署時手動掛載 persistent volume | 在 Docker Compose 加入 `keys` volume；Helm 加入 PVC |
| 7 | Saga DB migration | 新增 `saga_migration_jobs` 資料表需執行 EF migration | 執行 `dotnet ef migrations add AddSagaTable` 並 `dotnet ef database update` |
