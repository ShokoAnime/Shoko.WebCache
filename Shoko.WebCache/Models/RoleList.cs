using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.WebCache.Models.Database;

namespace Shoko.WebCache.Models
{
    public class RoleList : List<Role>
    {
        public class RoleList(WebCacheContext db)
        {

        }
    }
}
