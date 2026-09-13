using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Domino.Catalog
{
    public sealed class CatalogCacheEntry
    {
        public string Json { get; }
        public DateTimeOffset DownloadedAt { get; }
        public CatalogCacheEntry(string json,DateTimeOffset at) { Json=json;DownloadedAt=at; }
    }
    public interface IGameCatalogCache
    {
        CatalogCacheEntry Read();
        void Write(CatalogCacheEntry entry);
    }
    public sealed class FileGameCatalogCache : IGameCatalogCache
    {
        readonly string path;
        public FileGameCatalogCache(string path) { this.path=Path.GetFullPath(path); }
        public CatalogCacheEntry Read()
        {
            if(!File.Exists(path))return null;
            if(new FileInfo(path).Length>GameCatalogCodec.MaxBytes*2)throw new FormatException("CACHE_SIZE");
            var o=JObject.Parse(File.ReadAllText(path,Encoding.UTF8));
            if(o["cacheSchemaVersion"]?.Type!=JTokenType.Integer||(int)o["cacheSchemaVersion"]!=1||o["json"]?.Type!=JTokenType.String||o["downloadedAt"]?.Type!=JTokenType.Integer)throw new FormatException("CACHE_SCHEMA");
            return new CatalogCacheEntry((string)o["json"],DateTimeOffset.FromUnixTimeSeconds((long)o["downloadedAt"]));
        }
        public void Write(CatalogCacheEntry entry)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                var text=new JObject { ["cacheSchemaVersion"]=1,["downloadedAt"]=entry.DownloadedAt.ToUnixTimeSeconds(),["json"]=entry.Json }.ToString();
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                    var bytes=Encoding.UTF8.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);
                }
                if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
            } finally { if(File.Exists(temp))File.Delete(temp); }
        }
    }
}
