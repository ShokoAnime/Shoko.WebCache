using System.Collections.Generic;

namespace Shoko.WebCache.Models.Shared.OAuth
{
    public class AniDBLoggedInfo
    {
        public string UserName { get; set; }
        public Dictionary<string,string> Cookies { get; set; }
    }
}
