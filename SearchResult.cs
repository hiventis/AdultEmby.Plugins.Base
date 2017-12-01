using System;

namespace AdultEmby.Plugins.Base
{
    public class SearchResult
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public double Relevance { get; set; }

        public int? Year { get; set; }

        public string ImageUrl { get; set; }

        public string Overview { get; set; }

        public DateTime? PremiereDate { get; set; }

        public string Url { get; set;
            //get
            // {
            //     return Data18Constants.BaseUrl + Id;
            //}
        }
    }
}
