using System;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Parser.Html;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace AdultEmby.Plugins.Base
{
    public abstract class AdultEmbyProviderBase
    {
        internal readonly SemaphoreSlim ResourcePool = new SemaphoreSlim(1, 1);

        protected AdultEmbyProviderBase(IHttpClient httpClient, IServerConfigurationManager configurationManager, IFileSystem fileSystem, ILogManager logManager, IJsonSerializer jsonSerializer)
        {
            HttpClient = httpClient;
            ConfigurationManager = configurationManager;
            FileSystem = fileSystem;
            Logger = logManager.GetLogger(GetType().FullName);
            JsonSerializer = jsonSerializer;
            HtmlParser = new HtmlParser();
            NonSearchTrottle = new Throttle(TimeSpan.FromSeconds(0), GetType().Name + "NonSearch", logManager);
            SearchTrottle = new Throttle(TimeSpan.FromSeconds(5), GetType().Name + "Search", logManager);
            SearchSemaphore = new SemaphoreSlim(1);
            NonSearchSemaphore = new SemaphoreSlim(1);
        }

        protected ILogger Logger { get; }

        protected IThrottle NonSearchTrottle { get; }

        protected IThrottle SearchTrottle { get; }

        protected IFileSystem FileSystem { get; private set; }

        protected HtmlParser HtmlParser { get; private set; }

        protected IHttpClient HttpClient { get; }

        private SemaphoreSlim SearchSemaphore { get;  }

        private SemaphoreSlim NonSearchSemaphore { get; }

        protected IServerConfigurationManager ConfigurationManager { get; private set; }

        protected IJsonSerializer JsonSerializer { get; private set; }

        protected virtual async Task<HttpResponseInfo> HandleSecurityPassthrough(HttpResponseInfo httpResponseInfo, string url, CancellationToken cancellationToken)
        {
            await Task.FromResult(true);
            return httpResponseInfo;
        }

        protected async Task<HttpResponseInfo> InternalGetResponse(string url, CancellationToken cancellationToken)
        {
            return await InternalGetResponse(url, true, cancellationToken);
        }

        protected async Task<HttpResponseInfo> InternalGetResponse(string url, bool handleSecurityPassthrough, CancellationToken cancellationToken)
        {
            Logger.Info("Making http request for [{0}]", url);
            HttpRequestOptions options = this.CreateHttpRequestOptions(url, cancellationToken);
            HttpResponseInfo httpResponseInfo = await HttpClient.GetResponse(options);
            if (handleSecurityPassthrough)
            {
                return await HandleSecurityPassthrough(httpResponseInfo, url, cancellationToken);
            }
            else
            {
                return httpResponseInfo;
            }
        }

        public async Task<HttpResponseInfo> GetResponse(string url, SemaphoreSlim semaphore, IThrottle throttle, CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await throttle.GetNext(cancellationToken);
                return await InternalGetResponse(url, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<HttpResponseInfo> GetResponse(string url, bool isSearchRequest, CancellationToken cancellationToken)
        {
            if (isSearchRequest)
            {
                return await GetResponse(url, SearchSemaphore, SearchTrottle, cancellationToken);
            }
            else
            {
                return await GetResponse(url, NonSearchSemaphore, NonSearchTrottle, cancellationToken);
            }
        }

        protected HttpRequestOptions CreateHttpRequestOptions(string url, CancellationToken cancellationToken)
        {
            return new HttpRequestOptions()
            {
                CancellationToken = cancellationToken,
                Url = url,
                UserAgent = UserAgent,
                ResourcePool = ResourcePool,
                LogErrorResponseBody = true,
                LogRequest = true
            };
        }

        private string UserAgent
        {
            get
            {
                //var version = _applicationHost.ApplicationVersion.ToString();
                //return string.Format("Emby/{0} +http://emby.media/", version);
                return "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.11; rv:50.0) Gecko/20100101 Firefox/50.0";
            }
        }
    }
}
