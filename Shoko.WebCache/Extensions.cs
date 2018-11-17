using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shoko.Models.Server;
using Shoko.Models.Server.CrossRef;
using Shoko.Models.WebCache;
using Shoko.WebCache.Controllers;
using Shoko.WebCache.Models.Database;

namespace Shoko.WebCache
{
    public static class Extensions
    {
        public static string GetQueryString(this WebCache_OAuthAccessTokenWithState at) => "access_token=" + HttpUtility.UrlEncode(at.access_token) + "&token_type=" + HttpUtility.UrlEncode(at.token_type) + "&expires_in=" + HttpUtility.UrlEncode(at.expires_in.ToString()) + "&state=" + HttpUtility.UrlEncode(at.state);


        public static void FillWith(this CrossRef_AniDB_Provider prov, CrossRef_AniDB_Provider origin)
        {
            prov.CrossRefID = origin.CrossRefID;
            prov.AnimeID = origin.AnimeID;
            prov.CrossRefSource = origin.CrossRefSource;
            prov.CrossRefType = origin.CrossRefType;
            prov.EpisodesData = origin.EpisodesData;
            prov.EpisodesOverrideData = origin.EpisodesOverrideData;
        }
        public static void FillWith(this CrossRef_File_Episode prov, CrossRef_File_Episode origin)
        {
            prov.EpisodeID = origin.EpisodeID;
            prov.AnimeID = origin.AnimeID;
            prov.CrossRefSource = origin.CrossRefSource;
            prov.EpisodeOrder = origin.EpisodeOrder;
            prov.FileName = origin.FileName;
            prov.Hash = origin.Hash;
            prov.FileSize = origin.FileSize;
            prov.Percentage = origin.Percentage;
        }

        public static void FillWith(this WebCache_FileHash prov, WebCache_FileHash origin)
        {
            prov.CRC32 = origin.CRC32;
            prov.ED2K = origin.ED2K;
            prov.FileSize = origin.FileSize;
            prov.MD5 = origin.MD5;
            prov.SHA1 = origin.SHA1;
        }
        public static WebCache_FileHash_Collision ToCollision(this WebCache_FileHash_Info prov, string unique)
        {
            WebCache_FileHash_Collision col=new WebCache_FileHash_Collision();
            col.AniDBUserId = prov.AniDBUserId;
            col.CRC32 = prov.CRC32;
            col.ED2K = prov.ED2K;
            col.FileSize = prov.FileSize;
            col.MD5 = prov.MD5;
            col.SHA1 = prov.SHA1;
            col.CreationDate = prov.CreationDate;
            col.WebCache_FileHash_Collision_Unique = unique;
            return col;
        }
        public static WebCache_FileHash_Collision_Info ToCollisionInfo(this WebCache_FileHash_Collision prov, string username)
        {
            WebCache_FileHash_Collision_Info col = new WebCache_FileHash_Collision_Info();
            col.AniDBUserId = prov.AniDBUserId;
            col.CRC32 = prov.CRC32;
            col.ED2K = prov.ED2K;
            col.FileSize = prov.FileSize;
            col.MD5 = prov.MD5;
            col.SHA1 = prov.SHA1;
            col.CreationDate = prov.CreationDate;
            col.WebCache_FileHash_Collision_Unique = prov.WebCache_FileHash_Collision_Unique;
            col.WebCache_FileHash_Collision_Id = prov.WebCache_FileHash_Collision_Id;
            col.AniDBUserName = username;
            return col;
        }
    }
}
