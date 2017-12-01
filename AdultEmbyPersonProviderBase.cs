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
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace AdultEmby.Plugins.Base
{
    public abstract class AdultEmbyPersonProviderBase : AdultEmbyProviderBase
    {
        public AdultEmbyPersonProviderBase(IHttpClient httpClient, IServerConfigurationManager configurationManager, IFileSystem fileSystem, ILogManager logManager, IJsonSerializer jsonSerializer)
            : base(httpClient, configurationManager, fileSystem, logManager, jsonSerializer)
        {

        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            Logger.Info("Retrieving person image [{0}]", url);
            return GetResponse(url, false, cancellationToken);
        }

        private async Task<List<SearchResult>> GetSearchResults(string name, 
		    CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(name))
            {
                Logger.Info("Skipping search because name is not supplied");
                return new List<SearchResult>();
            }
            var url = string.Format(SearchUrl, EncodeSearchTerm ? WebUtility.UrlEncode(name) : name);

            Logger.Info("Searching for metadata by name: [{0}] with url: [{1}]", name, url);
            string html;

            using (var httpResponseInfo = await GetResponse(url, true, cancellationToken))
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
                return new List<SearchResult>();
            }

            Logger.Info("Attempting to search parse response from [{0}]", url);
            IHtmlDocument htmlDocument = HtmlParser.Parse(html);

            List<SearchResult> searchResults = SearchResultsHtmlExtractor.GetSearchResults(htmlDocument);

            Logger.Info("Extracted {0} items from [{1}]", searchResults.Count, url);
            return searchResults;
        }

        internal async Task<string> EnsurePersonInfo(string personId, CancellationToken cancellationToken)
        {
            var cachePath = GetItemCachePath(FileSystem, ConfigurationManager.ApplicationPaths, personId);
            string jsonFile = Path.Combine(cachePath, "item.json");

            var fileInfo = FileSystem.GetFileInfo(jsonFile);
            Logger.Info("Cache file for [{0}] is: [{1}]", personId, jsonFile);
            // Check cache first
            if (!fileInfo.Exists || (DateTime.UtcNow - FileSystem.GetLastWriteTimeUtc(fileInfo)).TotalDays > 7)
            {
                Logger.Info("Refreshing cache file for [{0}]", personId);
                string url = GetExternalUrl(personId);

				Logger.Info("Person metadata source url: [{0}]", url);
                // Download and cache
                try
                {
                    using (var httpResponseInfo = await GetResponse(url, false, cancellationToken))
                    {
                        DirectoryInfo directoryInfo = Directory.CreateDirectory(/*Path.GetDirectoryName(*/cachePath/*)*/);
                        Logger.Info("Retrieved metadata from source [{0}]", url);
                        string htmlFile = Path.Combine(cachePath, "item.html");
                        //if (stream.StatusCode != HttpStatusCode.OK)
                        //    {
                        using (var fileStream = FileSystem.GetFileStream(htmlFile, FileOpenMode.Create,
                                FileAccessMode.Write,
                                FileShareMode.Read, true))
                        {
                            Logger.Info("Saving http reponse to cache file [{0}] ", htmlFile);
                            await httpResponseInfo.Content.CopyToAsync(fileStream).ConfigureAwait(false);
                        }
                        PersonResult personResult = ConvertHtmlToPersonResult(personId, htmlFile);
                        JsonSerializer.SerializeToFile(personResult, jsonFile);
                    }
                }
                catch (Exception)
                {
                    Logger.Info("No metadata from source [{0}]", url);
                    PersonResult personResult = new PersonResult()
                    {
                        Id = personId,
                        HasMetadata = false
                    };
                    JsonSerializer.SerializeToFile(personResult, jsonFile);
                }
            }
            return jsonFile;
        }

        private string GetOverview(params string[] values)
        {
            List<string> overviewValues = new List<string>();
            foreach (var value in values)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    overviewValues.Add(value);
                }
            }
            return string.Join("<br/>", overviewValues);
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo,
    CancellationToken cancellationToken)
        {
            var personId = searchInfo.GetProviderId(GetExternalIdName());

            if (!string.IsNullOrEmpty(personId))
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
                            Overview = result.Item.Overview,
                            ProductionYear = result.Item.ProductionYear,
                            ProviderIds = result.Item.ProviderIds,
                            SearchProviderName = GetProviderName(),
                            PremiereDate = result.Item.PremiereDate,
                            ImageUrl = result.Item.PrimaryImagePath
                        }
                    };
                }
            }

            Logger.Info("Search term is not an id so performing search");
            var items = await GetSearchResults(searchInfo.Name, cancellationToken);

            return items.Select(i =>
            {
                var result = new RemoteSearchResult
                {
                    Name = i.Name,
                    Overview = i.Overview,
                    ProductionYear = i.Year,
                    //ProviderIds =
                    SearchProviderName = GetProviderName(),
                    //PremiereDate = ,
                    ImageUrl = i.ImageUrl                    
                };

                result.SetProviderId(GetExternalIdName(), i.Id);

                return result;
            });
        }

        public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo personLookupInfo, CancellationToken cancellationToken)
        {
            var personId = personLookupInfo.GetProviderId(GetExternalIdName());

            if (string.IsNullOrEmpty(personId))
            {
                Logger.Info("Can't find a [{0}] id for [{1}]", GetExternalIdName(), personLookupInfo);
                return new MetadataResult<Person>()
                {
                    HasMetadata = false
                };
            }

            string cachePath = await EnsurePersonInfo(personId, cancellationToken).ConfigureAwait(false);

            Logger.Info("Received response, attempting to extract metadata");
            //PersonResult personResult = ConvertHtmlToPersonResult(personId, cachePath);
            PersonResult personResult = JsonSerializer.DeserializeFromFile<PersonResult>(cachePath);

            Logger.Info("Extracted metadata, attempting to populate metadata result");
            var item = new Person();
            var result = new MetadataResult<Person>();
            if (personResult.HasMetadata)
            {
                result.HasMetadata = true;

                item.Name = personResult.Name;

                item.Overview = GetOverview(
                    personResult.Height,
                    personResult.Weight,
                    personResult.Measurements,
                    personResult.Nationality,
                    personResult.Ethnicity,
                    personResult.StarSign
                );
                string birthPlace = personResult.Birthplace;
                //item.HomePageUrl = info.homepage;

                if (!string.IsNullOrWhiteSpace(birthPlace))
                {
                    //item.PlaceOfBirth = birthPlace;
                    item.ProductionLocations = new string[] { birthPlace };
                }

                DateTime? birthdate = personResult.Birthdate;
                if (birthdate.HasValue)
                {
                    item.PremiereDate = birthdate.Value.ToUniversalTime();
                    item.ProductionYear = birthdate.Value.Year;
                }

                if (!string.IsNullOrEmpty(personId))
                {
                    item.SetProviderId(GetExternalIdName(), personId);
                }
                string primaryImageUrl = personResult.PrimaryImageUrl;
                if (!string.IsNullOrEmpty(primaryImageUrl))
                {
                    var img = new ItemImageInfo()
                    {
                        Type = ImageType.Primary,
                        Path = primaryImageUrl
                    };
                    item.SetImage(img, 0);
                }
                result.HasMetadata = true;

                result.Item = item;
            }
            Logger.Info("Extracted metadata");
            return result;
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(IHasMetadata item, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();
            if (item is Person)
            {
                var personId = item.GetProviderId(GetExternalIdName());

                if (!string.IsNullOrEmpty(personId))
                {
                    Logger.Info("Retrieving person image details for [{0}]", personId);

                    string cachePath = await EnsurePersonInfo(personId, cancellationToken).ConfigureAwait(false);

                    //PersonResult personResult = ConvertHtmlToPersonResult(personId, cachePath);
                    PersonResult personResult = JsonSerializer.DeserializeFromFile<PersonResult>(cachePath);
                    if (personResult.HasMetadata)
                    {
                        string primaryImageUrl = personResult.PrimaryImageUrl;

                        var supportedImages = GetSupportedImages(item).ToList();

                        if (supportedImages.Contains(ImageType.Primary))
                        {
                            list.Add(new RemoteImageInfo
                            {
                                Url = primaryImageUrl,
                                ProviderName = GetProviderName(),
                                Type = ImageType.Primary
                            });
                        }
                    }
                }
            }

            return list;
        }

        private PersonResult ConvertHtmlToPersonResult(string personId, string cachePath)
        {
            Logger.Info("Parsing html for [{0}] in [{1}]", personId, cachePath);
            IHtmlDocument htmlDocument;
            using (var stream = FileSystem.GetFileStream(cachePath, FileOpenMode.Open, FileAccessMode.Read, FileShareMode.Read))
            {
                htmlDocument = HtmlParser.Parse(stream);
            }

            Logger.Info("Received response, attempting to extract metadata");
            PersonResult personResult = new PersonResult()
            {
                HasMetadata = PersonHtmlExtractor.HasMetadata(htmlDocument),
                Id = personId
            };

            if (personResult.HasMetadata)
            {
                personResult.Name = PersonHtmlExtractor.GetName(htmlDocument);
                personResult.Height = PersonHtmlExtractor.GetHeight(htmlDocument);
                personResult.Weight = PersonHtmlExtractor.GetWeight(htmlDocument);
                personResult.Measurements = PersonHtmlExtractor.GetMeasurements(htmlDocument);
                personResult.Nationality = PersonHtmlExtractor.GetNationality(htmlDocument);
                personResult.Ethnicity = PersonHtmlExtractor.GetEthnicity(htmlDocument);
                personResult.StarSign = PersonHtmlExtractor.GetStarSign(htmlDocument);
                personResult.Birthplace = PersonHtmlExtractor.GetBirthplace(htmlDocument);
                personResult.Birthdate = PersonHtmlExtractor.GetBirthdate(htmlDocument);
                personResult.PrimaryImageUrl = PersonHtmlExtractor.GetPrimaryImageUrl(htmlDocument);
            }
            return personResult;
        }

        public bool Supports(IHasMetadata item)
        {
            return item is Person;
        }

        public IEnumerable<ImageType> GetSupportedImages(IHasMetadata item)
        {
            return new List<ImageType> { ImageType.Primary };
        }

        protected abstract string GetExternalIdName();

        protected abstract string GetProviderName();

        protected abstract string GetItemCachePath(IFileSystem fileSystem, IApplicationPaths appPaths, string itemId);

        protected abstract string SearchUrl { get; }

        protected virtual bool EncodeSearchTerm => true;

        protected abstract string GetExternalUrl(string id);

        protected IHtmlPersonExtractor PersonHtmlExtractor { get; set; }

        protected IHtmlSearchResultExtractor SearchResultsHtmlExtractor { get; set; }
    }

    public class PersonResult
    {
        public bool HasMetadata { get; set; }
        public string Name { get; set; }
        public string Height { get; set; }
        public string Weight { get; set; }
        public string Measurements { get; set; }
        public string Nationality { get; set; }
        public string Ethnicity { get; set; }
        public string StarSign { get; set; }
        public string Birthplace { get; set; }
        public DateTime? Birthdate { get; set; }
        public string Id { get; set; }
        public string PrimaryImageUrl { get; set; }
    }
}
