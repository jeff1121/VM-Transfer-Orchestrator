# ADR-004: 使用 ASP.NET DataProtection 加密連線密碼

- **狀態**：已接受
- **日期**：2025-01
- **決策者**：VMTO 團隊

## 背景

VMTO 需要儲存 vSphere 與 Proxmox VE 的連線憑證（密碼、API Token 等）。這些敏感資訊必須加密存儲，不能以明文寫入資料庫。

### 需求

- 連線密碼在資料庫中加密存儲
- API 回應不回傳密碼原文
- 加密 / 解密對業務邏輯透明
- 未來可替換為外部金鑰管理服務（HashiCorp Vault、AWS KMS 等）

### 考量的方案

1. **ASP.NET DataProtection**：.NET 內建加密框架，支援金鑰輪替
2. **HashiCorp Vault**：企業級密鑰管理，功能完整但部署複雜
3. **自建 AES 加密**：簡單但需自行處理金鑰管理與輪替
4. **資料庫層加密 (TDE)**：透明但無法控制欄位級加密

## 決策

**使用 ASP.NET DataProtection** 作為預設加密方案，並透過 `IEncryptionService` 介面抽象加密邏輯。

- `DataProtectionEncryptionService` 實作 `IEncryptionService`
- 密碼以 `EncryptedSecret` 值物件封裝（包含 `CipherText` 與 `KeyId`）
- Connection 聚合根的 `UpdateSecret()` 方法接收加密後的值
- `ConnectionDto` 不包含密碼欄位

### 介面設計

```csharp
public interface IEncryptionService
{
    EncryptedSecret Encrypt(string plainText);
    string Decrypt(EncryptedSecret secret);
}
```

## 後果

### 正面

- **零額外依賴**：DataProtection 是 ASP.NET Core 內建，不需額外服務
- **金鑰輪替**：DataProtection 自動處理金鑰輪替與舊密文解密
- **介面抽象**：`IEncryptionService` 可輕鬆替換為 Vault / KMS 實作
- **值物件封裝**：`EncryptedSecret` 在型別層面區分明文與密文
- **審計追蹤**：搭配 `AuditLogService` 記錄敏感操作

### 負面

- **金鑰儲存**：DataProtection 預設將金鑰存於本地檔案系統，多節點部署需共享金鑰儲存（如 Redis 或資料庫）
- **非集中式**：不如 Vault 提供集中式密鑰管理、存取控制與審計
- **遷移成本**：日後切換至 Vault 時，需重新加密現有密文
