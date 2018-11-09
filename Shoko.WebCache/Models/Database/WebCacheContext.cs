using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shoko.Models.Server;

namespace Shoko.WebCache.Models.Database
{
    public class WebCacheContext : DbContext
    {
        public WebCacheContext(DbContextOptions<WebCacheContext> options) : base(options)
        {

        }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<WB_CrossRef_AniDB_Other> CrossRef_AniDB_Others { get; set; }
    }
}
