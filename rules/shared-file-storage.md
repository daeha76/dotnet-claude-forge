# Shared: 파일 스토리지 (Azure Blob Storage)

## 결정: Azure Blob Storage 단독 사용

Supabase Storage 병행 사용하지 않음.
이유: Azure App Service + Azure Blob = 동일 리전, 인증 통합, Key Vault 연동.

---

## 파일 업로드 인터페이스

```csharp
// Application/Interfaces/IFileStorageService.cs
public interface IFileStorageService
{
    /// <summary>파일 업로드 후 파일 메타데이터 반환</summary>
    Task<FileUploadResult> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        FileCategory category,        // 컨테이너/경로 결정에 사용
        Guid? ownerId = null,         // 현장ID, 기술인ID 등
        CancellationToken ct = default);

    /// <summary>임시 접근 URL 생성 (SAS Token, 기본 1시간)</summary>
    Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null);

    /// <summary>파일 삭제 (Soft Delete — 실제 파일은 30일 후 자동 제거)</summary>
    Task DeleteAsync(string blobPath, CancellationToken ct = default);
}

public record FileUploadResult(
    string BlobPath,        // Azure Blob 경로 (DB에 저장)
    string FileName,        // 원본 파일명
    long FileSizeBytes,
    string ContentType,
    string ThumbnailPath);  // 이미지인 경우 썸네일 경로

public enum FileCategory
{
    BidDocument,        // 입찰 서류 — bids/{bidId}/
    SitePhoto,          // 현장 사진 — sites/{siteId}/photos/
    SiteDailyLog,       // 현장 일지 첨부 — sites/{siteId}/logs/
    PersonnelCert,      // 기술인 자격증 — personnel/{personnelId}/certs/
    PersonnelCareer,    // 기술인 경력증명 — personnel/{personnelId}/career/
    Contract,           // 계약서 — contracts/{contractId}/
    Report,             // 보고서 — reports/{siteId}/
    CompanyDoc          // 회사 공통 서류 — company/
}
```

---

## Azure Blob 컨테이너 구조

```
bms-files/                          ← 메인 컨테이너 (비공개)
├── bids/{bidId}/                   ← 입찰 서류
├── sites/{siteId}/
│   ├── photos/{date}/              ← 현장 사진
│   ├── logs/{year}/{month}/        ← 현장 일지 첨부
│   └── reports/{year}/            ← 보고서
├── personnel/{personnelId}/
│   ├── certs/                      ← 자격증 스캔본
│   └── career/                     ← 경력증명서
├── contracts/{contractId}/         ← 계약서
└── company/                        ← 회사 공통 서류 (면허증 등)

bms-thumbnails/                     ← 썸네일 컨테이너 (CDN 연결)
└── {원본 경로와 동일 구조}
```

---

## 구현 패턴

```csharp
// Infrastructure/Storage/AzureBlobStorageService.cs
// Nuget: Azure.Storage.Blobs, Azure.Identity
public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobServiceClient _blobClient;
    private const string MainContainer = "bms-files";
    private const string ThumbnailContainer = "bms-thumbnails";

    public async Task<FileUploadResult> UploadAsync(
        Stream fileStream, string fileName, string contentType,
        FileCategory category, Guid? ownerId = null, CancellationToken ct = default)
    {
        var blobPath = BuildBlobPath(category, ownerId, fileName);
        var container = _blobClient.GetBlobContainerClient(MainContainer);
        var blob = container.GetBlobClient(blobPath);

        await blob.UploadAsync(fileStream, new BlobHttpHeaders { ContentType = contentType }, ct);

        // 이미지인 경우 썸네일 생성
        var thumbnailPath = string.Empty;
        if (IsImage(contentType))
            thumbnailPath = await GenerateThumbnailAsync(fileStream, blobPath, ct);

        return new FileUploadResult(blobPath, fileName,
            fileStream.Length, contentType, thumbnailPath);
    }

    public async Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null)
    {
        var container = _blobClient.GetBlobContainerClient(MainContainer);
        var blob = container.GetBlobClient(blobPath);
        // SAS Token 1시간 유효 (기본)
        var sasUri = blob.GenerateSasUri(BlobSasPermissions.Read,
            DateTimeOffset.UtcNow.Add(expiry ?? TimeSpan.FromHours(1)));
        return sasUri.ToString();
    }
}
```

---

## Blazor 파일 업로드 컴포넌트 규칙

```razor
@* CORRECT: 청크 업로드로 대용량 파일 처리 *@
<InputFile OnChange="HandleFileSelected" multiple accept=".pdf,.jpg,.png,.docx" />

@code {
    // 최대 파일 크기: 50MB
    private const long MaxFileSize = 50 * 1024 * 1024;

    private async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        foreach (var file in e.GetMultipleFiles(maxAllowedFiles: 10))
        {
            if (file.Size > MaxFileSize)
            {
                // 에러 처리
                continue;
            }
            // API로 전송 (Blazor 컴포넌트는 직접 Azure Blob 접근 금지)
            using var stream = file.OpenReadStream(MaxFileSize);
            await FileApiClient.UploadAsync(stream, file.Name, file.ContentType, category);
        }
    }
}
```

**Blazor에서 직접 Azure Blob 접근 금지** — 반드시 .NET API를 통해 업로드.

---

## API 엔드포인트

```csharp
// Api/Controllers/FilesController.cs
[ApiController]
[Route("api/v1/files")]
[Authorize]
public class FilesController : ControllerBase
{
    /// <summary>파일 업로드</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(52_428_800)] // 50MB
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromForm] FileCategory category,
        [FromForm] Guid? ownerId)

    /// <summary>다운로드 URL 발급 (SAS Token 1시간)</summary>
    [HttpGet("{*blobPath}/url")]
    public async Task<IActionResult> GetDownloadUrl(string blobPath)
}
```

---

## DB 메타데이터 저장 (EF Core)

```csharp
// Domain/Entities/FileAttachment.cs
public class FileAttachment
{
    public Guid Id { get; private set; }
    public string BlobPath { get; private set; }    // Azure Blob 경로
    public string FileName { get; private set; }    // 원본 파일명
    public long FileSizeBytes { get; private set; }
    public string ContentType { get; private set; }
    public string? ThumbnailPath { get; private set; }
    public FileCategory Category { get; private set; }
    public Guid? OwnerId { get; private set; }       // 현장ID, 기술인ID 등
    public Guid UploadedBy { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public bool IsDeleted { get; private set; }      // Soft Delete
}
```

---

## Secret 설정

```bash
# 개발 환경
dotnet user-secrets set "Azure:BlobStorageConnectionString" "DefaultEndpointsProtocol=https;..."
```

```
# Azure Key Vault (운영) — Managed Identity 권장
Azure--BlobStorageConnectionString
```

> DefaultAzureCredential 사용 시 운영 환경에서 Connection String 불필요 (Managed Identity 자동 인증).
