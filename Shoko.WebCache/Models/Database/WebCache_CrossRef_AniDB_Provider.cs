using Newtonsoft.Json;
using Shoko.Models.Server.CrossRef;
using Shoko.Models.WebCache;

namespace Shoko.WebCache.Models.Database
{
    public class WebCache_CrossRef_AniDB_Provider : CrossRef_AniDB_Provider
    {
        [JsonIgnore]
        public int AniDBUserId { get; set; }

        [JsonIgnore]
        public WebCache_RoleType Approved { get; set; }
    }
}