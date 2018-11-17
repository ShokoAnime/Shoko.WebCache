using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shoko.WebCache.Models.Database
{
    public class WebCache_Ban
    {
        public int AniDBUserId { get; set; }
        public string Reason { get; set; }
        public DateTime ExpirationUTC { get; set; }
    }
}
