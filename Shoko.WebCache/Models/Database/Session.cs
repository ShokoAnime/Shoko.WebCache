using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Shoko.WebCache.Models.Database
{
    public class Session
    {
        public string Token { get; set; }
        public int AniDBUserId { get; set; }
        public string AniDBUserName { get; set; }
        public DateTime Expiration { get; set; }
    }
}
