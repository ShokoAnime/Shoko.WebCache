using System;
using Newtonsoft.Json;

namespace Shoko.WebCache.Models.Database
{
    public class WebCache_Media : Shoko.Models.WebCache.WebCache_Media
    {
        [JsonIgnore]
        public int AniDBUserId { get; set; }

        [JsonIgnore]
        public DateTime CreationDate { get; set; }
    }
}