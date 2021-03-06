﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shoko.Models.WebCache;
using Shoko.WebCache.Database;
using Shoko.WebCache.Models;
using Shoko.WebCache.Models.Database;
using WebCache_Ban = Shoko.WebCache.Models.Database.WebCache_Ban;

// ReSharper disable InconsistentlySynchronizedField

namespace Shoko.WebCache.Controllers
{
    public class InjectedController : ControllerBase
    {
        private static readonly object _lock = new object();
        internal readonly IConfiguration _configuration;
        internal readonly WebCacheContext _db;
        internal readonly ILogger _logger;
        internal readonly IMemoryCache _mc;

        public InjectedController(IConfiguration cfg, WebCacheContext ctx, IMemoryCache mc, ILogger logger)
        {
            _configuration = cfg;
            _db = ctx;
            _mc = mc;
            _logger = logger;
        }



        internal WebCache_RoleType GetRole(int AniDBUserId)
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

        internal void SetRole(int AniDBUserId, WebCache_RoleType rt)
        {
            lock (_lock)
            {
                WebCache_Role b = _db.Roles.FirstOrDefault(a => a.AniDBUserId == AniDBUserId);
                if (rt == WebCache_RoleType.None)
                {
                    //Kill Role
                    if (b != null)
                        _db.Remove(b);
                }
                else if (b == null)
                {
                    //Create rtole
                    b = new WebCache_Role();
                    b.AniDBUserId = AniDBUserId;
                    _db.Add(b);
                }
                else
                {
                    //Update role
                    b.Type = rt;
                }

                _db.SaveChanges();
                Dictionary<int, WebCache_Role> roles = _db.Roles.ToDictionary(a => a.AniDBUserId, a => a);
                _mc.Set("roles", roles, TimeSpan.FromSeconds(60));
            }
        }

        internal WebCache_Ban GetBan(int AniDBUserId)
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

        internal void SetBan(int AniDBUserId, string reason, int hours)
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

        internal async Task<SessionInfoWithError> VerifyTokenAsync(string token, bool force = false)
        {
            WebCache_Session s = await _db.Sessions.FirstOrDefaultAsync(a => a.Token == token);
            if (s == null)
                return new SessionInfoWithError {Error = StatusCode(403, "Invalid Token")};
            if (s.Expiration < DateTime.UtcNow)
            {
                //Lets reuse this call to kill em all, and do some database cleaning.
                _db.RemoveRange(_db.Sessions.Where(a => a.Expiration < DateTime.UtcNow));
                await _db.SaveChangesAsync();
                return new SessionInfoWithError {Error = StatusCode(403, "Invalid Token")};
            }

            if (s.Expiration.AddHours(-8) < DateTime.UtcNow || force) //Refresh Expiration if we have less than 8 hours left
            {
                s.Expiration = DateTime.UtcNow.AddHours(GetTokenExpirationInHours());
                await _db.SaveChangesAsync();
            }

            WebCache_Ban b = GetBan(s.AniDBUserId);
            if (b != null)
                return new SessionInfoWithError {Error = StatusCode(403, "Banned: " + b.Reason + " Expiration:" + b.ExpirationUTC.ToLongDateString())};
            SessionInfoWithError si = new SessionInfoWithError {AniDBUserId = s.AniDBUserId, AniDBUserName = s.AniDBUserName, Expiration = s.Expiration, Token = s.Token};
            si.Role = GetRole(s.AniDBUserId);
            si.Error = null;
            return si;
        }

        internal int GetTokenExpirationInHours()
        {
            return _configuration.GetValue("TokenExpirationInHours", 48);
        }

        internal string GetAniDBUserVerificationUri()
        {
            return _configuration.GetValue("AniDBUserVerificationUri", "http://anidb.net/perl-bin/animedb.pl?show=userpage");
        }

        internal string GetAniDBLogoutUri()
        {
            return _configuration.GetValue("AniDBLogoutUri", "http://anidb.net/perl-bin/animedb.pl?show=main&xdo.logout=1");
        }

        internal string GetAniDBUserVerificationRegEx()
        {
            return _configuration.GetValue("AniDBUserVerificationRegEx", "g_odd\\sname.*?value.*?>(?<username>.*?)\\s+?\\((?<id>.*?)\\)");
        }

        internal Dictionary<string, Credentials> GetOAuthProviders()
        {
            Dictionary<string, Credentials> creds = new Dictionary<string, Credentials>();
            try
            {
                _configuration.GetSection("OAuthProviders").Bind(creds);
            }
            catch (Exception)
            {
                //ignore
            }

            return creds;
        }
    }
}