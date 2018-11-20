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
using Shoko.Models.Plex.Login;
using Shoko.Models.WebCache;
using Shoko.WebCache.Database;
using Shoko.WebCache.Models;
using Shoko.WebCache.Models.Database;
using WebCache_Ban = Shoko.Models.WebCache.WebCache_Ban;

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
        [ProducesResponseType(400)]
        public async Task<IActionResult> Token(string code, string state, string error, string encoded)
        {
            WebCache_OAuthData enc;
            try
            {
                string normalbase64 = encoded.Replace("-", "+").Replace("_", "/"); //Convert BASE64URL to normal Base64
                int mod = normalbase64.Length % 4;
                if (mod == 2)
                    normalbase64 += "==";
                else if (mod == 3)
                    normalbase64 += "=";
                enc = JsonConvert.DeserializeObject<WebCache_OAuthData>(Encoding.UTF8.GetString(Convert.FromBase64String(normalbase64)));
                if (enc == null) //Bad encoded data, no way to redirect the error
                    return StatusCode(400, "Bad Request");
            }
            catch (Exception) //Bad encoded data, no way to redirect the error
            {
                return StatusCode(400, "Bad Request");
            }
            WebCache_OAuthAccessTokenWithState errtoken=new WebCache_OAuthAccessTokenWithState();
            SessionInfoWithError s = await VerifyTokenAsync(enc.Token);
            if (s.Error != null)
            {
                errtoken.error = s.Error.StatusCode + ": " + s.Error.ToString();
                return ReturnResult(enc, errtoken);
            }
            Dictionary<string, Credentials> providers = GetOAuthProviders();
            if (!providers.ContainsKey(enc.Provider))
            {
                errtoken.error = $"404: Provider {enc.Provider} Not Found";
                return ReturnResult(enc, errtoken);

            }
            Credentials credentials = providers[enc.Provider];
            var token = await GetTokenAsync(code, state, credentials, enc.OriginalRedirectUri);
            if (token == null)
            {
                errtoken.error = "400: Bad Request";
                return ReturnResult(enc, errtoken);
            }
            return ReturnResult(enc, token);
         }

        private IActionResult ReturnResult(WebCache_OAuthData enc, WebCache_OAuthAccessTokenWithState token)
        {
            //If the encoded data has a redirect back, lets redirect the page,
            //otherwise return a web page, with the access token in a head meta tag
            if (enc.RedirectUri != null) 
            {
                string separator = "?";
                if (enc.RedirectUri.Contains("?"))
                    separator = "&";
                return Redirect(enc.RedirectUri + separator + token.GetQueryString());
            }
            string json = JsonConvert.SerializeObject(token, Formatting.None).Replace("\r", "").Replace("\n", "");
            return new ContentResult { ContentType = "text/html", StatusCode = (int)HttpStatusCode.OK, Content = "<html><head><meta name=\"AccessToken\" content=\"" + HttpUtility.HtmlEncode(json) + "\"/></head></html>" };
        }


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
        [ProducesResponseType(403)]
        [Produces(typeof(WebCache_SessionInfo))]
        public async Task<IActionResult> RefreshSession(string token)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token,true);
            if (s.Error != null)
                return s.Error;
            return new JsonResult(s);
        }
        [HttpPost("Ban/{token}")]
        [ProducesResponseType(403)]
        public async Task<IActionResult> Ban(string token, Shoko.Models.WebCache.WebCache_Ban ban)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            if ((s.Role & WebCache_RoleType.Admin) == 0)
                return StatusCode(403, "Admin Only");
            SetBan(ban.AniDBUserId,ban.Reason,ban.Hours);
            return Ok();
        }
        [HttpPost("SetRole/{token}/{anidbuserid}/{role}")]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> SetRole(string token, int anidbuserid, int role)
        {
            SessionInfoWithError s = await VerifyTokenAsync(token);
            if (s.Error != null)
                return s.Error;
            if ((s.Role & WebCache_RoleType.Admin) == 0)
                return StatusCode(403, "Admin Only");
            WebCache_User us = await _db.Users.FirstOrDefaultAsync(a => a.AniDBUserId == anidbuserid);
            if (us==null)
                return StatusCode(404, "User not found");
            WebCache_RoleType rt = (WebCache_RoleType) role;
            SetRole(anidbuserid, rt);
            return Ok();
        }
        [HttpPost("AniDB")]
        [ProducesResponseType(403)]
        [Produces(typeof(WebCache_SessionInfo))]
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