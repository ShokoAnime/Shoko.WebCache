using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Shoko.Models.Server;
using Shoko.Models.WebCache;
using Shoko.WebCache.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace Shoko.WebCache.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class CrossRefController : InjectedController
    {
        public CrossRefController(IConfiguration Configuration, WebCacheContext Context) : base(Configuration, Context)
        {
        }

        [HttpPost("Add/AniDB_Other/{token}")]
        public async Task<IActionResult> AddAniDB_OtherAsync(CrossRef_AniDB_Other cross, string token)
        {
            Session s = await VerifyTokenAsync(token);
            if (s == null)
                return StatusCode(403, "Invalid Token");
            WB_CrossRef_AniDB_Other r=await _db.CrossRef_AniDB_Others.FirstOrDefaultAsync(a=>a.AniDBUserId==s.AniDBUserId && a.AnimeID==cross.AnimeID && a.CrossRefType==cross.CrossRefType);
            if (r == null)
            {
                r=new WB_CrossRef_AniDB_Other();
                _db.Add(r);
            }

            r.AnimeID = cross.AnimeID;
            r.AdminApproved = false;
            r.CrossRefSource = cross.CrossRefSource;
            r.CrossRefType = cross.CrossRefType;
            r.CrossRefID = cross.CrossRefID;
            r.AniDBUserId = s.AniDBUserId;
            await _db.SaveChangesAsync();
            return Ok();
        }

    }
}