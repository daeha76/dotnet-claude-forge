namespace Daeha.CleanArch.Application.Common.Interfaces;

/// <summary>
/// 파일 업로드 결과
/// </summary>
/// <param name="BlobPath">Azure Blob 경로 (DB에 저장)</param>
/// <param name="FileName">원본 파일명</param>
/// <param name="FileSizeBytes">파일 크기 (bytes)</param>
/// <param name="ContentType">MIME 타입</param>
/// <param name="ThumbnailPath">이미지인 경우 썸네일 경로. 비이미지는 null.</param>
public record FileUploadResult(
    string BlobPath,
    string FileName,
    long FileSizeBytes,
    string ContentType,
    string? ThumbnailPath);

/// <summary>
/// 파일 스토리지 추상화. 구현체: Azure Blob Storage.
/// category 파라미터로 Blob 경로를 결정하므로, 각 프로젝트에서 규칙을 정의.
/// </summary>
public interface IFileStorageService
{
    /// <summary>파일 업로드 후 메타데이터 반환</summary>
    /// <param name="fileStream">파일 스트림</param>
    /// <param name="fileName">원본 파일명</param>
    /// <param name="contentType">MIME 타입</param>
    /// <param name="category">Blob 경로 결정에 사용되는 카테고리 문자열</param>
    /// <param name="ownerId">파일 소유자 ID (현장ID, 기술인ID 등). 경로 구성에 사용.</param>
    /// <param name="ct">취소 토큰</param>
    Task<FileUploadResult> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string category,
        Guid? ownerId = null,
        CancellationToken ct = default);

    /// <summary>SAS Token 기반 임시 다운로드 URL 발급 (기본 1시간)</summary>
    Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null);

    /// <summary>파일 Soft Delete (30일 후 물리 삭제)</summary>
    Task DeleteAsync(string blobPath, CancellationToken ct = default);
}
