using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shoko.WebCache.Models.Database
{
    [Flags]
    public enum RoleType
    {
        None=0,
        Admin=1,
        Moderator=2
    }
}
