using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Shoko.Models.Enums;
using Shoko.Models.Plex.TVShow;
using Shoko.Models.Server.CrossRef;
using Shoko.Models.WebCache;
using Shoko.WebCache.Database;
using Shoko.WebCache.Models;
using Shoko.WebCache.Models.Database;
using WebCache_CrossRef_File_Episode = Shoko.WebCache.Models.Database.WebCache_CrossRef_File_Episode;

namespace Shoko.WebCache.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class CrossRefController : InjectedController
    {
        public CrossRefController(IConfiguration cfg, WebCacheContext ctx, IMemoryCache mc) : base(cfg, ctx, mc)
        {
        }


        [HttpGet("AniDB_Provider/{token}/{animeId}/{crossRefType}")]
        public async Task<IActionResult> GetAniDB_ProviderAsync(string token, int animeId, CrossRefType crossRefType)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;

            List<WebCache_Reliability<CrossRef_AniDB_Provider>> results = new List<WebCache_Reliability<CrossRef_AniDB_Provider>>();
            //First check for admin approved link
            WebCache_CrossRef_AniDB_Provider r = await _db.CrossRef_AniDB_Providers.FirstOrDefaultAsync(a => a.Approved == WebCache_RoleType.Admin && a.AnimeID == animeId && a.CrossRefType == crossRefType);
            if (r != null)
            {
                results.Add(new WebCache_Reliability<CrossRef_AniDB_Provider> {Result = r, Type = WebCache_ReliabilityType.AdminVerified});
                if (s.Role != WebCache_RoleType.Admin)
                    return new JsonResult(results);
                //If not Admin, early exit, otherwise other admin might want to evaluate
            }

            //Second Check for Moderator Approved Link
            List<WebCache_CrossRef_AniDB_Provider> rl = await _db.CrossRef_AniDB_Providers.Where(a => a.Approved == WebCache_RoleType.Moderator && a.AnimeID == animeId && a.CrossRefType == crossRefType).ToListAsync();
            if (rl.Count > 0)
                results.AddRange(rl.Select(a => new WebCache_Reliability<CrossRef_AniDB_Provider> {Result = a, Type = WebCache_ReliabilityType.ModeratorVerified}));
            //Then The user link
            r = await _db.CrossRef_AniDB_Providers.FirstOrDefaultAsync(a => a.AniDBUserId == s.AniDBUserId && a.AnimeID == animeId && a.CrossRefType == crossRefType);
            if (r != null)
                results.Add(new WebCache_Reliability<CrossRef_AniDB_Provider> {Result = r, Type = WebCache_ReliabilityType.User});
            //And Now, the popular ones.
            var res = await _db.CrossRef_AniDB_Providers.Where(a => a.AnimeID == animeId && a.CrossRefType == crossRefType && a.Approved == WebCache_RoleType.None).GroupBy(a => new {a.CrossRefID, a.EpisodesData, a.EpisodesOverrideData}).Select(a => new {Count = a.Count(), Result = a.First()}).OrderByDescending(a => a.Count).Take(5).ToListAsync();
            foreach (var n in res)
            {
                results.Add(new WebCache_Reliability<CrossRef_AniDB_Provider> {Result = n.Result, Type = WebCache_ReliabilityType.Popular, PopularityCount = n.Count});
            }

            if (results.Count > 0)
                return new JsonResult(results);
            return StatusCode(404, "CrossRef Not Found");
        }

        [HttpPost("AniDB_Provider/{token}/{approve?}")]
        public async Task<IActionResult> AddAniDB_ProviderAsync([FromBody] CrossRef_AniDB_Provider cross, string token, bool? approve)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            //Exists already?
            WebCache_CrossRef_AniDB_Provider r = await _db.CrossRef_AniDB_Providers.FirstOrDefaultAsync(a => a.AniDBUserId == s.AniDBUserId && a.AnimeID == cross.AnimeID && a.CrossRefType == cross.CrossRefType);
            if (r == null)
            {
                r = new WebCache_CrossRef_AniDB_Provider();
                _db.Add(r);
            }

            WebCache_RoleType rt = GetRole(s.AniDBUserId);
            r.FillWith(cross);
            //If user is Admin, and this come with approve flag, let approve it, and clean any other approval from the db
            if (rt == WebCache_RoleType.Admin && approve.HasValue && approve.Value)
            {
                r.Approved = WebCache_RoleType.Admin;
                List<WebCache_CrossRef_AniDB_Provider> reset_admins = await _db.CrossRef_AniDB_Providers.Where(a => a.AnimeID == cross.AnimeID && a.CrossRefType == cross.CrossRefType && a.AniDBUserId != s.AniDBUserId && a.Approved == WebCache_RoleType.Admin).ToListAsync();
                foreach (WebCache_CrossRef_AniDB_Provider w in reset_admins)
                    w.Approved = WebCache_RoleType.None;
            }
            //If moderator, simple tag it.
            else if (rt == WebCache_RoleType.Moderator)
                r.Approved = WebCache_RoleType.Moderator;
            else
                r.Approved = WebCache_RoleType.None;

            r.AniDBUserId = s.AniDBUserId;
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("AniDB_Provider/{token}/{animeId}/{crossRefType}")]
        public async Task<IActionResult> DeleteAniDB_ProviderAsync(string token, int animeId, CrossRefType crossRefType)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            WebCache_CrossRef_AniDB_Provider r = await _db.CrossRef_AniDB_Providers.FirstOrDefaultAsync(a => a.AniDBUserId == s.AniDBUserId && a.AnimeID == animeId && a.CrossRefType == crossRefType);
            if (r == null)
                return StatusCode(404, "CrossRef Not Found");
            _db.Remove(r);
            await _db.SaveChangesAsync();
            return Ok();
        }


        [HttpGet("File_Episode/{token}/{hash}")]
        public async Task<IActionResult> GetFile_EpisodeAsync(string token, string hash)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            WebCache_CrossRef_File_Episode ep = await _db.CrossRef_File_Episodes.FirstOrDefaultAsync(a => a.Hash == hash && a.AniDBUserId == s.AniDBUserId);
            if (ep==null)
                return StatusCode(404, "CrossRef Not Found");
            return new JsonResult(ep);
        }
        [HttpPost("File_Episode/{token}")]
        public async Task<IActionResult> AddFile_EpisodeAsync(string token, [FromBody] WebCache_CrossRef_File_Episode episode)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            WebCache_CrossRef_File_Episode ep = await _db.CrossRef_File_Episodes.FirstOrDefaultAsync(a => a.Hash == episode.Hash && a.AniDBUserId == s.AniDBUserId);
            if (ep == null)
            {
                ep=new WebCache_CrossRef_File_Episode();
                _db.Add(ep);
            }
            ep.FillWith(episode);
            ep.AniDBUserId = s.AniDBUserId;
            await _db.SaveChangesAsync();
            return Ok();
        }
        [HttpDelete("File_Episode/{token}/{hash}")]
        public async Task<IActionResult> DeleteFile_EpisodeAsync(string token, string hash)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            WebCache_CrossRef_File_Episode ep = await _db.CrossRef_File_Episodes.FirstOrDefaultAsync(a => a.Hash == hash && a.AniDBUserId == s.AniDBUserId);
            if (ep == null)
                return StatusCode(404, "CrossRef Not Found");
            _db.Remove(ep);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}