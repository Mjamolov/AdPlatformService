using Microsoft.AspNetCore.Mvc;
using AdPlatformService.Models;
using AdPlatformService.Services;

namespace AdPlatformService.Controllers
{
    /// <summary>
    /// Контроллер для работы с рекламными площадками
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AdPlatformController : ControllerBase
    {
        private readonly IAdPlatformService _adPlatformService;
        private readonly ILogger<AdPlatformController> _logger;

        public AdPlatformController(IAdPlatformService adPlatformService, ILogger<AdPlatformController> logger)
        {
            _adPlatformService = adPlatformService;
            _logger = logger;
        }

        /// <summary>
        /// Загрузка файлов с даннми из площадок
        /// </summary>
        /// <param name="file">Текстовый файл с рекламными площадками</param>
        /// <returns>Результат загрузки</returns>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadAdPlatforms(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("Upload attempt with empty or null file");
                    return BadRequest(new ErrorResponse
                    {
                        Error = "File is required",
                        Details = "Please provide a valid text file with ad platforms data"
                    });
                }

                const int maxFileSize = 10 * 1024 * 1024;
                if (file.Length > maxFileSize)
                {
                    _logger.LogWarning($"File too large: {file.Length} bytes");
                    return BadRequest(new ErrorResponse
                    {
                        Error = "File too large",
                        Details = $"Maximum file size is {maxFileSize / (1024 * 1024)}MB"
                    });
                }

                var allowedExtensions = new[] { ".txt", ".csv" };
                var fileExtension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(new ErrorResponse
                    {
                        Error = "Invalid file type",
                        Details = "Only .txt and .csv files are supported"
                    });
                }

                _logger.LogInformation($"Starting upload of file: {file.FileName}, size: {file.Length} bytes");

                using var stream = file.OpenReadStream();
                var result = await _adPlatformService.LoadAdPlatformsAsync(stream);

                if (result.Success)
                {
                    _logger.LogInformation($"Successfully uploaded {result.ProcessedPlatforms} ad platforms");
                }
                else
                {
                    _logger.LogWarning($"Upload completed with errors: {result.Message}");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file upload");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ErrorResponse
                    {
                        Error = "Internal server error",
                        Details = "An error occurred while processing the file"
                    });
            }
        }

        /// <summary>
        /// В зависимости от загруженного файла ищет данные в соответсвии с параметром
        /// </summary>
        /// <param name="location">Локация для поиска (например: /ru/msk)</param>
        /// <returns>Список подходящих плащадок</returns>
        [HttpGet("search")]
        [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public IActionResult SearchAdPlatforms([FromQuery] string location)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(location))
                {
                    return BadRequest(new ErrorResponse
                    {
                        Error = "Location is required",
                        Details = "Please provide a location parameter (e.g., /ru/msk)"
                    });
                }

                _logger.LogDebug($"Searching ad platforms for location: {location}");

                var result = _adPlatformService.SearchAdPlatforms(location);

                _logger.LogDebug($"Found {result.Count} ad platforms for location: {location}");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during search for location: {location}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ErrorResponse
                    {
                        Error = "Internal server error",
                        Details = "An error occurred while searching ad platforms"
                    });
            }
        }

        /// <summary>
        /// Статистика
        /// </summary>
        /// <returns>Информация о локақиях</returns>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public IActionResult GetStatistics()
        {
            try
            {
                var (platformsCount, locationsCount) = _adPlatformService.GetStatistics();

                return Ok(new
                {
                    PlatformsCount = platformsCount,
                    LocationsCount = locationsCount,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ErrorResponse
                    {
                        Error = "Internal server error",
                        Details = "An error occurred while getting statistics"
                    });
            }
        }

        /// <summary>
        /// Чисто проверка, привычка так делать, чтобы не париться)
        /// </summary>
        /// <returns>Статус сервиса</returns>
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
        }
    }
}