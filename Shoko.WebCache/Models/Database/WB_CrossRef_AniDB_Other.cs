using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Models.Server;

namespace Shoko.WebCache.Models.Database
{
    public class WB_CrossRef_AniDB_Other : CrossRef_AniDB_Other
    {
        public int AniDBUserId { get; set; }

        public bool AdminApproved { get; set; }
    }
}
