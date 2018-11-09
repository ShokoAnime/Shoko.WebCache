﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shoko.Models.Server;

namespace Shoko.WebCache.Models.Database
{
    public class WB_CrossRef_AniDB_Other : CrossRef_AniDB_Other
    {
        [JsonIgnore]
        public int AniDBUserId { get; set; }

        [JsonIgnore]
        public bool Approved { get; set; }
    }
}
