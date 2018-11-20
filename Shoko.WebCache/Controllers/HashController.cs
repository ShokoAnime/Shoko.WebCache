using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Shoko.Models.WebCache;
using Shoko.WebCache.Database;
using Shoko.WebCache.Models;
using Shoko.WebCache.Models.Database;

namespace Shoko.WebCache.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class HashController : InjectedController
    {

        public HashController(IConfiguration cfg, WebCacheContext ctx, IMemoryCache mc) : base(cfg, ctx, mc)
        {
        }

        [HttpGet("CrossHash/{token}/{type}/{hash}/{size?}")]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        [Produces(typeof(WebCache_FileHash))]
        public async Task<IActionResult> GetHash(string token, int type, string hash, long? size)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            hash = hash.ToUpperInvariant();
            WebCache_FileHash h = null;
            switch ((WebCache_HashType)type)
            {
                case WebCache_HashType.ED2K:
                    h = await _db.WebCache_FileHashes.FirstOrDefaultAsync(a => a.ED2K == hash);
                    break;
                case WebCache_HashType.CRC:
                    if (size == null)
                        return StatusCode(400, "You must include size when asking for CRC");
                    h = await _db.WebCache_FileHashes.FirstOrDefaultAsync(a => a.CRC32 == hash && a.FileSize == size.Value);
                    break;
                case WebCache_HashType.MD5:
                    h = await _db.WebCache_FileHashes.FirstOrDefaultAsync(a => a.MD5 == hash);
                    break;
                case WebCache_HashType.SHA1:
                    h = await _db.WebCache_FileHashes.FirstOrDefaultAsync(a => a.SHA1 == hash);
                    break;
            }

            if (h == null)
                return StatusCode(404, "Hash not found");
            return new JsonResult(h);
        }

        private async Task<bool> InternalAddHash(SessionInfoWithError s, WebCache_FileHash hash)
        {
            bool update = false;
            if (string.IsNullOrEmpty(hash.ED2K) || string.IsNullOrEmpty(hash.CRC32) || string.IsNullOrEmpty(hash.MD5) || string.IsNullOrEmpty(hash.SHA1) || hash.FileSize==0)
                return false;
            hash.ED2K = hash.ED2K.ToUpperInvariant();
            hash.CRC32 = hash.CRC32.ToUpperInvariant();
            hash.MD5 = hash.MD5.ToUpperInvariant();
            hash.SHA1 = hash.SHA1.ToUpperInvariant();
            WebCache_FileHash_Info ed2k = await _db.WebCache_FileHashes.FirstOrDefaultAsync(a => a.ED2K == hash.ED2K);
            WebCache_FileHash_Info md5 = await _db.WebCache_FileHashes.FirstOrDefaultAsync(a => a.MD5 == hash.MD5);
            WebCache_FileHash_Info sha1 = await _db.WebCache_FileHashes.FirstOrDefaultAsync(a => a.SHA1 == hash.SHA1);
            WebCache_FileHash_Info orig = new WebCache_FileHash_Info();
            orig.FillWith(hash);
            orig.AniDBUserId = s.AniDBUserId;
            orig.CreationDate = DateTime.UtcNow;
            if (ed2k == null && md5 == null && sha1 == null) //Not CRC, we may get a normal collision in CRC
            {
                _db.Add(orig);
                update = true;
            }
            else
            {
                List<WebCache_FileHash_Info> collisions = new List<WebCache_FileHash_Info>();
                if (ed2k.CRC32 != hash.CRC32 || ed2k.FileSize != hash.FileSize || ed2k.SHA1 != hash.SHA1 || ed2k.MD5 != hash.MD5)
                {
                    collisions.Add(ed2k);
                }

                if (md5.CRC32 != hash.CRC32 || md5.FileSize != hash.FileSize || md5.SHA1 != hash.SHA1 || md5.ED2K != hash.ED2K)
                {
                    if (!collisions.Contains(md5))
                        collisions.Add(md5);
                }
                if (sha1.CRC32 != hash.CRC32 || sha1.FileSize != hash.FileSize || sha1.MD5 != hash.MD5 || sha1.ED2K != hash.ED2K)
                {
                    if (!collisions.Contains(sha1))
                        collisions.Add(sha1);
                }

                if (collisions.Count > 0)
                {
                    if (collisions.Any(b => b.CollisionApproved))
                        return false; //We already have the approved one, so this new one is wrong
                    collisions.Add(orig);
                    string unique = Guid.NewGuid().ToString().Replace("-", String.Empty);
                    _db.AddRange(collisions.Select(a => a.ToCollision(unique)));
                    update = true;
                }
            }
            return update;
        }


        [HttpPost("CrossHash/Batch/{token}")]
        [ProducesResponseType(403)]
        public async Task<IActionResult> AddHashes(string token, [FromBody] List<WebCache_FileHash> hashes)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            bool update = false;
            foreach (WebCache_FileHash hash in hashes)
            {
                if (await InternalAddHash(s, hash))
                    update = true;
            }
            if (update)
                await _db.SaveChangesAsync();
            return Ok();
        }
        [HttpPost("CrossHash/{token}")]
        [ProducesResponseType(403)]
        public async Task<IActionResult> AddHash(string token, [FromBody] WebCache_FileHash hash)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            if (await InternalAddHash(s, hash))
                await _db.SaveChangesAsync();
            return Ok();
        }
        [HttpGet("Collision/{token}")]
        [ProducesResponseType(403)]
        [Produces(typeof(List<WebCache_FileHash_Collision_Info>))]
        public async Task<IActionResult> GetCollisions(string token)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            if ((s.Role&WebCache_RoleType.Admin)==0)
                return StatusCode(403, "Admin Only");
            Dictionary<int,string> users=new Dictionary<int, string>();
            List<WebCache_FileHash_Collision> collisions = _db.WebCache_FileHash_Collisions.OrderBy(a=>a.WebCache_FileHash_Collision_Unique).ToList();
            List<WebCache_FileHash_Collision_Info> rets=new List<WebCache_FileHash_Collision_Info>();
            foreach (WebCache_FileHash_Collision c in collisions)
            {
                string uname = null;
                if (users.ContainsKey(c.AniDBUserId))
                    uname = users[c.AniDBUserId];
                else
                {
                    WebCache_User k = await _db.Users.FirstOrDefaultAsync(a => a.AniDBUserId == c.AniDBUserId);
                    if (k != null)
                    {
                        users.Add(c.AniDBUserId,k.AniDBUserName);
                        uname = k.AniDBUserName;
                    }
                }

                if (uname != null)
                {
                    rets.Add(c.ToCollisionInfo(uname));
                }                    
            }            
            return new JsonResult(rets);
        }
        [HttpPost("Collision/{token}/{id}")]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ApproveCollision(string token, int id)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            if ((s.Role & WebCache_RoleType.Admin) == 0)
                return StatusCode(403, "Admin Only");
            WebCache_FileHash_Collision approved = await _db.WebCache_FileHash_Collisions.FirstOrDefaultAsync(a => a.WebCache_FileHash_Collision_Id == id);
            if (approved == null)
                return StatusCode(404, "Collision Not Found");
            List<WebCache_FileHash_Collision> notapproved = await _db.WebCache_FileHash_Collisions.Where(a => a.WebCache_FileHash_Collision_Unique == approved.WebCache_FileHash_Collision_Unique && a.WebCache_FileHash_Collision_Id != id).ToListAsync();
            foreach (WebCache_FileHash_Collision n in notapproved)
            {
                WebCache_FileHash_Info fc = await _db.WebCache_FileHashes.FirstOrDefaultAsync(a => a.CRC32 == n.CRC32 && a.ED2K == n.ED2K && a.MD5 == n.MD5 && a.SHA1 == n.SHA1 && a.FileSize == n.FileSize);
                if (fc != null)
                {
                    _db.Remove(fc);
                    await _db.SaveChangesAsync();
                }
            }
            WebCache_FileHash_Info ap = await _db.WebCache_FileHashes.FirstOrDefaultAsync(a => a.CRC32 == approved.CRC32 && a.ED2K == approved.ED2K && a.MD5 == approved.MD5 && a.SHA1 == approved.SHA1 && a.FileSize == approved.FileSize);
            if (ap == null)
            {
                ap=new WebCache_FileHash_Info();
                ap.FillWith(approved);
                _db.Add(ap);
            }

            ap.AniDBUserId = s.AniDBUserId;
            ap.CollisionApproved = true;
            await _db.SaveChangesAsync();
            return Ok();
        }
    }


}
