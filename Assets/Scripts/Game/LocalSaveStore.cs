using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ArrowCube.Gameplay;
using UnityEngine;

namespace ArrowCube.Game
{
    /// <summary>Checksummed offline saves, atomic replacement, and a last-good backup.</summary>
    public sealed class LocalSaveStore
    {
        [Serializable] private sealed class Envelope { public int Version=1; public string Payload,Checksum; }
        public string Path { get; }
        private bool preserveBackup;
        public LocalSaveStore(string path) { Path=System.IO.Path.GetFullPath(path); }
        public void Save(PlayerSave save)
        {
            save.HasActive=save.Active!=null; save.Validate();
            string payload=JsonUtility.ToJson(save);
            string text=JsonUtility.ToJson(new Envelope { Payload=payload,Checksum=Hash(payload) });
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            string temporary=Path+".tmp";
            using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            {
                var bytes=Encoding.UTF8.GetBytes(text); file.Write(bytes,0,bytes.Length); file.Flush(true);
            }
            if(!File.Exists(Path)) File.Move(temporary,Path);
            else
            {
                try { File.Replace(temporary,Path,preserveBackup ? null : Path+".bak"); }
                catch(PlatformNotSupportedException)
                { if(!preserveBackup) File.Copy(Path,Path+".bak",true); File.Copy(temporary,Path,true); File.Delete(temporary); }
            }
            preserveBackup=false;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Unity 6's default web template requires an explicit IndexedDB sync.
            // The engine coalesces requests; desktop filesystem behavior is unchanged.
            JS_FileSystem_Sync();
#endif
        }
        public PlayerSave Load(out string warning)
        {
            warning="";
            if(TryRead(Path,out var save,out var error)) return save;
            if(TryRead(Path+".bak",out save,out _)) { preserveBackup=true; warning="Recovered your last good local save from backup."; return save; }
            if(File.Exists(Path) || File.Exists(Path+".bak")) warning="The local save could not be loaded. Your existing files were kept. "+error;
            return null;
        }
        private static bool TryRead(string path,out PlayerSave save,out string error)
        {
            save=null; error="";
            if(!File.Exists(path)) return false;
            try
            {
                if(new FileInfo(path).Length>32*1024*1024) throw new InvalidDataException("Save exceeds the supported size.");
                var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
                if(envelope==null || envelope.Version!=1 || envelope.Payload==null || Hash(envelope.Payload)!=envelope.Checksum)
                    throw new InvalidDataException("Save checksum or version is invalid.");
                var candidate=JsonUtility.FromJson<PlayerSave>(envelope.Payload);
                if(candidate==null) throw new InvalidDataException("Save is empty.");
                if(!candidate.HasActive) candidate.Active=null;
                candidate.Validate(); candidate.Active?.Restore(); save=candidate; return true;
            }
            catch(Exception exception) when(exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException || exception is InvalidOperationException || exception is FormatException)
            { error=exception.Message; return false; }
        }
        private static string Hash(string text)
        {
            using(var hash=SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void JS_FileSystem_Sync();
#endif
    }
}
