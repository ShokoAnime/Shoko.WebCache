using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Shoko.WebCache.Models.Database;
using Shoko.WebCache.Models.Shared;
using Shoko.WebCache.Models.Shared.OAuth;

namespace Shoko.WebCache.Controllers
{
    [Route("Auth")]
    public class AuthController : ControllerBase
    {
        private IConfiguration _configuration;
        private WebCacheContext _db;

        public AuthController(IConfiguration Configuration, WebCacheContext Context)
        {
            _configuration = Configuration;
            _db = Context;
        }

        [HttpGet("OAuthToken/{encoded}")]
        public async Task<IActionResult> TokenAsync(string code, string state, string encoded)
        {
            Encoded enc;
            try
            {
                string normalbase64 = encoded.Replace("-", "+").Replace("_", "/");
                int mod = normalbase64.Length % 4;
                if (mod == 2)
                    normalbase64 += "==";
                else if (mod == 3)
                    normalbase64 += "=";
                enc = JsonConvert.DeserializeObject<Encoded>(Encoding.UTF8.GetString(Convert.FromBase64String(normalbase64)));
                if (enc == null)
                    return StatusCode(400, "Bad Request");
            }
            catch (Exception)
            {
                return StatusCode(400, "Bad Request");
            }
            Dictionary<string, Credentials> providers = new Dictionary<string, Credentials>();
            _configuration.GetSection("OAuthProviders").Bind(providers);
            if (!providers.ContainsKey(enc.Provider))
                return StatusCode(404, $"Provider {enc.Provider} Not Found");
            Credentials credentials = providers.GetValueOrDefault(enc.Provider,null);
            AccessTokenWithState token = await GetTokenAsync(code, state, credentials, enc.OriginalRedirectUri);
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
            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = (int) HttpStatusCode.OK,
                Content = "<html><head><meta name=\"AccessToken\" content=\"" + HttpUtility.HtmlEncode(json) + "\"/></head></html>"
            };
        }

        [HttpGet("Token/{encoded}")]
        private async Task<AccessTokenWithState> GetTokenAsync(string code, string state, Credentials credentials, string redirecturi)
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
                AccessTokenWithState at = JsonConvert.DeserializeObject<AccessTokenWithState>(await response.Content.ReadAsStringAsync());
                at.state = state;
                return at;
            }
        }

        [HttpGet("RefreshToken/{token}")]
        public async Task<IActionResult> RefreshSession(string token)
        {
            Session s = await _db.RefreshTokenAsync(token, _configuration.GetValue<int>("TokenExpirationInHours"));
            if (s==null)
                return StatusCode(403, "Token expired");
            return new JsonResult(s);
        }

        [HttpPost("AniDB")]
        public async Task<IActionResult> Verify(AniDBLoggedInfo data)
        {
            CookieContainer cookieContainer = new CookieContainer();
            using (var handler = new HttpClientHandler { CookieContainer = cookieContainer })
            using (var client = new HttpClient(handler))
            {
                string curi = _configuration.GetValue<string>("AniDBUserVerificationUri");
                string regex = _configuration.GetValue<string>("AniDBUserVerificationRegEx");
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
                            if (val != null)
                            {
                                if (string.Compare(val, data.UserName, StringComparison.InvariantCultureIgnoreCase) == 0)
                                {
                                    Session s=new Session();
                                    s.Token = Guid.NewGuid().ToString().Replace("-", string.Empty);
                                    s.Expiration=DateTime.UtcNow.AddHours(_configuration.GetValue<int>("TokenExpirationInHours"));
                                    s.AniDBUserName = data.UserName;
                                    _db.Add(s);
                                    await _db.SaveChangesAsync();
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
