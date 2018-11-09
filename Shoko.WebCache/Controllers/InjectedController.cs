using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shoko.WebCache.Models.Database;
using Shoko.WebCache.Models.Shared;

namespace Shoko.WebCache.Controllers
{
    public class InjectedController : ControllerBase
    {
        internal IConfiguration _configuration;
        internal WebCacheContext _db;

        public InjectedController(IConfiguration Configuration, WebCacheContext Context)
        {
            _configuration = Configuration;
            _db = Context;
        }

        private InjectedController()
        {

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
            return _configuration.GetValue("AniDBUserVerificationRegEx", "<title>My\\sUserPage\\sof\\s(.*?)\\s-\\s(<username>.*?)\\s-\\sAniDB</title>");
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
