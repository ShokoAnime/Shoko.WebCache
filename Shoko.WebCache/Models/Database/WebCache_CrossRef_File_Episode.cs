using Newtonsoft.Json;
using Shoko.Models.Server;

namespace Shoko.WebCache.Models.Database
{
    public class WebCache_CrossRef_File_Episode : CrossRef_File_Episode
    {
        [JsonIgnore]
        public int AniDBUserId { get; set; }
    }
}