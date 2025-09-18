namespace AdPlatformService.Models
{
    public class AdPlatform
    {
        public string Name { get; set; } = string.Empty;
        public HashSet<string> Locations { get; set; } = new();

        public AdPlatform() { }

        public AdPlatform(string name, IEnumerable<string> locations)
        {
            Name = name;
            Locations = new HashSet<string>(locations);
        }
    }

    public class UploadRequest
    {
        public IFormFile File { get; set; } = null!;
    }

    public class SearchResponse
    {
        public string Location { get; set; } = string.Empty;
        public List<string> AdPlatforms { get; set; } = new();
        public int Count => AdPlatforms.Count;
    }
    public class UploadResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ProcessedPlatforms { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}