using System;
using System.Collections.Generic;
using AngleSharp.Dom;
using MediaBrowser.Controller.Entities;

namespace AdultEmby.Plugins.Base
{
    public interface IHtmlMetadataExtractor
    {
        string GetTitle(IDocument htmlDocument);

        string GetSynopsis(IDocument htmlDocument);

        List<string> GetGenres(IDocument htmlDocument);

        MoviePerson GetDirector(IDocument htmlDocument);

        string GetStudio(IDocument htmlDocument);

        DateTime? GetReleaseDate(IDocument htmlDocument);

        int? GetProductionYear(IDocument htmlDocument);

        List<MoviePerson> GetActors(IDocument htmlDocument);

        string GetSet(IDocument htmlDocument);

        string GetUpcCode(IDocument htmlDocument);

        string GetPrimaryImageUrl(IDocument htmlDocument);
    }
}
