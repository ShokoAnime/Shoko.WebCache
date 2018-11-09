using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Shoko.Models.Plex.Login;
using Shoko.WebCache.Models.Database;
using Shoko.WebCache.Models.Shared;

namespace Shoko.WebCache.Controllers
{
    public class InjectedController : ControllerBase
    {
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

        public RoleType GetRole(int AniDBUserId)
        {
            if (!_mc.TryGetValue("roles", out Dictionary<int, Role> roles))
            {
                roles = _db.Roles.ToDictionary(a => a.AniDBUserId, a => a);
                _mc.Set("roles", roles, TimeSpan.FromSeconds(60));
            }
            if (roles.ContainsKey(AniDBUserId))
                return roles[AniDBUserId].Role;
            return RoleType.None;
        }

        public async Task<Session> VerifyTokenAsync(string token)
        {
            Session s = await _db.Sessions.FirstOrDefaultAsync(a => a.Token == token);
            if (s == null)
                return null;
            if (s.Expiration < DateTime.UtcNow)
            {
                _db.Remove(s);
                await _db.SaveChangesAsync();
                return null;
            }
            s.Expiration = DateTime.UtcNow.AddHours(GetTokenExpirationInHours());
            await _db.SaveChangesAsync();
            return s;
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
            Dictionary<string, Credentials> creds=new Dictionary<string, Credentials>();
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
