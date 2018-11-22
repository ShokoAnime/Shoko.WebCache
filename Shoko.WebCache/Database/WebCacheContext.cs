using Microsoft.EntityFrameworkCore;
using Shoko.Models.WebCache;
using Shoko.WebCache.Models.Database;
using WebCache_Ban = Shoko.WebCache.Models.Database.WebCache_Ban;
using WebCache_CrossRef_AniDB_Provider = Shoko.WebCache.Models.Database.WebCache_CrossRef_AniDB_Provider;
using WebCache_CrossRef_File_Episode = Shoko.WebCache.Models.Database.WebCache_CrossRef_File_Episode;
using WebCache_Media = Shoko.WebCache.Models.Database.WebCache_Media;

namespace Shoko.WebCache.Database
{
    public class WebCacheContext : DbContext
    {
        public WebCacheContext(DbContextOptions<WebCacheContext> options) : base(options)
        {

        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            {
                var model = builder.Entity<WebCache_Session>();
                model.ToTable("Sessions").HasKey(x => x.Token);
                model.Property(x => x.Token).IsRequired().ValueGeneratedNever().HasMaxLength(40);
                model.Property(x => x.Expiration).IsRequired();
                model.Property(x => x.AniDBUserId).IsRequired();
                model.Property(x => x.AniDBUserName).IsRequired();
                model.HasIndex(x => x.AniDBUserId).HasName("IX_Sessions_AniDBUserId");
            }
            {
                var model = builder.Entity<WebCache_User>();
                model.ToTable("Users").HasKey(x => x.AniDBUserId);
                model.Property(x => x.AniDBUserName).IsRequired();
                model.Property(x => x.AniDBUserId).IsRequired().ValueGeneratedNever();
            }
            {
                var model = builder.Entity<WebCache_Role>();
                model.ToTable("Roles").HasKey(x => x.AniDBUserId);
                model.Property(x => x.Type).IsRequired();
                model.Property(x => x.AniDBUserId).IsRequired().ValueGeneratedNever();
            }
            {
                var model = builder.Entity<WebCache_Ban>();
                model.ToTable("Bans").HasKey(x => x.AniDBUserId);
                model.Property(x => x.ExpirationUTC).IsRequired();
                model.Property(x => x.AniDBUserId).IsRequired().ValueGeneratedNever();
            }
            {
                var model = builder.Entity<WebCache_CrossRef_AniDB_Provider>();
                model.ToTable("CrossRef_AniDB_Providers").HasKey(x => x.WebCache_AniDB_ProviderID);
                model.HasIndex(x => new {x.AnimeID, x.CrossRefType, x.Approved, x.AniDBUserId}).HasName("IX_CrossRef_AniDB_Provider_AnimeID_CrossRefType");
                model.Property(x => x.WebCache_AniDB_ProviderID).ValueGeneratedOnAdd();
                model.Property(x=>x.AnimeID).IsRequired();
                model.Property(x => x.AniDBUserId).IsRequired();
                model.Property(x=>x.CrossRefID).IsRequired();
                model.Property(x=>x.Approved).IsRequired();
                model.Property(x=>x.CrossRefSource).IsRequired();
                model.Property(x=>x.CrossRefType).IsRequired();
            }
            {
                var model = builder.Entity<WebCache_CrossRef_File_Episode>();
                model.ToTable("CrossRef_File_Episodes").HasKey(x => x.CrossRef_File_EpisodeID);
                model.HasIndex(x => new { x.AniDBUserId, x.Hash, x.EpisodeID}).HasName("IX_CrossRef_File_Episodes_AniDBUserId_Hash");
                model.Property(x => x.CrossRef_File_EpisodeID).ValueGeneratedOnAdd();
                model.Property(x => x.AnimeID).IsRequired();
                model.Property(x => x.AniDBUserId).IsRequired();
                model.Property(x => x.Hash).IsRequired().HasMaxLength(32);
                model.Property(x => x.CrossRefSource).IsRequired();
                model.Property(x => x.EpisodeID).IsRequired();
                model.Property(x => x.EpisodeOrder).IsRequired();
            }
            {
                var model = builder.Entity<WebCache_FileHash_Info>();
                model.ToTable("FileHashes").HasKey(x => x.ED2K);
                model.HasIndex(x => x.MD5).HasName("IX_FileHashes_MD5");
                model.HasIndex(x => x.SHA1).HasName("IX_FileHashes_SHA1");
                model.HasIndex(x => new { x.CRC32, x.FileSize }).HasName("IX_FileHashes_CRC32_FileSize");
                model.Property(x => x.ED2K).IsRequired().HasMaxLength(32).ValueGeneratedNever();
                model.Property(x => x.MD5).IsRequired().HasMaxLength(32);
                model.Property(x => x.SHA1).IsRequired().HasMaxLength(40);
                model.Property(x => x.CRC32).IsRequired().HasMaxLength(4);
                model.Property(x => x.FileSize).IsRequired();
                model.Property(x => x.CollisionApproved).IsRequired();
                model.Property(x => x.AniDBUserId).IsRequired();
                model.Property(x => x.CreationDate).IsRequired();
            }
            {
                var model = builder.Entity<WebCache_FileHash_Collision>();
                model.ToTable("FileHash_Collisions").HasKey(x => x.WebCache_FileHash_Collision_Id);
                model.HasIndex(x => x.WebCache_FileHash_Collision_Unique).HasName("IX_FileHash_Collisions_Unique");
                model.Property(x => x.WebCache_FileHash_Collision_Id).ValueGeneratedOnAdd();
                model.Property(x => x.ED2K).IsRequired().HasMaxLength(32);
                model.Property(x => x.MD5).IsRequired().HasMaxLength(32);
                model.Property(x => x.SHA1).IsRequired().HasMaxLength(40);
                model.Property(x => x.CRC32).IsRequired().HasMaxLength(4);
                model.Property(x => x.FileSize).IsRequired();
                model.Property(x => x.CollisionApproved).IsRequired();
                model.Property(x => x.AniDBUserId).IsRequired();
                model.Property(x => x.CreationDate).IsRequired();                
            }
            {
                var model = builder.Entity<WebCache_Media>();
                model.ToTable("Medias").HasKey(x => x.ED2K);
                model.Property(x => x.AniDBUserId).IsRequired();
                model.Property(x => x.CreationDate).IsRequired();
                model.Property(x => x.ED2K).IsRequired().HasMaxLength(32);
                model.Property(x => x.MediaInfo).IsRequired();
                model.Property(x => x.Version).IsRequired();
            }

            base.OnModelCreating(builder);
        }
        public DbSet<WebCache_Session> Sessions { get; set; }
        public DbSet<WebCache_User> Users { get; set; }
        public DbSet<WebCache_Role> Roles { get; set; }
        public DbSet<WebCache_Ban> Bans { get; set; }
        public DbSet<WebCache_CrossRef_AniDB_Provider> CrossRef_AniDB_Providers { get; set; }
        public DbSet<WebCache_CrossRef_File_Episode> CrossRef_File_Episodes { get; set; }
        public DbSet<WebCache_FileHash_Info> WebCache_FileHashes { get; set; }
        public DbSet<WebCache_FileHash_Collision> WebCache_FileHash_Collisions { get; set; }
        public DbSet<WebCache_Media> WebCache_Medias { get; set; }

    }
}
