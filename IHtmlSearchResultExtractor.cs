using AngleSharp.Dom.Html;
using System.Collections.Generic;
using AngleSharp.Dom;

namespace AdultEmby.Plugins.Base
{
    public interface IHtmlSearchResultExtractor
    {
        List<SearchResult> GetSearchResults(IDocument hmlDocument);
    }
}
