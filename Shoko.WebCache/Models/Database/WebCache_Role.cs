using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Models.WebCache;

namespace Shoko.WebCache.Models.Database
{
    public class WebCache_Role
    {
        public int AniDBUserId { get; set; }

        public WebCache_RoleType Type { get; set; }
    }
}
