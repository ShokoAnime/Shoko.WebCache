using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Shoko.Models.WebCache;

namespace Shoko.WebCache.Models
{
    public class SessionInfoWithError : WebCache_SessionInfo
    {
        [JsonIgnore]
        public ObjectResult Error { get; set; }
    }
}