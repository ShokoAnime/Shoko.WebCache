using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Shoko.Models.WebCache;
using Shoko.WebCache.Database;
using Shoko.WebCache.Models;
using Shoko.WebCache.Models.Database;

namespace Shoko.WebCache.Controllers
{
    public class InjectedController : ControllerBase
    {
        private static object _lock;
        internal IConfiguration _configuration;
        internal WebCacheContext _db;
        internal IMemoryCache _mc;

        public InjectedController(IConfiguration cfg, WebCacheContext ctx, IMemoryCache mc)
        {
            _configuration = cfg;
            _db = ctx;
            _mc = mc;
        }

        private InjectedController()
        {
        }

        public WebCache_RoleType GetRole(int AniDBUserId)
        {
            if (!_mc.TryGetValue("roles", out Dictionary<int, WebCache_Role> roles))
            {
                lock (_lock)
                {
                    roles = _db.Roles.ToDictionary(a => a.AniDBUserId, a => a);
                    _mc.Set("roles", roles, TimeSpan.FromSeconds(60));
                }
            }

            if (roles.ContainsKey(AniDBUserId))
                return roles[AniDBUserId].Type;
            return WebCache_RoleType.None;
        }

        public WebCache_Ban GetBan(int AniDBUserId)
        {
            if (!_mc.TryGetValue("bans", out Dictionary<int, WebCache_Ban> bans))
            {
                lock (_lock)
                {
                    bans = _db.Bans.ToDictionary(a => a.AniDBUserId, a => a);
                    _mc.Set("bans", bans, TimeSpan.FromSeconds(60));
                }
            }

            if (bans.ContainsKey(AniDBUserId))
                return bans[AniDBUserId];
            return null;
        }

        public void SetBan(int AniDBUserId, string reason, int hours)
        {
            lock (_lock)
            {
                WebCache_Ban b = _db.Bans.FirstOrDefault(a => a.AniDBUserId == AniDBUserId);
                if (b == null)
                {
                    b = new WebCache_Ban();
                    b.AniDBUserId = AniDBUserId;
                    _db.Add(b);
                }

                b.Reason = reason;
                b.ExpirationUTC = DateTime.UtcNow.AddHours(hours);
                _db.SaveChanges();
                Dictionary<int, WebCache_Ban> bans = _db.Bans.ToDictionary(a => a.AniDBUserId, a => a);
                _mc.Set("bans", bans, TimeSpan.FromSeconds(60));
            }
        }

        public async Task<SessionInfoWithError> VerifyTokenAsync(string token)
        {
            WebCache_Session s = await _db.Sessions.FirstOrDefaultAsync(a => a.Token == token);
            if (s == null)
                return new SessionInfoWithError {Error = StatusCode(403, "Invalid Token")};
            if (s.Expiration < DateTime.UtcNow)
            {
                _db.Remove(s);
                await _db.SaveChangesAsync();
                return new SessionInfoWithError {Error = StatusCode(403, "Invalid Token")};
            }

            s.Expiration = DateTime.UtcNow.AddHours(GetTokenExpirationInHours());
            await _db.SaveChangesAsync();
            WebCache_Ban b = GetBan(s.AniDBUserId);
            if (b != null)
                return new SessionInfoWithError {Error = StatusCode(403, "Banned: " + b.Reason + " Expiration:" + b.ExpirationUTC.ToLongDateString())};
            SessionInfoWithError si = new SessionInfoWithError {AniDBUserId = s.AniDBUserId, AniDBUserName = s.AniDBUserName, Expiration = s.Expiration, Token = s.Token};
            si.Role = GetRole(s.AniDBUserId);
            si.Error = null;
            return si;
        }

        public int GetTokenExpirationInHours()
        {
            return _configuration.GetValue("TokenExpirationInHours", 48);
        }

        public string GetAniDBUserVerificationUri()
        {
            return _configuration.GetValue("AniDBUserVerificationUri", "http://anidb.net/perl-bin/animedb.pl?show=userpage");
        }

        public string GetAniDBUserVerificationRegEx()
        {
            return _configuration.GetValue("AniDBUserVerificationRegEx", "g_odd\\sname.*?value.*?>(?<username>.*?)\\s+?\\((?<id>.*?)\\)");
        }

        public Dictionary<string, Credentials> GetOAuthProviders()
        {
            Dictionary<string, Credentials> creds = new Dictionary<string, Credentials>();
            try
            {
                _configuration.GetSection("OAuthProviders").Bind(creds);
            }
            catch (Exception e)
            {
            }

            return creds;
        }
    }
}