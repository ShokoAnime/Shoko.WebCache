using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shoko.Models.WebCache;
using Shoko.WebCache.Database;
using Shoko.WebCache.Models;

namespace Shoko.WebCache.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class MediaInfoController : InjectedController
    {
        public MediaInfoController(IConfiguration cfg, WebCacheContext ctx, IMemoryCache mc, ILogger<MediaInfoController> logger) : base(cfg, ctx, mc, logger)
        {
        }


        [HttpGet("{token}/{ed2k}")]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        [Produces(typeof(WebCache_Media))]
        public async Task<IActionResult> GetMediaInfo(string token, string ed2k)
        {
            try
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
            catch (Exception e)
            {
                _logger.LogError(e, $"GETMEDIAINFO with Token={token} ED2K={ed2k}");
                return StatusCode(500);
            }
        }

        private async Task<bool> AddMediaInfoInternal(SessionInfoWithError s, WebCache_Media media)
        {
            try
            {
                Models.Database.WebCache_Media m = await _db.WebCache_Medias.FirstOrDefaultAsync(a => a.ED2K == media.ED2K);
                if (m == null)
                {
                    m = new Models.Database.WebCache_Media();
                    _db.Add(m);
                }
                else if (m.Version >= media.Version)
                {
                    return false;
                }

                m.Version = media.Version;
                m.MediaInfo = media.MediaInfo;
                m.ED2K = media.ED2K;
                m.CreationDate = DateTime.UtcNow;
                m.AniDBUserId = s.AniDBUserId;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

        }
        [HttpPost("{token}")]
        [ProducesResponseType(403)]
        [ProducesResponseType(500)]

        public async Task<IActionResult> AddMediaInfo(string token, [FromBody] WebCache_Media media)
        {
            try
            {
                SessionInfoWithError s = await VerifyTokenAsync(token);
                if (s.Error != null)
                    return s.Error;
                media.ED2K = media.ED2K.ToUpperInvariant();
                if (await AddMediaInfoInternal(s, media))
                    await _db.SaveChangesAsync();
                return Ok();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"ADDMEDIAINFO with Token={token} ED2K={media.ED2K} Version={media.Version}");
                return StatusCode(500);
            }

        }
        [HttpPost("Batch/{token}")]
        [ProducesResponseType(403)]
        [ProducesResponseType(500)]

        public async Task<IActionResult> AddMediaInfoBatch(string token, [FromBody] List<WebCache_Media> medias)
        {
            try
            {
                SessionInfoWithError s = await VerifyTokenAsync(token);
                if (s.Error != null)
                    return s.Error;
                bool persist = false;
                foreach (WebCache_Media media in medias)
                {
                    media.ED2K = media.ED2K.ToUpperInvariant();
                    if (await AddMediaInfoInternal(s, media))
                        persist = true;
                }
                if (persist)
                    await _db.SaveChangesAsync();
                return Ok();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"ADDMEDIAINFOBATCH with Token={token} Medias={medias.Count}");
                return StatusCode(500);
            }

        }
    }
}