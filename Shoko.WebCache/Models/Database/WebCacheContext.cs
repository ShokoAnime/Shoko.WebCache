using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Shoko.WebCache.Models.Database
{
    public class WebCacheContext : DbContext
    {
        public WebCacheContext(DbContextOptions<WebCacheContext> options) : base(options)
        {

        }
        public DbSet<Session> Sessions { get; set; }
    }
}
