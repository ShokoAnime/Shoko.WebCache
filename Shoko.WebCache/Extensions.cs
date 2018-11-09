using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shoko.Models.WebCache;
using Shoko.WebCache.Models.Database;
using Shoko.WebCache.Models.Shared;

namespace Shoko.WebCache
{
    public static class Extensions
    {
        public static string GetQueryString(this WebCache_OAuthAccessTokenWithState at) => "access_token=" + HttpUtility.UrlEncode(at.access_token) + "&token_type=" + HttpUtility.UrlEncode(at.token_type) + "&expires_in=" + HttpUtility.UrlEncode(at.expires_in.ToString()) + "&state=" + HttpUtility.UrlEncode(at.state);



    }
}
