using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Shoko.Models.Server;
using Shoko.Models.WebCache;
using Shoko.WebCache.Models.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Shoko.Models.Enums;

namespace Shoko.WebCache.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class CrossRefController : InjectedController
    {
        public CrossRefController(IConfiguration cfg, WebCacheContext ctx, IMemoryCache mc) : base(cfg,ctx,mc)
        {
        }
        [HttpGet("AniDB_Other/{token}/{animeId}/{crossRefType")]
        public async Task<IActionResult> GetAniDB_OtherAsync(string token, int animeId, int crossRefType)
        {
            Session s = await VerifyTokenAsync(token);
            if (s == null)
                return StatusCode(403, "Invalid Token");
            //User one
            WB_CrossRef_AniDB_Other r = await _db.CrossRef_AniDB_Others.FirstOrDefaultAsync(a => a.AniDBUserId == s.AniDBUserId && a.AnimeID == animeId && a.CrossRefType == crossRefType);
            if (r != null)
                return new JsonResult(new WebCache_Reliability<CrossRef_AniDB_Other>{ Result=r,Type=WebCache_ReliabilityType.User});
            //Admin Approved one
            r = await _db.CrossRef_AniDB_Others.FirstOrDefaultAsync(a => a.Approved && a.AnimeID == animeId && a.CrossRefType == crossRefType);
            if (r != null)
                return new JsonResult(new WebCache_Reliability<CrossRef_AniDB_Other> { Result = r, Type = WebCache_ReliabilityType.Verified });
            //Most popular
            var res = await _db.CrossRef_AniDB_Others.Where(a => a.AnimeID == animeId && a.CrossRefType == crossRefType).
                GroupBy(a => a.CrossRefID).Select(a => new {cnt = a.Count(), which = a.First()}).
                OrderByDescending(a => a.cnt).FirstOrDefaultAsync();
            if (res!=null)
                return new JsonResult(new WebCache_Reliability<CrossRef_AniDB_Other> { Result = res.which, Type = WebCache_ReliabilityType.Popular, PopularityCount = res.cnt });
            return StatusCode(404, "CrossRef Not Found");
        }

        [HttpPost("AniDB_Other/{token}")]
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
            RoleType rt = GetRole(s.AniDBUserId);
            r.AnimeID = cross.AnimeID;
            r.Approved = (rt&RoleType.Moderator)>0;
            r.CrossRefSource = cross.CrossRefSource;
            r.CrossRefType = cross.CrossRefType;
            r.CrossRefID = cross.CrossRefID;
            r.AniDBUserId = s.AniDBUserId;
            if (r.Approved)
            {
                //Clean previous admin approvals.
                List<WB_CrossRef_AniDB_Other> reset_admins = await _db.CrossRef_AniDB_Others.Where(a => a.AnimeID == cross.AnimeID && a.CrossRefType == cross.CrossRefType && a.AniDBUserId!=s.AniDBUserId && a.Approved).ToListAsync();
                foreach (WB_CrossRef_AniDB_Other w in reset_admins)
                    w.Approved = false;
            }
            await _db.SaveChangesAsync();
            return Ok();
        }
        [HttpDelete("AniDB_Other/{token}/{animeId}/{crossRefType")]
        public async Task<IActionResult> DeleteAniDB_OtherAsync(string token, int animeId, int crossRefType)
        {
            Session s = await VerifyTokenAsync(token);
            if (s == null)
                return StatusCode(403, "Invalid Token");
            //User one
            WB_CrossRef_AniDB_Other r = await _db.CrossRef_AniDB_Others.FirstOrDefaultAsync(a => a.AniDBUserId == s.AniDBUserId && a.AnimeID == animeId && a.CrossRefType == crossRefType);
            if (r == null)
                return StatusCode(404, "CrossRef Not Found");
            _db.Remove(r);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}