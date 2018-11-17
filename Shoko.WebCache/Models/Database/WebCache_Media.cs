using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shoko.Models.WebCache;

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
