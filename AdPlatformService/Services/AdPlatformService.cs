using AdPlatformService.Models;
using System.Collections.Concurrent;
using System.Text;

namespace AdPlatformService.Services
{
    public class AdPlatformService : IAdPlatformService
    {
        private readonly ReaderWriterLockSlim _lock = new();
        private Dictionary<string, AdPlatform> _adPlatforms = [];
        private readonly Dictionary<string, HashSet<string>> _locationIndex = [];
        private readonly ConcurrentDictionary<string, List<string>> _searchCache = new();

        public async Task<UploadResponse> LoadAdPlatformsAsync(Stream stream)
        {
            var response = new UploadResponse();
            var errors = new List<string>();
            var platforms = new Dictionary<string, AdPlatform>();

            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string? line;
                int lineNumber = 0;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var platform = ParseLine(line);
                        if (platform != null)
                        {
                            platforms[platform.Name] = platform;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Line {lineNumber}: {ex.Message}");
                    }
                }
                _lock.EnterWriteLock();
                try
                {
                    _adPlatforms = platforms;
                    BuildLocationIndex();
                    _searchCache.Clear(); 

                    response.Success = true;
                    response.ProcessedPlatforms = platforms.Count;
                    response.Message = $"Successfully loaded {platforms.Count} ad platforms";
                    response.Errors = errors;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Failed to load ad platforms: {ex.Message}";
                response.Errors = errors;
            }

            return response;
        }

        public SearchResponse SearchAdPlatforms(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return new SearchResponse
                {
                    Location = location ?? string.Empty,
                    AdPlatforms = []
                };
            }

            location = NormalizeLocation(location);

            if (_searchCache.TryGetValue(location, out var cachedResult))
            {
                return new SearchResponse
                {
                    Location = location,
                    AdPlatforms = new List<string>(cachedResult)
                };
            }

            _lock.EnterReadLock();
            try
            {
                var matchingPlatforms = new HashSet<string>();

                if (_locationIndex.TryGetValue(location, out var exactMatches))
                {
                    matchingPlatforms.UnionWith(exactMatches);
                }

                foreach (var kvp in _locationIndex)
                {
                    var platformLocation = kvp.Key;
                    var platforms = kvp.Value;

                    if (IsLocationPrefix(platformLocation, location))
                    {
                        matchingPlatforms.UnionWith(platforms);
                    }
                }

                var result = matchingPlatforms.OrderBy(x => x).ToList();

                _searchCache.TryAdd(location, new List<string>(result));

                return new SearchResponse
                {
                    Location = location,
                    AdPlatforms = result
                };
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public (int PlatformsCount, int LocationsCount) GetStatistics()
        {
            _lock.EnterReadLock();
            try
            {
                var totalLocations = _adPlatforms.Values
                    .SelectMany(p => p.Locations)
                    .Distinct()
                    .Count();

                return (_adPlatforms.Count, totalLocations);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private static AdPlatform? ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            var colonIndex = line.IndexOf(':');
            if (colonIndex == -1)
                throw new FormatException($"Invalid format: missing ':' separator");

            var name = line[..colonIndex].Trim();
            if (string.IsNullOrEmpty(name))
                throw new FormatException($"Platform name is empty");

            var locationsString = line[(colonIndex + 1)..].Trim();
            if (string.IsNullOrEmpty(locationsString))
                throw new FormatException($"Locations string is empty for platform '{name}'");

            var locations = locationsString
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(loc => NormalizeLocation(loc.Trim()))
                .Where(loc => !string.IsNullOrEmpty(loc))
                .ToList();

            if (locations.Count == 0)
                throw new FormatException($"No valid locations found for platform '{name}'");

            return new AdPlatform(name, locations);
        }

        private void BuildLocationIndex()
        {
            _locationIndex.Clear();

            foreach (var platform in _adPlatforms.Values)
            {
                foreach (var location in platform.Locations)
                {
                    if (!_locationIndex.TryGetValue(location, out var platforms))
                    {
                        platforms = [];
                        _locationIndex[location] = platforms;
                    }
                    platforms.Add(platform.Name);
                }
            }
        }

        private static string NormalizeLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
                return string.Empty;

            location = location.Trim();

            while (location.Contains("//"))
            {
                location = location.Replace("//", "/");
            }

            if (location.Length > 1 && location.EndsWith('/'))
            {
                location = location[..^1];
            }

            return location;
        }
        private static bool IsLocationPrefix(string platformLocation, string targetLocation)
        {
            if (platformLocation == targetLocation)
                return false;

            if (!targetLocation.StartsWith(platformLocation))
                return false;

            if (targetLocation.Length > platformLocation.Length)
            {
                var nextChar = targetLocation[platformLocation.Length];
                return nextChar == '/';
            }

            return false;
        }
    }
}