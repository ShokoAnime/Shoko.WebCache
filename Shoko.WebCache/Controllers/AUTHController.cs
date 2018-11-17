using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Shoko.Models.WebCache;
using Shoko.WebCache.Database;
using Shoko.WebCache.Models;
using Shoko.WebCache.Models.Database;

namespace Shoko.WebCache.Controllers
{
    [Route("[Controller]")]
    [ApiController]
    public class AuthController : InjectedController
    {
        public AuthController(IConfiguration cfg, WebCacheContext ctx, IMemoryCache mc) : base(cfg, ctx, mc)
        {
        }

        [HttpGet("OAuthToken/{encoded}")]
        public async Task<IActionResult> TokenAsync(string code, string state, string encoded)
        {
            WebCache_OAuthData enc;
            try
            {
                string normalbase64 = encoded.Replace("-", "+").Replace("_", "/");
                int mod = normalbase64.Length % 4;
                if (mod == 2)
                    normalbase64 += "==";
                else if (mod == 3)
                    normalbase64 += "=";
                enc = JsonConvert.DeserializeObject<WebCache_OAuthData>(Encoding.UTF8.GetString(Convert.FromBase64String(normalbase64)));
                if (enc == null)
                    return StatusCode(400, "Bad Request");
            }
            catch (Exception)
            {
                return StatusCode(400, "Bad Request");
            }

            SessionInfoWithError s = await VerifyTokenAsync(enc.Token);
            if (s.Error!=null)
                return s.Error;
            Dictionary<string, Credentials> providers = GetOAuthProviders();
            if (!providers.ContainsKey(enc.Provider))
                return StatusCode(404, $"Provider {enc.Provider} Not Found");
            Credentials credentials = providers[enc.Provider];
            WebCache_OAuthAccessTokenWithState token = await GetTokenAsync(code, state, credentials, enc.OriginalRedirectUri);
            if (token == null)
                return StatusCode(400, "Bad Request");
            if (enc.RedirectUri != null)
            {
                string separator = "?";
                if (enc.RedirectUri.Contains("?"))
                    separator = "&";
                return Redirect(enc.RedirectUri + separator + token.GetQueryString());
            }

            string json = JsonConvert.SerializeObject(token, Formatting.None).Replace("\r", "").Replace("\n", "");
            return new ContentResult {ContentType = "text/html", StatusCode = (int) HttpStatusCode.OK, Content = "<html><head><meta name=\"AccessToken\" content=\"" + HttpUtility.HtmlEncode(json) + "\"/></head></html>"};
        }

        [HttpGet("Token/{encoded}")]
        private async Task<WebCache_OAuthAccessTokenWithState> GetTokenAsync(string code, string state, Credentials credentials, string redirecturi)
        {
            Dictionary<string, string> postdata = new Dictionary<string, string>();
            postdata.Add("grant_type", "authorization_code");
            postdata.Add("code", code);
            postdata.Add("client_id", credentials.Id);
            postdata.Add("client_secret", credentials.Secret);
            postdata.Add("redirect_uri", redirecturi);

            using (var client = new HttpClient())
            {
                string accept = "application/json";
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, credentials.TokenUri);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
                request.Content = new FormUrlEncodedContent(postdata);
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                WebCache_OAuthAccessTokenWithState at = JsonConvert.DeserializeObject<WebCache_OAuthAccessTokenWithState>(await response.Content.ReadAsStringAsync());
                at.state = state;
                return at;
            }
        }

        [HttpGet("RefreshToken/{token}")]
        public async Task<IActionResult> RefreshSession(string token)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            return new JsonResult(s);
        }

        [HttpPost("AniDB")]
        public async Task<IActionResult> Verify(WebCache_AniDBLoggedInfo data)
        {
            CookieContainer cookieContainer = new CookieContainer();
            using (var handler = new HttpClientHandler {CookieContainer = cookieContainer})
            using (var client = new HttpClient(handler))
            {
                string curi = GetAniDBUserVerificationUri();
                string regex = GetAniDBUserVerificationRegEx();
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36 Edge/16.16241");
                Uri uri = new Uri(curi);
                Regex rn = new Regex(regex, RegexOptions.Singleline);
                foreach (string k in data.Cookies.Keys)
                    cookieContainer.Add(new Cookie(k, data.Cookies[k], "/", uri.Host));
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
                HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                if (response.IsSuccessStatusCode)
                {
                    string str = await response.Content.ReadAsStringAsync();
                    Match m = rn.Match(str);
                    if (m.Success)
                    {
                        if (m.Groups.Count > 1)
                        {
                            string val = m.Groups["username"]?.Value;
                            string id = m.Groups["id"]?.Value;
                            int aniid;
                            if (val != null && id != null && int.TryParse(id, out aniid))
                            {
                                if (string.Compare(val, data.UserName, StringComparison.InvariantCultureIgnoreCase) == 0)
                                {
                                    WebCache_User u = await _db.Users.FirstOrDefaultAsync(a => a.AniDBUserId == aniid);
                                    if (u == null)
                                    {
                                        u=new WebCache_User();
                                        u.AniDBUserId = aniid;
                                        u.AniDBUserName = val;
                                        _db.Add(u);
                                    }
                                    else if (u.AniDBUserName != val)
                                    {
                                        u.AniDBUserName = val;
                                    }
                                    WebCache_Session s = new WebCache_Session();
                                    s.Token = Guid.NewGuid().ToString().Replace("-", string.Empty);
                                    s.Expiration = DateTime.UtcNow.AddHours(GetTokenExpirationInHours());
                                    s.AniDBUserName = val;
                                    s.AniDBUserId = aniid;
                                    _db.Add(s);
                                    await _db.SaveChangesAsync();
                                    SessionInfoWithError si = new SessionInfoWithError { AniDBUserId = s.AniDBUserId, AniDBUserName = s.AniDBUserName, Expiration = s.Expiration, Token = s.Token };
                                    si.Role = GetRole(s.AniDBUserId);
                                    si.Error = null;
                                    return new JsonResult(s);

                                }
                            }
                        }
                    }
                }
            }

            return StatusCode(403, "Invalid credentials");
        }
    }
}