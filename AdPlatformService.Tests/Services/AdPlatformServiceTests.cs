using AdPlatformService.Services;
using FluentAssertions;
using System.Text;

namespace AdPlatformService.Tests.Services
{
    public class AdPlatformServiceTests
    {
        private readonly IAdPlatformService _service;

        public AdPlatformServiceTests()
        {
            _service = new AdPlatformService.Services.AdPlatformService();
        }

        [Fact]
        public async Task LoadAdPlatformsAsync_WithValidData_ShouldLoadSuccessfully()
        {
            var testData = @"Яндекс.Директ:/ru
            Ревдинский рабочий:/ru/svrd/revda,/ru/svrd/pervik
            Газета уральских москвичей:/ru/msk,/ru/permobl,/ru/chelobl
            Крутая реклама:/ru/svrd";

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(testData));
            var result = await _service.LoadAdPlatformsAsync(stream);

            result.Success.Should().BeTrue();
            result.ProcessedPlatforms.Should().Be(4);
            result.Message.Should().Contain("4 ad platforms");
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public async Task LoadAdPlatformsAsync_WithInvalidLines_ShouldReportErrors()
        {
            var testData = @"Яндекс.Директ:/ru
            InvalidLine
            Газета уральских москвичей:/ru/msk
            Platform without locations:";

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(testData));
            var result = await _service.LoadAdPlatformsAsync(stream);

            result.Success.Should().BeTrue(); 
            result.ProcessedPlatforms.Should().Be(2);
            result.Errors.Should().HaveCount(2); 
        }

        [Theory]
        [InlineData("/ru/msk", new[] { "Яндекс.Директ", "Газета уральских москвичей" })]
        [InlineData("/ru/svrd", new[] { "Яндекс.Директ", "Крутая реклама" })]
        [InlineData("/ru/svrd/revda", new[] { "Яндекс.Директ", "Крутая реклама", "Ревдинский рабочий" })]
        [InlineData("/ru", new[] { "Яндекс.Директ" })]
        [InlineData("/ru/svrd/ekb", new[] { "Яндекс.Директ", "Крутая реклама" })]
        public async Task SearchAdPlatforms_WithVariousLocations_ShouldReturnCorrectPlatforms(
            string location, string[] expectedPlatforms)
        {
            await LoadTestData();

            var result = _service.SearchAdPlatforms(location);

            result.Location.Should().Be(location);
            result.AdPlatforms.Should().BeEquivalentTo(expectedPlatforms);
            result.Count.Should().Be(expectedPlatforms.Length);
        }

        [Fact]
        public async Task SearchAdPlatforms_WithEmptyLocation_ShouldReturnEmptyResult()
        {
            await LoadTestData();

            var result = _service.SearchAdPlatforms("");
            result.AdPlatforms.Should().BeEmpty();
            result.Count.Should().Be(0);
        }

        [Fact]
        public async Task SearchAdPlatforms_WithUnknownLocation_ShouldReturnEmptyResult()
        {
            await LoadTestData();

            var result = _service.SearchAdPlatforms("/unknown/location");
            result.AdPlatforms.Should().BeEmpty();
            result.Count.Should().Be(0);
        }

        [Fact]
        public async Task SearchAdPlatforms_WithNormalizedLocation_ShouldWork()
        {
            await LoadTestData();

            var result1 = _service.SearchAdPlatforms("/ru/msk/");
            var result2 = _service.SearchAdPlatforms("//ru//msk");
            var result3 = _service.SearchAdPlatforms("/ru/msk");

            result1.AdPlatforms.Should().BeEquivalentTo(result3.AdPlatforms);
            result2.AdPlatforms.Should().BeEquivalentTo(result3.AdPlatforms);
        }

        [Fact]
        public async Task GetStatistics_AfterLoading_ShouldReturnCorrectCounts()
        {
            await LoadTestData();

            var (platformsCount, locationsCount) = _service.GetStatistics();

            platformsCount.Should().Be(4);
            locationsCount.Should().Be(7); 
        }

        [Fact]
        public async Task SearchAdPlatforms_MultipleCallsSameLocation_ShouldUseCaching()
        {
            await LoadTestData();
            var location = "/ru/msk";

            var result1 = _service.SearchAdPlatforms(location);
            var result2 = _service.SearchAdPlatforms(location);

            result1.AdPlatforms.Should().BeEquivalentTo(result2.AdPlatforms);
            result1.Count.Should().Be(result2.Count);
        }

        [Fact]
        public async Task LoadAdPlatformsAsync_ReplacesExistingData_ShouldWork()
        {
            await LoadTestData();
            var (_, _) = _service.GetStatistics();

            var newTestData = "New Platform:/new/location";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(newTestData));

            var result = await _service.LoadAdPlatformsAsync(stream);

            result.Success.Should().BeTrue();
            result.ProcessedPlatforms.Should().Be(1);

            var (newCount, _) = _service.GetStatistics();
            newCount.Should().Be(1);
        }

        private async Task LoadTestData()
        {
            var testData = @"Яндекс.Директ:/ru
            Ревдинский рабочий:/ru/svrd/revda,/ru/svrd/pervik
            Газета уральских москвичей:/ru/msk,/ru/permobl,/ru/chelobl
            Крутая реклама:/ru/svrd";

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(testData));
            await _service.LoadAdPlatformsAsync(stream);
        }
    }
}