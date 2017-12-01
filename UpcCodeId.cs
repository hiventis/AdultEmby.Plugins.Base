using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace AdultEmby.Plugins.Base
{
    public class UpcCodeId : IExternalId
    {
        public string Key
        {
            get { return KeyName; }
        }

        public string Name
        {
            get { return "UPC Code"; }
        }

        public bool Supports(IHasProviderIds item)
        {
            return item is Movie;
        }

        public string UrlFormatString
        {
            get
            {
                return null;
            }
        }

        public static string KeyName
        {
            get
            {
                return "UpcCode";
            }
        }
    }
}
