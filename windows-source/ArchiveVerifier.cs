using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FocusStar
{
    internal static class ArchiveVerifier
    {
        static bool Candidate(string f) { string n=Path.GetFileNameWithoutExtension(f); DateTime d; return Path.GetExtension(f).Equals(".csv",StringComparison.OrdinalIgnoreCase)&&(n.Equals("history",StringComparison.OrdinalIgnoreCase)||(n.Length>=10&&DateTime.TryParseExact(n.Substring(0,10),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out d))); }
        static List<FocusSpan> Read(string root) { var a=new List<FocusSpan>(); foreach(var folder in new[]{"data","mac-data","windows-data"}) { var d=Path.Combine(root,folder); if(!Directory.Exists(d)) continue; foreach(var f in Directory.GetFiles(d,"*.csv",SearchOption.AllDirectories).Where(Candidate)) a.AddRange(ActivityRepository.ParseRecords(File.ReadAllText(f),folder=="mac-data"?"macOS":"Windows")); } return a; }
        static SortedDictionary<DateTime,DayTotal> Totals(string root,HashSet<string> names) { return ActivityRepository.Summarize(Read(root),names); }
        static Dictionary<string,double> Apps(SortedDictionary<DateTime,DayTotal> days) { return days.SelectMany(x=>x.Value.Spans).GroupBy(x=>x.Platform+"|"+x.App).ToDictionary(x=>x.Key,x=>ActivityRepository.Union(x)); }
        static void Main(string[] args) {
            string root=args[0],device=args[1]; var settings=AppSettings.Load(Path.Combine(root,"settings-mac.json")); var names=new HashSet<string>(settings.workProcessNames,StringComparer.OrdinalIgnoreCase);
            var before=Totals(root,names); var beforeApps=Apps(before); var run=CrossDayArchive.ArchivePastWindows(root,device,ActivityRepository.Today); var after=Totals(root,names); var afterApps=Apps(after);
            if(!before.Keys.SequenceEqual(after.Keys)) throw new Exception("Day set changed");
            foreach(var d in before.Keys) { if(Math.Abs(before[d].Focus-after[d].Focus)>.01||Math.Abs(before[d].Windows-after[d].Windows)>.01||Math.Abs(before[d].Mac-after[d].Mac)>.01) throw new Exception("Daily totals changed: "+d.ToString("yyyy-MM-dd")); }
            if(!beforeApps.Keys.OrderBy(x=>x).SequenceEqual(afterApps.Keys.OrderBy(x=>x))) throw new Exception("App set changed");
            foreach(var k in beforeApps.Keys) if(Math.Abs(beforeApps[k]-afterApps[k])>.01) throw new Exception("App duration changed: "+k);
            var repository=new ActivityRepository(root); repository.SetWorkProcessNames(settings.workProcessNames);
            string transfer=Path.Combine(root,"archive-transfer-test.focusstar"); repository.ExportBackup(transfer);
            using(var zip=System.IO.Compression.ZipFile.OpenRead(transfer)) {
                if(!zip.Entries.Any(e=>e.FullName.StartsWith("windows-data/")&&e.FullName.EndsWith("history.csv"))) throw new Exception("Transfer omitted Windows history");
                if(!zip.Entries.Any(e=>e.FullName.StartsWith("mac-data/"))) throw new Exception("Transfer omitted Mac data");
            }
            Console.WriteLine("PASS actual snapshot: "+before.Count+" days, "+beforeApps.Count+" platform/apps, "+run.DeletedFiles.Count+" details archived, "+run.Warnings.Count+" warnings");
            foreach(var d in after) Console.WriteLine(d.Key.ToString("yyyy-MM-dd")+" focus="+d.Value.Focus.ToString("F3",CultureInfo.InvariantCulture)+" windows="+d.Value.Windows.ToString("F3",CultureInfo.InvariantCulture)+" mac="+d.Value.Mac.ToString("F3",CultureInfo.InvariantCulture));
        }
    }
}
