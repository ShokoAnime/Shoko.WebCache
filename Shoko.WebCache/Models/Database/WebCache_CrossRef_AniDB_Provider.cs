using Shoko.Models.Enums;
using Shoko.Models.WebCache;

namespace Shoko.WebCache.Models.Database
{
    public class WebCache_CrossRef_AniDB_Provider
    {
        public int WebCache_AniDB_ProviderID { get; set; }
        public int AnimeID { get; set; }
        public string CrossRefID { get; set; }
        public CrossRefSource CrossRefSource { get; set; }
        public CrossRefType CrossRefType { get; set; }
        public string EpisodesOverrideData { get; set; }
        public bool IsAdditive { get; set; }
        public int AniDBUserId { get; set; }
        public WebCache_RoleType Approved { get; set; }
    }
}