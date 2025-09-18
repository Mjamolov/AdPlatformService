using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using FluentAssertions;
using System.Text.Json;
using AdPlatformService.Models;

namespace AdPlatformService.Tests.Integration
{
    public class AdPlatformControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public AdPlatformControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task HealthCheck_ShouldReturnHealthy()
        {
            // Act
            var response = await _client.GetAsync("/api/adplatform/health");

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue();
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Healthy");
        }

        [Fact]
        public async Task UploadAndSearch_FullWorkflow_ShouldWork()
        {
            // Arrange
            var testData = @"Яндекс.Директ:/ru
                Ревдинский рабочий:/ru/svrd/revda,/ru/svrd/pervik
                Газета уральских москвичей:/ru/msk,/ru/permobl,/ru/chelobl
                Крутая реклама:/ru/svrd";

            // Step 1: Upload data
            var formData = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(testData));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            formData.Add(fileContent, "file", "test.txt");

            var uploadResponse = await _client.PostAsync("/api/adplatform/upload", formData);

            // Assert upload
            uploadResponse.IsSuccessStatusCode.Should().BeTrue();
            var uploadContent = await uploadResponse.Content.ReadAsStringAsync();
            var uploadResult = JsonSerializer.Deserialize<UploadResponse>(uploadContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            uploadResult.Should().NotBeNull();
            uploadResult!.Success.Should().BeTrue();
            uploadResult.ProcessedPlatforms.Should().Be(4);

            // Step 2: Test search
            var searchResponse = await _client.GetAsync("/api/adplatform/search?location=/ru/msk");

            // Assert search
            searchResponse.IsSuccessStatusCode.Should().BeTrue();
            var searchContent = await searchResponse.Content.ReadAsStringAsync();
            var searchResult = JsonSerializer.Deserialize<SearchResponse>(searchContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            searchResult.Should().NotBeNull();
            searchResult!.Location.Should().Be("/ru/msk");
            searchResult.AdPlatforms.Should().Contain("Яндекс.Директ");
            searchResult.AdPlatforms.Should().Contain("Газета уральских москвичей");
            searchResult.Count.Should().Be(2);
        }

        [Fact]
        public async Task Upload_EmptyFile_ShouldReturnBadRequest()
        {
            // Arrange
            var formData = new MultipartFormDataContent();
            var emptyFileContent = new ByteArrayContent([]);
            emptyFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            formData.Add(emptyFileContent, "file", "empty.txt");

            // Act
            var response = await _client.PostAsync("/api/adplatform/upload", formData);

            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Upload_NoFile_ShouldReturnBadRequest()
        {
            // Act
            var response = await _client.PostAsync("/api/adplatform/upload", new MultipartFormDataContent());

            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Search_EmptyLocation_ShouldReturnBadRequest()
        {
            // Act
            var response = await _client.GetAsync("/api/adplatform/search?location=");

            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Search_MissingLocationParameter_ShouldReturnBadRequest()
        {
            var response = await _client.GetAsync("/api/adplatform/search");

            response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetStatistics_ShouldReturnStatistics()
        {
            var response = await _client.GetAsync("/api/adplatform/statistics");

            response.IsSuccessStatusCode.Should().BeTrue();
            var content = await response.Content.ReadAsStringAsync();

            content.Should().Contain("platformsCount");   
            content.Should().Contain("locationsCount");   
            content.Should().Contain("timestamp");      
        }

        [Theory]
        [InlineData("/ru/svrd", "Яндекс.Директ")]
        [InlineData("/ru/svrd", "Крутая реклама")]
        [InlineData("/ru/svrd/revda", "Ревдинский рабочий")]
        public async Task Search_AfterUpload_ShouldFindExpectedPlatforms(string location, string expectedPlatform)
        {
            await UploadTestData();

            var response = await _client.GetAsync($"/api/adplatform/search?location={location}");

            response.IsSuccessStatusCode.Should().BeTrue();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SearchResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            result.Should().NotBeNull();
            result!.AdPlatforms.Should().Contain(expectedPlatform);
        }

        private async Task UploadTestData()
        {
            var testData = @"Яндекс.Директ:/ru
                Ревдинский рабочий:/ru/svrd/revda,/ru/svrd/pervik
                Газета уральских москвичей:/ru/msk,/ru/permobl,/ru/chelobl
                Крутая реклама:/ru/svrd";

            var formData = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(testData));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            formData.Add(fileContent, "file", "test.txt");

            await _client.PostAsync("/api/adplatform/upload", formData);
        }
    }
}