using System;
using AngleSharp.Dom.Html;

namespace AdultEmby.Plugins.Base
{
    public interface IHtmlPersonExtractor
    {
        bool HasMetadata(IHtmlDocument htmlDocument);

        string GetName(IHtmlDocument htmlDocument);

        string GetPrimaryImageUrl(IHtmlDocument htmlDocument);

        DateTime? GetBirthdate(IHtmlDocument htmlDocument);

        string GetStarSign(IHtmlDocument htmlDocument);

        string GetMeasurements(IHtmlDocument htmlDocument);
    
        string GetHeight(IHtmlDocument htmlDocument);

        string GetWeight(IHtmlDocument htmlDocument);

        string GetTwitter(IHtmlDocument htmlDocument);

        //string GetOfficialSite(IHtmlDocument htmlDocument);

        string GetNationality(IHtmlDocument htmlDocument);

        string GetBirthplace(IHtmlDocument htmlDocument);

        string GetEthnicity(IHtmlDocument htmlDocument);
    }
}
