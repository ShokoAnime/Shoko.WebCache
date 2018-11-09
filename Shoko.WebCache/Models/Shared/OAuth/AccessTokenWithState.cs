namespace Shoko.WebCache.Models.Shared.OAuth
{
    public class AccessTokenWithState : AccessToken
    {
        public string state { get; set; }

    }
}
