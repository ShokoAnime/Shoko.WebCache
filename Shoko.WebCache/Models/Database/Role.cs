using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shoko.WebCache.Models.Database
{
    public class Role
    {
        public int AniDBUserId { get; set; }

        public RoleType Role { get; set; }
    }
}
