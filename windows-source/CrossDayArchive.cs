using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FocusStar
{
    internal sealed class ArchiveRunResult
    {
        public readonly List<string> DeletedFiles = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool Changed;
    }

    internal static class CrossDayArchive
    {
        internal const string Header = "timestamp,state,reason,idle_seconds,foreground_process,sample_seconds,platform,device_id,start_epoch,end_epoch";
        internal static Action BeforeCommitForTests;
        private static readonly DateTime Epoch = new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);

        private sealed class Snapshot
        {
            public string Path;
            public byte[] Bytes;
            public string Hash;
        }

        private static bool Number(string value, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && !double.IsNaN(result) && !double.IsInfinity(result);
        }

        private static Snapshot ReadSnapshot(string path)
        {
            byte[] bytes;
            using (var input = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using (var memory = new MemoryStream()) { input.CopyTo(memory); bytes=memory.ToArray(); }
            using (var sha = SHA256.Create()) return new Snapshot { Path=path,Bytes=bytes,Hash=Convert.ToBase64String(sha.ComputeHash(bytes)) };
        }

        private static bool Unchanged(Snapshot snapshot)
        {
            try { return ReadSnapshot(snapshot.Path).Hash == snapshot.Hash; }
            catch { return false; }
        }

        private static bool ExactDatedCsv(string path, out DateTime date)
        {
            date=default(DateTime);
            return Path.GetExtension(path).Equals(".csv",StringComparison.OrdinalIgnoreCase)
                && DateTime.TryParseExact(Path.GetFileNameWithoutExtension(path),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date);
        }

        private static bool TryParse(string text, string device, bool history, out List<FocusSpan> work, out string error)
        {
            work=new List<FocusSpan>(); error="";
            if(!text.EndsWith("\n",StringComparison.Ordinal)) { error="incomplete trailing row"; return false; }
            var rows=ActivityRepository.ParseCsv(text);
            if(rows.Count==0) { error="missing header"; return false; }
            var h=rows[0];
            int ti=Array.IndexOf(h,"timestamp"), si=Array.IndexOf(h,"state"), ai=Array.IndexOf(h,"foreground_process"), di=Array.IndexOf(h,"sample_seconds"),
                pi=Array.IndexOf(h,"platform"), id=Array.IndexOf(h,"device_id"), a=Array.IndexOf(h,"start_epoch"), b=Array.IndexOf(h,"end_epoch");
            if(ti<0||si<0||ai<0||di<0||(history&&(pi<0||id<0||a<0||b<0))) { error="unsupported header"; return false; }
            for(int n=1;n<rows.Count;n++) {
                var r=rows[n]; double seconds,start,end; DateTime parsed;
                if(r.Length!=h.Length || !Number(r[di],out seconds) || seconds<=0 || seconds>3600) { error="invalid row "+n; return false; }
                if(pi>=0 && r[pi].Length>0 && !r[pi].Equals("Windows",StringComparison.OrdinalIgnoreCase)) { error="contains non-Windows data"; return false; }
                if(id>=0 && r[id].Length>0 && !r[id].Equals(device,StringComparison.OrdinalIgnoreCase)) { error="contains another Windows device"; return false; }
                if(b<0 || !Number(r[b],out end)) {
                    if(!DateTime.TryParseExact(r[ti],"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out parsed)) { error="invalid timestamp row "+n; return false; }
                    end=(DateTime.SpecifyKind(parsed-ActivityRepository.StatisticsOffset,DateTimeKind.Utc)-Epoch).TotalSeconds;
                }
                if(a<0 || !Number(r[a],out start)) start=end-seconds;
                if(end<=start || end-start>3600 || Math.Abs(end-start-seconds)>=0.1) { error="invalid interval row "+n; return false; }
                if(r[si]=="Work") work.Add(new FocusSpan { Start=start,End=end,Platform="Windows",App=r[ai],State="Work" });
                else if(history) { error="history contains non-Work row "+n; return false; }
            }
            return true;
        }

        internal static List<FocusSpan> Canonicalize(IEnumerable<FocusSpan> source)
        {
            var pieces=new List<FocusSpan>();
            foreach(var s in source.Where(x=>x.State=="Work"&&x.End>x.Start)) {
                for(double cursor=s.Start;cursor<s.End;) {
                    var day=Epoch.AddSeconds(cursor).Add(ActivityRepository.StatisticsOffset).Date;
                    double boundary=(DateTime.SpecifyKind(day.AddDays(1)-ActivityRepository.StatisticsOffset,DateTimeKind.Utc)-Epoch).TotalSeconds;
                    double stop=Math.Min(s.End,boundary);
                    pieces.Add(new FocusSpan { Start=cursor,End=stop,Platform="Windows",App=s.App,State="Work" }); cursor=stop;
                }
            }
            var answer=new List<FocusSpan>();
            foreach(var group in pieces.GroupBy(s=>Epoch.AddSeconds(s.Start).Add(ActivityRepository.StatisticsOffset).Date.ToString("yyyy-MM-dd")+"\0"+s.App,StringComparer.OrdinalIgnoreCase)) {
                foreach(var merged in ActivityRepository.Merge(group)) {
                    for(double cursor=merged.Start;cursor<merged.End;) {
                        double stop=Math.Min(merged.End,cursor+3600);
                        answer.Add(new FocusSpan { Start=cursor,End=stop,Platform="Windows",App=merged.App,State="Work" }); cursor=stop;
                    }
                }
            }
            return answer.OrderBy(s=>s.Start).ThenBy(s=>s.App,StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string Csv(string value) { return "\""+(value??"").Replace("\"","\"\"")+"\""; }
        private static string Serialize(IEnumerable<FocusSpan> spans, string device)
        {
            var lines=new List<string>{Header};
            foreach(var s in spans) {
                var end=Epoch.AddSeconds(s.End);
                var values=new[]{end.Add(ActivityRepository.StatisticsOffset).ToString("yyyy-MM-dd HH:mm:ss"),"Work","Archived","0",s.App,
                    (s.End-s.Start).ToString("F3",CultureInfo.InvariantCulture),"Windows",device,s.Start.ToString("F3",CultureInfo.InvariantCulture),s.End.ToString("F3",CultureInfo.InvariantCulture)};
                lines.Add(string.Join(",",values.Select(Csv)));
            }
            return string.Join("\n",lines)+"\n";
        }

        private static string Signature(IEnumerable<FocusSpan> spans)
        {
            return string.Join("\n",Canonicalize(spans).Select(s=>s.Start.ToString("F3",CultureInfo.InvariantCulture)+"|"+s.End.ToString("F3",CultureInfo.InvariantCulture)+"|"+s.App.ToLowerInvariant()));
        }

        internal static ArchiveRunResult ArchivePastWindows(string root, string device, DateTime today)
        {
            var result=new ArchiveRunResult();
            var data=Path.Combine(root,"data");
            var ownDaily=Path.Combine(root,"windows-data",device,"v2");
            var candidates=new List<Snapshot>(); var all=new List<FocusSpan>();
            foreach(var file in new[]{data,ownDaily}.Where(Directory.Exists).SelectMany(folder=>Directory.GetFiles(folder,"*.csv",SearchOption.TopDirectoryOnly))) {
                DateTime date; if(!ExactDatedCsv(file,out date)||date>=today.Date) continue;
                try {
                    var snapshot=ReadSnapshot(file); List<FocusSpan> spans; string error;
                    if(!TryParse(new UTF8Encoding(false,true).GetString(snapshot.Bytes),device,false,out spans,out error)) {
                        // Legacy data/YYYY-MM-DD.csv can contain records from several Windows devices.
                        // It is still read for statistics, but only the originating device may archive it.
                        if(error!="contains another Windows device" || !file.StartsWith(data+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                            result.Warnings.Add(Path.GetFileName(file)+" retained: "+error);
                        continue;
                    }
                    candidates.Add(snapshot); all.AddRange(spans);
                } catch(Exception e) { result.Warnings.Add(Path.GetFileName(file)+" retained: "+e.Message); }
            }
            if(candidates.Count==0) return result;
            var folder=Path.Combine(root,"windows-data",device); var history=Path.Combine(folder,"history.csv"); Snapshot oldHistory=null;
            try {
                if(File.Exists(history)) {
                    oldHistory=ReadSnapshot(history); List<FocusSpan> existing; string error;
                    if(!TryParse(new UTF8Encoding(false,true).GetString(oldHistory.Bytes),device,true,out existing,out error)) { result.Warnings.Add("history.csv unreadable; details retained: "+error); return result; }
                    all.AddRange(existing);
                }
                var canonical=Canonicalize(all); var content=Serialize(canonical,device);
                Directory.CreateDirectory(folder); var temp=Path.Combine(folder,"history."+Guid.NewGuid().ToString("N")+".partial");
                try {
                    using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { var bytes=new UTF8Encoding(false).GetBytes(content); f.Write(bytes,0,bytes.Length); f.Flush(true); }
                    List<FocusSpan> verified; string error;
                    if(!TryParse(File.ReadAllText(temp,new UTF8Encoding(false,true)),device,true,out verified,out error)||Signature(verified)!=Signature(canonical)) throw new IOException("archive verification failed: "+error);
                    if(BeforeCommitForTests!=null) BeforeCommitForTests();
                    if(candidates.Any(s=>!Unchanged(s)) || (oldHistory!=null&&!Unchanged(oldHistory)) || (oldHistory==null&&File.Exists(history))) throw new IOException("source changed during archive");
                    if(File.Exists(history)) File.Replace(temp,history,null); else File.Move(temp,history);
                    result.Changed=true;
                    foreach(var snapshot in candidates) {
                        if(!Unchanged(snapshot)) { result.Warnings.Add(Path.GetFileName(snapshot.Path)+" changed after archive; retained"); continue; }
                        try { File.Delete(snapshot.Path); result.DeletedFiles.Add(snapshot.Path); }
                        catch(Exception e) { result.Warnings.Add(Path.GetFileName(snapshot.Path)+" archived but retained: "+e.Message); }
                    }
                } finally { if(File.Exists(temp)) File.Delete(temp); }
            } catch(Exception e) { result.Warnings.Add("Archive deferred: "+e.Message); }
            return result;
        }
    }
}
