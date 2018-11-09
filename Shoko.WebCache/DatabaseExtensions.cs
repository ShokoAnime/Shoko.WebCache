using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.EntityFrameworkCore;
using Shoko.WebCache.Models.Database;
using Shoko.WebCache.Models.Shared.OAuth;

namespace Shoko.WebCache
{
    public static class DatabaseExtensions
    {
        public static string GetQueryString(this AccessTokenWithState at) => "access_token=" + HttpUtility.UrlEncode(at.access_token) + "&token_type=" + HttpUtility.UrlEncode(at.token_type) + "&expires_in=" + HttpUtility.UrlEncode(at.expires_in.ToString()) + "&state=" + HttpUtility.UrlEncode(at.state);


        public static async Task<Session> RefreshTokenAsync(this WebCacheContext context, string token, int hours)
        {
            Session s = await context.Sessions.FirstOrDefaultAsync(a => a.Token == token);
            if (s == null)
                return null;
            if (s.Expiration < DateTime.UtcNow)
            {
                context.Remove(s);
                await context.SaveChangesAsync();
                return null;
            }
            s.Expiration = DateTime.UtcNow.AddHours(hours);
            await context.SaveChangesAsync();
            return s;
        }


    }
}
