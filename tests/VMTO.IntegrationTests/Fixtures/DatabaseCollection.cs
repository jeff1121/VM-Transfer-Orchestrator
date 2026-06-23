namespace VMTO.IntegrationTests.Fixtures;

/// <summary>
/// 資料庫整合測試集合定義，共用 PostgreSQL 容器。
/// </summary>
[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<PostgreSqlFixture>;
