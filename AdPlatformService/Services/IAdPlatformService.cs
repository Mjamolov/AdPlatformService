using AdPlatformService.Models;

namespace AdPlatformService.Services
{
    public interface IAdPlatformService
    {
        /// <summary>
        /// Выгрузка данных
        /// </summary>
        /// <param name="stream">Поток данных</param>
        /// <returns></returns>
        Task<UploadResponse> LoadAdPlatformsAsync(Stream stream);

        /// <summary>
        /// Поиск
        /// </summary>
        /// <param name="location">Локация для поиска</param>
        /// <returns></returns>
        SearchResponse SearchAdPlatforms(string location);

        /// <summary>
        /// Статистика
        /// </summary>
        /// <returns></returns>
        (int PlatformsCount, int LocationsCount) GetStatistics();
    }
}