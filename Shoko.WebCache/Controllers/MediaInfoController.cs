using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Shoko.Models.WebCache;
using Shoko.WebCache.Database;
using Shoko.WebCache.Models;

namespace Shoko.WebCache.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class MediaInfoController : InjectedController
    {
        public MediaInfoController(IConfiguration cfg, WebCacheContext ctx, IMemoryCache mc) : base(cfg, ctx, mc)
        {
        }


        [HttpGet("{token}/{ed2k}")]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        [Produces(typeof(Shoko.Models.WebCache.WebCache_Media))]
        public async Task<IActionResult> GetMediaInfo(string token, string ed2k)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            ed2k = ed2k.ToUpperInvariant();
            Models.Database.WebCache_Media m = await _db.WebCache_Medias.FirstOrDefaultAsync(a => a.ED2K == ed2k);
            if (m == null)
                return StatusCode(404, "Media Not Found");
            return new JsonResult(m);
        }

        [HttpPost("{token}")]
        [ProducesResponseType(403)]
        public async Task<IActionResult> AddMediaInfo(string token, [FromBody] WebCache_Media media)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            media.ED2K = media.ED2K.ToUpperInvariant();
            Models.Database.WebCache_Media m = await _db.WebCache_Medias.FirstOrDefaultAsync(a => a.ED2K == media.ED2K);
            if (m == null)
            {
                m = new Models.Database.WebCache_Media();
                _db.Add(m);
            }
            else if (m.Version >= media.Version)
            {
                return Ok();
            }

            m.Version = media.Version;
            m.MediaInfo = media.MediaInfo;
            m.ED2K = media.ED2K;
            m.CreationDate = DateTime.UtcNow;
            m.AniDBUserId = s.AniDBUserId;
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}