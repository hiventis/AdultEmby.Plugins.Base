using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom.Html;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace AdultEmby.Plugins.Base
{
    public abstract class AdultEmbyMovieProviderBase : AdultEmbyProviderBase
    {
    
        protected AdultEmbyMovieProviderBase(IHttpClient httpClient, IServerConfigurationManager configurationManager, IFileSystem fileSystem, ILogManager logManager, IJsonSerializer jsonSerializer)
            : base(httpClient, configurationManager, fileSystem, logManager, jsonSerializer)
        {

        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            Logger.Info("Retrieving movie image [{0}]", url);
            return GetResponse(url, false, cancellationToken);
        }

        private async Task<List<SearchResult>> GetSearchResults(string name, int? year,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(name))
            {
                Logger.Info("Skipping search because name is not supplied");
                return new List<SearchResult>();
            }
            var url = string.Format(SearchUrl, EncodeSearchTerm ? WebUtility.UrlEncode(name) : name);

            Logger.Info("Searching for metadata by name: [{0}] year: [{1}] with url: [{2}]", name, year, url);

            string html;

            using (var httpResponseInfo = await GetResponse(url, true, cancellationToken)/*.ConfigureAwait(false)*/)
            {
                html = httpResponseInfo.Content.ToStringFromStream();
            }

            //SecurityPassthrough siteParsingProfileSecurityPassthrough = (SecurityPassthrough)siteToParseFrom;
            //if (RequiresSecurityPassthrough(html))
            //{
            //    html = RunSecurityPassthrough(html, options);
            //}

            if (string.IsNullOrEmpty(html))
            {
                Logger.Info("Received empty response for [{0}]", url);
                return null;
            }

            Logger.Info("Attempting to search parse response from [{0}]", url);
            IHtmlDocument htmlDocument = HtmlParser.Parse(html);

            List<SearchResult> searchResults = SearchResultsHtmlExtractor.GetSearchResults(htmlDocument);

            Logger.Info("Extracted {0} items from [{1}]", searchResults.Count, url);
            CalculateSearchResultRelevancy(searchResults, name, year);
            return searchResults.OrderByDescending(o => o.Relevance).ToList();
        }

        internal async Task<string> EnsureMovieInfo(string movieId, CancellationToken cancellationToken)
        {
            var cachePath = GetItemCachePath(FileSystem, ConfigurationManager.ApplicationPaths, movieId);
            string jsonFile = Path.Combine(cachePath, "item.json");

            var fileInfo = FileSystem.GetFileInfo(jsonFile);

            Logger.Info("Cache file for [{0}] is: [{1}]", movieId, jsonFile);
            // Check cache first
            if (!fileInfo.Exists || (DateTime.UtcNow - FileSystem.GetLastWriteTimeUtc(fileInfo)).TotalDays > 7)
            {
                Logger.Info("Refreshing cache file for [{0}]", movieId);
                string url = GetExternalUrl(movieId);

                Logger.Info("Movie metadata source url: [{0}]", url);

                // Download and cache
                string htmlFile = Path.Combine(cachePath, "item.html");
                using (var httpResponseInfo = await GetResponse(url, false, cancellationToken))
                {
                    DirectoryInfo directoryInfo = Directory.CreateDirectory(/*Path.GetDirectoryName(*/cachePath/*)*/);
                    Logger.Info("Retrieved movie metadata from source [{0}]", url);
                    
                    using (
                        var fileStream = FileSystem.GetFileStream(htmlFile, FileOpenMode.Create, FileAccessMode.Write,
                            FileShareMode.Read, true))
                    {
                        Logger.Info("Saving movie metadata to cache file [{0}] ", htmlFile);
                        await httpResponseInfo.Content.CopyToAsync(fileStream).ConfigureAwait(false);
                    }
                }
                IHtmlMetadataExtractor extractor = GetMovieHtmlExtractor(movieId);
                MovieResult movieResult = ConvertHtmlToMovieResult(movieId, htmlFile, extractor);
                JsonSerializer.SerializeToFile(movieResult, jsonFile);
            }
            return jsonFile;
        }

        private async Task<MetadataResult<Movie>> GetItemDetails(string movieId, CancellationToken cancellationToken)
        {
            //string html;
            var result = new MetadataResult<Movie>();
            result.Item = new Movie();
            Movie item = result.Item;
            item.SetProviderId(GetMovieExternalIdName(), movieId);

            Logger.Info("Getting details for: [{0}]", movieId);

            string cachePath = await EnsureMovieInfo(movieId, cancellationToken).ConfigureAwait(false);

            //PersonResult personResult = ConvertHtmlToPersonResult(personId, cachePath);
            MovieResult movieResult = JsonSerializer.DeserializeFromFile<MovieResult>(cachePath);

            /*IHtmlDocument htmlDocument;
            using (var stream = await GetInfo(id, cancellationToken).ConfigureAwait(false))
            {
                htmlDocument = HtmlParser.Parse(stream);
            }

            //IHtmlDocument htmlDocument = HtmlParser.Parse(html);

            Logger.Info("Received response, attempting to extract metadata");
            IHtmlMetadataExtractor extractor = GetMovieHtmlExtractor(id);
            */
            if (movieResult != null)
            {
                GetCast(movieResult, result);

                GetSynopsisAndTagLine(movieResult, item);

                GetCategories(movieResult, result);

                GetDetails(movieResult, result);

                GetPrimaryImage(movieResult, result);

                if (string.IsNullOrEmpty(item.OfficialRating))
                {
                    item.OfficialRating = "XXX";
                }
                result.HasMetadata = false;
                Logger.Info("Extracted metadata");
            }
            else
            {
                result.HasMetadata = false;
            }
            return result;
        }

        private void GetPrimaryImage(MovieResult movieResult, MetadataResult<Movie> result)
        {
            string url = movieResult.PrimaryImageUrl;
            if (url != null)
            {
                var img = new ItemImageInfo();
                img.Type = ImageType.Primary;
                img.Path = url;
                result.Item.SetImage(img, 0);
            }
        }

        private void GetDetails(MovieResult movieResult, MetadataResult<Movie> result)
        {
            result.Item.PremiereDate = movieResult.ReleaseDate;

            int? year = movieResult.ProductionYear;
            if (year.HasValue)
            {
                result.Item.ProductionYear = year.Value;
            }
            var studio = movieResult.Studio;
            if (!string.IsNullOrEmpty(studio))
            {
                result.Item.AddStudio(studio);
            }

            var upcCode = movieResult.UpcCode;
            if (!string.IsNullOrEmpty(upcCode))
            {
                result.Item.SetProviderId(UpcCodeId.KeyName, upcCode);
            }
        }

        private void GetCategories(MovieResult movieResult, MetadataResult<Movie> metadataResult)
        {
            metadataResult.Item.Genres.Clear();

            IEnumerable<string> genres = movieResult.Genres;
            if (genres != null)
            {
                foreach (var genre in genres)
                {
                    metadataResult.Item.AddGenre(genre);
                }
            }
        }

        private void GetSynopsisAndTagLine(MovieResult movieResult, Movie item)
        {
            string name = movieResult.Title;
            if (name != null)
            {
                item.Name = name;
            }

            string synopsis = movieResult.Synopsis;
            if (synopsis != null)
            {
                item.Overview = synopsis;
            }
        }

        private void GetCast(MovieResult movieResult, MetadataResult<Movie> result)
        {
            result.ResetPeople();

            IEnumerable<MoviePerson> actors = movieResult.Actors;
            foreach (MoviePerson actor in actors)
            {
                PersonInfo personInfo = new PersonInfo()
                {
                    Name = actor.Name,
                    Type = PersonType.Actor
                };
                if (!string.IsNullOrEmpty(actor.Id))
                {
                    personInfo.SetProviderId(GetPersonExternalIdName(), actor.Id);
                }
                result.AddPerson(personInfo);
            }

            MoviePerson director = movieResult.Director;
            if (director != null)
            {
                result.AddPerson(new PersonInfo()
                {
                    Name = director.Name,
                    Type = PersonType.Director
                });
            }
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo,
    CancellationToken cancellationToken)
        {
            var movieId = searchInfo.GetProviderId(GetMovieExternalIdName());

            if (!string.IsNullOrEmpty(movieId))
            {
                Logger.Info("Search term is an id so retrieving via metadata");
                var result = await GetMetadata(searchInfo, cancellationToken).ConfigureAwait(false);

                if (result.HasMetadata)
                {
                    return new List<RemoteSearchResult>()
                    {
                        new RemoteSearchResult
                        {
                            Name = result.Item.Name,
                            ProductionYear = result.Item.ProductionYear,
                            ProviderIds = result.Item.ProviderIds,
                            SearchProviderName = ProviderName,
                            PremiereDate = result.Item.PremiereDate,
                            ImageUrl = result.Item.PrimaryImagePath
                        }
                    };
                }
            }

            Logger.Info("Search term is not an id so performing search");
            var items = await GetSearchResults(searchInfo.Name, searchInfo.Year, cancellationToken);

            return items.Select(i =>
            {
                var result = new RemoteSearchResult
                {
                    Name = i.Name,
                    ProductionYear = i.Year,
                    PremiereDate = i.PremiereDate,
                    ImageUrl = i.ImageUrl,
                    SearchProviderName = ProviderName
                };

                result.SetProviderId(GetMovieExternalIdName(), i.Id);

                return result;
            });
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            MetadataResult<Movie> result = null;

            var movieId = info.GetProviderId(GetMovieExternalIdName());

            if (string.IsNullOrEmpty(movieId))
            {
                Logger.Info("Metadata request does not contain an id so will search by name: [{0}] year: [{1}]", info.Name, info.Year);
                var items = await GetSearchResults(info.Name, info.Year, cancellationToken);

                if (items != null && items.Any())
                {
                    var probableItem = items.FirstOrDefault(x => x.Relevance > 0.8f);

                    if (probableItem != null)
                    {
                        movieId = probableItem.Id;
                        Logger.Info("Mapped name: [{0}] year: [{1}] to {2}", info.Name, info.Year, movieId);
                    }
                }
            }

            if (!string.IsNullOrEmpty(movieId))
            {
                result = await GetItemDetails(movieId, cancellationToken);

                result.HasMetadata = true;
            }
            else
            {
                Logger.Info("Failed to map name: [{0}] year: [{1}]", info.Name, info.Year);
                result = new MetadataResult<Movie>();
            }

            return result;
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(IHasMetadata item, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();
            if (item is Movie)
            {
                var movieId = item.GetProviderId(GetMovieExternalIdName());
                if (!string.IsNullOrEmpty(movieId))
                {
                    Logger.Info("Retrieving movie image details for [{0}]", movieId);
                    //IHtmlMetadataExtractor metadataExtractor = GetMovieHtmlExtractor(movieId);

                    //string html;
                    //using (var stream = await GetInfo(movieId, cancellationToken).ConfigureAwait(false))
                    //{
                    //    html = stream.ToStringFromStream();
                    //}
                    //IHtmlDocument htmlDocument = HtmlParser.Parse(html);
                    string cachePath = await EnsureMovieInfo(movieId, cancellationToken).ConfigureAwait(false);

                    //PersonResult personResult = ConvertHtmlToPersonResult(personId, cachePath);
                    MovieResult movieResult = JsonSerializer.DeserializeFromFile<MovieResult>(cachePath);
                    string primaryImageUrl = movieResult.PrimaryImageUrl;

                    var supportedImages = GetSupportedImages(item).ToList();

                    if (supportedImages.Contains(ImageType.Primary))
                    {
                        list.Add(new RemoteImageInfo
                        {
                            Url = primaryImageUrl,
                            ProviderName = ProviderName,
                            Type = ImageType.Primary
                        });
                    }
                }
            }

            return list;
        }

        private void CalculateSearchResultRelevancy(List<SearchResult> searchResults, string name, int? year)
        {
            foreach (var searchResult in searchResults)
            {
                // calculate the relavance of this hit
                string sCompareTitle = searchResult.Name.ToLower();
                string sMatchTitle = name.ToLower();

                /*
                 * Identify the best match by performing a fuzzy string compare on the search term and
                 * the result. Additionally, use the year (if available) to further refine the best match.
                 * An exact match scores 1, a match off by a year scores 0.5 (release dates can vary between
                 * countries), otherwise it scores 0.
                 */
                int? sCompareYear = searchResult.Year;

                double yearScore = 0;
                if (year.HasValue && sCompareYear.HasValue)
                {
                    yearScore = Math.Max(0.0, 1 - 0.5 * Math.Abs(year.Value - sCompareYear.Value));
                }
                searchResult.Relevance = StringUtils.FuzzyStringCompare(sMatchTitle, sCompareTitle) + yearScore;
            }
        }

        private MovieResult ConvertHtmlToMovieResult(string personId, string cachePath, IHtmlMetadataExtractor extractor)
        {
            Logger.Info("Parsing html for [{0}] in [{1}]", personId, cachePath);
            IHtmlDocument htmlDocument;
            using (var stream = FileSystem.GetFileStream(cachePath, FileOpenMode.Open, FileAccessMode.Read, FileShareMode.Read))
            {
                htmlDocument = HtmlParser.Parse(stream);
            }

            Logger.Info("Received response, attempting to extract metadata");

            MovieResult result = new MovieResult()
            {
                //HasMetadata = extractor.HasMetadata(htmlDocument),
                Id = personId
            };

            //if (personResult.HasMetadata)
            //{
            result.Title = extractor.GetTitle(htmlDocument);
            result.Synopsis = extractor.GetSynopsis(htmlDocument);

            //GetCast(htmlDocument, movieResult, extractor);
            result.Director = extractor.GetDirector(htmlDocument);
            List<MoviePerson> actors = extractor.GetActors(htmlDocument);
            if (actors != null)
            {
                result.Actors = actors;
            }

            result.Genres = extractor.GetGenres(htmlDocument);
            result.ReleaseDate = extractor.GetReleaseDate(htmlDocument);
            result.ProductionYear = extractor.GetProductionYear(htmlDocument);
            result.UpcCode = extractor.GetUpcCode(htmlDocument);
            result.Studio = extractor.GetStudio(htmlDocument);
            result.PrimaryImageUrl = extractor.GetPrimaryImageUrl(htmlDocument);
            //}
            return result;
        }

        public bool Supports(IHasMetadata item)
        {
            return item is Movie;
        }

        public IEnumerable<ImageType> GetSupportedImages(IHasMetadata item)
        {
            return new List<ImageType> { ImageType.Primary, /*, ImageType.BoxRear*/};
        }

        protected abstract string GetMovieExternalIdName();

        protected abstract string GetPersonExternalIdName();

        protected abstract string ProviderName { get; }

        protected abstract string GetItemCachePath(IFileSystem fileSystem, IApplicationPaths appPaths, string movieId);

        protected abstract string SearchUrl { get; }

        protected virtual bool EncodeSearchTerm => true;

        protected abstract string GetExternalUrl(string id);

        protected abstract IHtmlMetadataExtractor GetMovieHtmlExtractor(string movieId);

        protected IHtmlSearchResultExtractor SearchResultsHtmlExtractor { get; set; }
    }

    public class MovieResult
    {
        public MovieResult()
        {
            Actors = new List<MoviePerson>();
        }
        public bool HasMetadata { get; set; }
        public string Title { get; set; }
        public string Synopsis { get; set; }
        public List<string> Genres { get; set; }
        public MoviePerson Director { get; set; }
        public List<MoviePerson> Actors { get; set; }
        public string Studio { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public int? ProductionYear { get; set; }
        public string UpcCode { get; set; }
        public string Set { get; set; }
        public string Id { get; set; }
        public string PrimaryImageUrl { get; set; }
    }

    public class MoviePerson
    {
        public string Name { get; set; }

        public string Id { get; set; }
    }
}
