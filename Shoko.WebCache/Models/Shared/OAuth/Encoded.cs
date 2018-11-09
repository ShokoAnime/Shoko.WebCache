namespace Shoko.WebCache.Models.Shared.OAuth
{
    public class Encoded
    {
        public string OriginalRedirectUri { get; set; }

        public string Provider { get; set; }
        public string RedirectUri { get; set; }
    }
}