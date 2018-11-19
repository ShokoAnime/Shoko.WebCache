using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Models.WebCache;
using Shoko.WebCache.Database;
using Shoko.WebCache.Models;
using Shoko.WebCache.Models.Database;
using WebCache_CrossRef_AniDB_Provider = Shoko.Models.WebCache.WebCache_CrossRef_AniDB_Provider;

namespace Shoko.WebCache.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class CrossRefController : InjectedController
    {
        public CrossRefController(IConfiguration cfg, WebCacheContext ctx, IMemoryCache mc) : base(cfg, ctx, mc)
        {
        }
        [HttpGet("AniDB_Provider/Random/{token}/{crossRefType}")]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        [Produces(typeof(List<WebCache_CrossRef_AniDB_Provider>))]
        public async Task<IActionResult> GetRandomProvider(string token, int crossRefType)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            if ((s.Role & WebCache_RoleType.Admin) == 0)
                return StatusCode(403, "Admin Only");
            int animeid = await _db.CrossRef_AniDB_Providers.Where(a => a.CrossRefType == (CrossRefType)crossRefType).GroupBy(a => a.AnimeID).Where(a => !a.Any(b => b.Approved == WebCache_RoleType.Admin)).Select(a => a.Key).FirstOrDefaultAsync();
            if (animeid == 0)
                return StatusCode(404, "CrossRef Not Found, All Approved :)");
            return await GetProviderInternal(s, animeid, (CrossRefType)crossRefType);
        }
        [HttpPost("AniDB_Provider/Batch/{token}/{approve?}")]
        [ProducesResponseType(403)]
        public async Task<IActionResult> AddProviderBatch([FromBody] List<WebCache_CrossRef_AniDB_Provider> crosses, string token, bool? approve)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            if (approve.HasValue && approve.Value && (s.Role & WebCache_RoleType.Admin) == 0)
                return StatusCode(403, "Admin Only");
            //Exists already?
            foreach (WebCache_CrossRef_AniDB_Provider cross in crosses)
                await AddProviderInternal(s, cross, approve);
            await _db.SaveChangesAsync();
            return Ok();
        }
        [HttpGet("AniDB_Provider/{token}/{animeId}/{crossRefType}")]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        [Produces(typeof(List<WebCache_CrossRef_AniDB_Provider>))]
        public async Task<IActionResult> GetProvider(string token, int animeId, int crossRefType)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            return await GetProviderInternal(s, animeId, (CrossRefType) crossRefType);
        }

        private async Task<IActionResult> GetProviderInternal(SessionInfoWithError s, int animeId, CrossRefType crossRefType)
        {
            List<WebCache_CrossRef_AniDB_Provider> results = new List<WebCache_CrossRef_AniDB_Provider>();
            //First check for admin approved link
            Models.Database.WebCache_CrossRef_AniDB_Provider r = await _db.CrossRef_AniDB_Providers.FirstOrDefaultAsync(a => a.Approved == WebCache_RoleType.Admin && a.AnimeID == animeId && a.CrossRefType == crossRefType);
            if (r != null)
            {
                results.Add(r.ToWebCache(WebCache_ReliabilityType.AdminVerified, 0));
                if ((s.Role & WebCache_RoleType.Admin) == 0)
                    return new JsonResult(results);
                //If not Admin, early exit, otherwise other admin might want to evaluate
            }

            //Second Check for Moderator Approved Link
            List<Models.Database.WebCache_CrossRef_AniDB_Provider> rl = await _db.CrossRef_AniDB_Providers.Where(a => a.Approved == WebCache_RoleType.Moderator && a.AnimeID == animeId && a.CrossRefType == crossRefType).ToListAsync();
            if (rl.Count > 0)
                results.AddRange(rl.Select(a => a.ToWebCache(WebCache_ReliabilityType.ModeratorVerified, 0)));
            //Then The user link
            r = await _db.CrossRef_AniDB_Providers.FirstOrDefaultAsync(a => a.AniDBUserId == s.AniDBUserId && a.AnimeID == animeId && a.CrossRefType == crossRefType);
            if (r != null)
                results.Add(r.ToWebCache(WebCache_ReliabilityType.User, 0));
            //And Now, the popular ones.
            var res = await _db.CrossRef_AniDB_Providers.Where(a => a.AnimeID == animeId && a.CrossRefType == crossRefType && a.Approved == WebCache_RoleType.None).GroupBy(a => new {a.CrossRefID, a.EpisodesOverrideData}).Select(a => new {Count = a.Count(), Result = a.First()}).OrderByDescending(a => a.Count).Take(5).ToListAsync();
            foreach (var n in res)
            {
                results.Add(n.Result.ToWebCache(WebCache_ReliabilityType.Popular, n.Count));
            }

            if (results.Count > 0)
                return new JsonResult(results);
            return StatusCode(404, "CrossRef Not Found");
        }

       

        private async Task AddProviderInternal(SessionInfoWithError s, WebCache_CrossRef_AniDB_Provider cross, bool? approve)
        {
            Models.Database.WebCache_CrossRef_AniDB_Provider r = await _db.CrossRef_AniDB_Providers.FirstOrDefaultAsync(a => a.AniDBUserId == s.AniDBUserId && a.AnimeID == cross.AnimeID && a.CrossRefType == cross.CrossRefType);
            if (r == null)
            {
                r = new Models.Database.WebCache_CrossRef_AniDB_Provider();
                _db.Add(r);
            }

            WebCache_RoleType rt = GetRole(s.AniDBUserId);
            r.FillWith(cross);
            //If user is Admin, and this come with approve flag, let approve it, and clean any other approval from the db
            if ((rt & WebCache_RoleType.Admin) > 0 && approve.HasValue && approve.Value)
            {
                r.Approved = WebCache_RoleType.Admin;
                List<Models.Database.WebCache_CrossRef_AniDB_Provider> reset_admins = await _db.CrossRef_AniDB_Providers.Where(a => a.AnimeID == cross.AnimeID && a.CrossRefType == cross.CrossRefType && a.AniDBUserId != s.AniDBUserId && a.Approved == WebCache_RoleType.Admin).ToListAsync();
                foreach (Models.Database.WebCache_CrossRef_AniDB_Provider w in reset_admins)
                    w.Approved = WebCache_RoleType.None;
            }
            //If moderator, simple tag it.
            else if ((rt & WebCache_RoleType.Moderator) > 0)
                r.Approved = WebCache_RoleType.Moderator;
            else
                r.Approved = WebCache_RoleType.None;

            r.AniDBUserId = s.AniDBUserId;
        }

        [HttpPost("AniDB_Provider/{token}/{approve?}")]
        [ProducesResponseType(403)]
        public async Task<IActionResult> AddProvider([FromBody] WebCache_CrossRef_AniDB_Provider cross, string token, bool? approve)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            if (approve.HasValue && approve.Value && (s.Role & WebCache_RoleType.Admin) == 0)
                return StatusCode(403, "Admin Only");
            //Exists already?
            await AddProviderInternal(s, cross, approve);            
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("AniDB_Provider/Manage/{token}/{id}/{approve}")]
        [ProducesResponseType(403)]
        public async Task<IActionResult> ProviderManage(string token, int id, bool approve)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            if ((s.Role & WebCache_RoleType.Admin) == 0)
                return StatusCode(403, "Admin Only");
            //Exists already?
            Models.Database.WebCache_CrossRef_AniDB_Provider r = await _db.CrossRef_AniDB_Providers.FirstOrDefaultAsync(a => a.WebCache_AniDB_ProviderID == id);
            if (r == null)
                return StatusCode(404, "CrossRef Not Found");
            if (approve)
            {
                r.Approved = WebCache_RoleType.Admin;
                List<Models.Database.WebCache_CrossRef_AniDB_Provider> reset_admins = await _db.CrossRef_AniDB_Providers.Where(a => a.AnimeID == r.AnimeID && a.CrossRefType == r.CrossRefType && a.AniDBUserId != s.AniDBUserId && a.Approved == WebCache_RoleType.Admin && a.WebCache_AniDB_ProviderID != r.WebCache_AniDB_ProviderID).ToListAsync();
                foreach (Models.Database.WebCache_CrossRef_AniDB_Provider w in reset_admins)
                    w.Approved = WebCache_RoleType.None;
            }
            else
            {
                r.Approved = WebCache_RoleType.None;
            }

            r.AniDBUserId = s.AniDBUserId;
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("AniDB_Provider/{token}/{animeId}/{crossRefType}")]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteProvider(string token, int animeId, int crossRefType)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            Models.Database.WebCache_CrossRef_AniDB_Provider r = await _db.CrossRef_AniDB_Providers.FirstOrDefaultAsync(a => a.AniDBUserId == s.AniDBUserId && a.AnimeID == animeId && a.CrossRefType == (CrossRefType) crossRefType);
            if (r == null)
                return StatusCode(404, "CrossRef Not Found");
            _db.Remove(r);
            await _db.SaveChangesAsync();
            return Ok();
        }
        [HttpPost("File_Episode/Batch/{token}")]
        [ProducesResponseType(403)]
        public async Task<IActionResult> AddFileEpisodeBatch(string token, [FromBody] List<CrossRef_File_Episode> episodes)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            foreach (CrossRef_File_Episode episode in episodes)
            {
                WebCache_CrossRef_File_Episode ep = await _db.CrossRef_File_Episodes.FirstOrDefaultAsync(a => a.CrossRef_File_EpisodeID == episode.CrossRef_File_EpisodeID && a.AniDBUserId == s.AniDBUserId);
                if (ep == null)
                {
                    ep = new WebCache_CrossRef_File_Episode();
                    _db.Add(ep);
                }
                ep.FillWith(episode);
                ep.AniDBUserId = s.AniDBUserId;
            }
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("File_Episode/{token}/{hash}")]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        [Produces(typeof(List<CrossRef_File_Episode>))]
        public async Task<IActionResult> GetFileEpisodes(string token, string hash)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            List<CrossRef_File_Episode> ep = await _db.CrossRef_File_Episodes.Where(a => a.Hash == hash && a.AniDBUserId == s.AniDBUserId).Cast<CrossRef_File_Episode>().ToListAsync();
            if (ep.Count == 0)
                return StatusCode(404, "CrossRef Not Found");
            return new JsonResult(ep);
        }

        [HttpPost("File_Episode/{token}")]
        [ProducesResponseType(403)]
        public async Task<IActionResult> AddFileEpisode(string token, [FromBody] CrossRef_File_Episode episode)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            WebCache_CrossRef_File_Episode ep = await _db.CrossRef_File_Episodes.FirstOrDefaultAsync(a => a.CrossRef_File_EpisodeID == episode.CrossRef_File_EpisodeID && a.AniDBUserId == s.AniDBUserId);
            if (ep == null)
            {
                ep = new WebCache_CrossRef_File_Episode();
                _db.Add(ep);
            }

            ep.FillWith(episode);
            ep.AniDBUserId = s.AniDBUserId;
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("File_Episode/{token}/{hash}/{episodeid}")]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteFileEpisode(string token, string hash, int episodeid)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            WebCache_CrossRef_File_Episode ep = await _db.CrossRef_File_Episodes.FirstOrDefaultAsync(a => a.CrossRef_File_EpisodeID == episodeid && a.Hash==hash && a.AniDBUserId == s.AniDBUserId);
            if (ep == null)
                return StatusCode(404, "CrossRef Not Found");
            _db.Remove(ep);
            await _db.SaveChangesAsync();
            return Ok();
        }
        [HttpDelete("File_Episode/{token}/{hash}")]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteFileEpisodes(string token, string hash)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            List<WebCache_CrossRef_File_Episode> eps = await _db.CrossRef_File_Episodes.Where(a => a.Hash == hash && a.AniDBUserId == s.AniDBUserId).ToListAsync();
            if (eps.Count==0)
                return StatusCode(404, "CrossRef Not Found");
            _db.RemoveRange(eps);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}