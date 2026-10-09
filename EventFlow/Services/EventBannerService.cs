using Microsoft.AspNetCore.Http;

namespace EventFlow.Services
{
    public class EventBannerService
    {
        private readonly IWebHostEnvironment _environment;

        private static readonly string[] AllowedExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

        public EventBannerService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<string?> SaveBannerAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            if (file.Length > MaxFileSize)
            {
                throw new InvalidOperationException(
                    "Banner image must be 5 MB or smaller.");
            }

            var extension = Path.GetExtension(file.FileName)
                .ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException(
                    "Only JPG, JPEG, PNG and WEBP images are allowed.");
            }

            var uploadFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "events");

            Directory.CreateDirectory(uploadFolder);

            var fileName =
                $"{Guid.NewGuid():N}{extension}";

            var filePath = Path.Combine(
                uploadFolder,
                fileName);

            await using var stream =
                new FileStream(filePath, FileMode.Create);

            await file.CopyToAsync(stream);

            return $"/uploads/events/{fileName}";
        }

        public void DeleteBanner(string? bannerPath)
        {
            if (string.IsNullOrWhiteSpace(bannerPath))
            {
                return;
            }

            var fileName = Path.GetFileName(bannerPath);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            var filePath = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "events",
                fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}