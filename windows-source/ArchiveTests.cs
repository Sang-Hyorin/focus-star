using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FocusStar
{
    internal static class ArchiveTests
    {
        static int count;
        static readonly DateTime Epoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
        static string TestDirectory { get { string path=Environment.GetEnvironmentVariable("FOCUSSTAR_TEST_ROOT"); return String.IsNullOrWhiteSpace(path) ? Path.GetTempPath() : path; } }
        static void True(bool value,string label) { if(!value) throw new Exception(label); count++; }
        static void Eq(double actual,double expected,string label) { if(Math.Abs(actual-expected)>0.01) throw new Exception(label+": "+actual+" != "+expected); count++; }
        static double E(int day,int hour,int minute,int second=0) { var local=new DateTime(2026,1,day,hour,minute,second); return (DateTime.SpecifyKind(local-ActivityRepository.StatisticsOffset,DateTimeKind.Utc)-Epoch).TotalSeconds; }
        static string Row(int day,int sh,int sm,int eh,int em,string state,string app,string platform,string device)
        {
            double start=E(day,sh,sm),end=E(day,eh,em); if(eh<sh) end=E(day+1,eh,em);
            return string.Join(",",new[]{new DateTime(2026,1,day,eh,em,0).ToString("yyyy-MM-dd HH:mm:ss"),state,"detail","0",app,(end-start).ToString("F3",CultureInfo.InvariantCulture),platform,device,start.ToString("F3",CultureInfo.InvariantCulture),end.ToString("F3",CultureInfo.InvariantCulture)}.Select(C))+"\n";
        }
        static string C(string s) { return "\""+s.Replace("\"","\"\"")+"\""; }
        static string FileText(params string[] rows) { return CrossDayArchive.Header+"\n"+string.Concat(rows); }
        static List<FocusSpan> ReadAll(string root)
        {
            var spans=new List<FocusSpan>();
            foreach(var folder in new[]{"data","mac-data","windows-data"}) {
                var dir=Path.Combine(root,folder); if(!Directory.Exists(dir)) continue;
                foreach(var f in Directory.GetFiles(dir,"*.csv",SearchOption.AllDirectories)) {
                    string n=Path.GetFileNameWithoutExtension(f); DateTime ignored;
                    if(!(n.Equals("history",StringComparison.OrdinalIgnoreCase)||(n.Length>=10&&DateTime.TryParseExact(n.Substring(0,10),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out ignored)))) continue;
                    spans.AddRange(ActivityRepository.ParseRecords(File.ReadAllText(f),folder=="mac-data"?"macOS":"Windows"));
                }
            }
            return spans;
        }
        static SortedDictionary<DateTime,DayTotal> Totals(string root) { return ActivityRepository.Summarize(ReadAll(root),new HashSet<string>(new[]{"Code","WINWORD"},StringComparer.OrdinalIgnoreCase)); }
        static Dictionary<string,double> Apps(string root)
        {
            return Totals(root).SelectMany(d=>d.Value.Spans).GroupBy(s=>s.Platform+"|"+s.App).ToDictionary(g=>g.Key,g=>ActivityRepository.Union(g));
        }
        static void Same(SortedDictionary<DateTime,DayTotal> a,SortedDictionary<DateTime,DayTotal> b,string label)
        {
            True(a.Keys.SequenceEqual(b.Keys),label+" days");
            foreach(var day in a.Keys) { Eq(b[day].Focus,a[day].Focus,label+" focus "+day); Eq(b[day].Windows,a[day].Windows,label+" Windows "+day); Eq(b[day].Mac,a[day].Mac,label+" Mac "+day); }
        }
        static void Main()
        {
			var saved=new SortedDictionary<DateTime,double>();
			ActivityRepository.MergeSavedFocusCsv("date,focus_seconds,windows_seconds,mac_seconds,timezone,sync_status\n2026-01-01,3600,0,0,UTC+08:00,ok\n2026-01-01,5400,0,0,UTC+08:00,ok\n2026-02-30,7000,0,0,UTC+08:00,bad\n2026-01-02,-1,0,0,UTC+08:00,bad\n2026-01-03,90000,0,0,UTC+08:00,bad\n2026-01-04,NaN,0,0,UTC+08:00,bad\n2026-01-05,7200,0,0,UTC+00:00,bad\n2026-01-06,1800,0,0,UTC+08:00",saved);
			ActivityRepository.MergeSavedFocusCsv("date,windows_seconds,mac_seconds,focus_seconds\n2026-01-01,0,0,4500\n2026-01-07,0,0,1200\n",saved);
			Eq(saved[new DateTime(2026,1,1)],5400,"saved device totals use maximum, not sum");
			True(saved.Count==2&&!saved.ContainsKey(new DateTime(2026,1,6)),"invalid timezone values and incomplete trailing rows ignored");
			var settled=new SortedDictionary<DateTime,DayTotal>();
			settled[new DateTime(2026,1,1)]=new DayTotal { Focus=3600, Windows=3600 };
			settled[new DateTime(2026,1,7)]=new DayTotal { Focus=2400, Windows=2400 };
			ActivityRepository.ApplySettledFocus(settled,saved);
			Eq(settled[new DateTime(2026,1,1)].Focus,5400,"saved daily total prevents a lower incomplete union");
			Eq(settled[new DateTime(2026,1,7)].Focus,2400,"larger current union wins over saved total");

            string root=Path.Combine(TestDirectory,"focus-archive-tests-"+Guid.NewGuid().ToString("N")); string device="11111111-1111-1111-1111-111111111111";
            Directory.CreateDirectory(Path.Combine(root,"data")); Directory.CreateDirectory(Path.Combine(root,"mac-data","mac","v2"));
            string old=FileText(
                Row(1,10,0,10,30,"Work","Code","Windows",device), Row(1,10,30,11,0,"Work","Code","Windows",device), Row(1,11,0,11,30,"Work","Code","Windows",device),
                Row(1,12,0,12,10,"Inactive","Code","Windows",device), Row(2,23,59,0,0,"Work","Code","Windows",device));
            File.WriteAllText(Path.Combine(root,"data","2026-01-01.csv"),old,new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root,"data","2026-01-02.csv"),FileText(Row(2,23,59,0,0,"Work","Code","Windows",device)),new UTF8Encoding(false));
            string conflict=Path.Combine(root,"data","2026-01-01-copy.csv");
            File.WriteAllText(conflict,FileText(Row(1,10,0,10,30,"Work","Code","Windows",device),Row(1,14,0,14,10,"Work","WINWORD","Windows",device)),new UTF8Encoding(false));
            string today=Path.Combine(root,"data","2026-01-03.csv"); File.WriteAllText(today,FileText(Row(3,12,0,12,1,"Work","Code","Windows",device)),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root,"mac-data","mac","v2","2026-01-01.csv"),FileText(Row(1,10,45,11,45,"Work","Code","macOS","mac-device")),new UTF8Encoding(false));
            var before=Totals(root); var appsBefore=Apps(root);
            var run=CrossDayArchive.ArchivePastWindows(root,device,new DateTime(2026,1,3));
            True(run.Changed,"archive changed"); True(run.DeletedFiles.Count==2,"two exact past details cleaned"); True(File.Exists(today),"today detail retained"); True(File.Exists(conflict),"conflict copy retained");
            string history=Path.Combine(root,"windows-data",device,"history.csv"); True(File.Exists(history),"history written");
            var historyRows=ActivityRepository.ParseCsv(File.ReadAllText(history)); int state=Array.IndexOf(historyRows[0],"state"),reason=Array.IndexOf(historyRows[0],"reason"),idle=Array.IndexOf(historyRows[0],"idle_seconds"),seconds=Array.IndexOf(historyRows[0],"sample_seconds");
            True(historyRows.Skip(1).All(r=>r[state]=="Work"&&r[reason]=="Archived"&&r[idle]=="0"&&double.Parse(r[seconds],CultureInfo.InvariantCulture)<=3600),"history keeps compact bounded Work segments");
            True(historyRows.Skip(1).Any(r=>r[0].StartsWith("2026-01-03 00:00:00")),"midnight split preserved");
            var after=Totals(root); Same(before,after,"before and after"); var appsAfter=Apps(root);
            True(appsBefore.Keys.OrderBy(x=>x).SequenceEqual(appsAfter.Keys.OrderBy(x=>x)),"app keys stable"); foreach(var k in appsBefore.Keys) Eq(appsAfter[k],appsBefore[k],"app stable "+k);
            File.WriteAllText(Path.Combine(root,"data","2026-01-01.csv"),old,new UTF8Encoding(false));
            var late=CrossDayArchive.ArchivePastWindows(root,device,new DateTime(2026,1,3)); True(late.Changed&&late.DeletedFiles.Count==1,"late resync archived idempotently"); Same(after,Totals(root),"late resync no duplicate");

            string interrupted=Path.Combine(TestDirectory,"focus-archive-interrupt-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(interrupted,"data"));
            string changing=Path.Combine(interrupted,"data","2026-01-01.csv"); File.WriteAllText(changing,FileText(Row(1,9,0,9,1,"Work","Code","Windows",device)),new UTF8Encoding(false));
            CrossDayArchive.BeforeCommitForTests=()=>File.AppendAllText(changing,Row(1,9,1,9,2,"Work","Code","Windows",device));
            var stopped=CrossDayArchive.ArchivePastWindows(interrupted,device,new DateTime(2026,1,3)); CrossDayArchive.BeforeCommitForTests=null;
            True(!stopped.Changed&&File.Exists(changing)&&!File.Exists(Path.Combine(interrupted,"windows-data",device,"history.csv")),"source change aborts archive and retains detail");

            string incomplete=Path.Combine(TestDirectory,"focus-archive-incomplete-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(incomplete,"data"));
            string bad=Path.Combine(incomplete,"data","2026-01-01.csv"); File.WriteAllText(bad,CrossDayArchive.Header+"\n\"partial",new UTF8Encoding(false));
            var held=CrossDayArchive.ArchivePastWindows(incomplete,device,new DateTime(2026,1,3)); True(!held.Changed&&File.Exists(bad),"incomplete file retained");

            string bufferedRoot=Path.Combine(TestDirectory,"focus-buffer-tests-"+Guid.NewGuid().ToString("N"));
            string bufferedLocal=Path.Combine(TestDirectory,"focus-buffer-local-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(bufferedRoot);
            var repository=new ActivityRepository(bufferedRoot,bufferedLocal);
            repository.SetWorkProcessNames(new[]{"Code"});
            DateTime firstEnd=DateTime.Now.AddMinutes(-2);
            string currentDay=ActivityRepository.Today.ToString("yyyy-MM-dd");
            repository.AppendSegment(currentDay,firstEnd,"Work","test",0,"Code",60);
            True(!Directory.Exists(Path.Combine(bufferedRoot,"data")),"samples stay local before cloud interval");
            True(Directory.GetFiles(Path.Combine(bufferedLocal,"pending"),"*.csv",SearchOption.AllDirectories).Length==1,"local recovery journal created");
            repository.Refresh();
            Eq(repository.GetDay(ActivityRepository.Today).Windows,60,"pending sample visible in live totals");
            string summary=Directory.GetFiles(Path.Combine(bufferedRoot,"windows-reports"),"daily-summary.csv",SearchOption.AllDirectories).Single();
            string summaryBefore=File.ReadAllText(summary);
            repository.AppendSegment(currentDay,firstEnd.AddMinutes(1),"Work","test",0,"Code",60);
            repository.Refresh();
            Eq(repository.GetDay(ActivityRepository.Today).Windows,120,"new pending sample visible without cloud write");
            True(File.ReadAllText(summary)==summaryBefore,"summary throttled before ten minutes");
            True(repository.FlushPendingToShared(),"pending samples batch committed");
            string localDevice=File.ReadAllText(Path.Combine(bufferedLocal,"device-id")).Trim();
            string shared=Path.Combine(bufferedRoot,"windows-data",localDevice,"v2",currentDay+".csv");
            True(File.Exists(shared),"shared daily detail created by batch");
            True(!Directory.Exists(Path.Combine(bufferedRoot,"data")),"new Windows samples do not write legacy shared CSV");
            True(Directory.GetFiles(Path.Combine(bufferedLocal,"pending"),"*.csv",SearchOption.AllDirectories).Length==0,"local journal cleared after durable commit");
            Eq(ActivityRepository.Union(ActivityRepository.ParseRecords(File.ReadAllText(shared),"Windows")),120,"batch preserves exact focus intervals");
            repository.Refresh(true);
            True(File.ReadAllText(summary)!=summaryBefore,"forced lifecycle refresh updates summary");
            DateTime unchangedWrite=File.GetLastWriteTimeUtc(summary);
            System.Threading.Thread.Sleep(30);
            repository.Refresh(true);
            True(File.GetLastWriteTimeUtc(summary)==unchangedWrite,"unchanged summary is not rewritten");

			string settledRoot=Path.Combine(TestDirectory,"focus-settlement-tests-"+Guid.NewGuid().ToString("N"));
			string settledLocal=Path.Combine(TestDirectory,"focus-settlement-local-"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(settledRoot,"mac-reports","mac-a"));
			File.WriteAllText(Path.Combine(settledRoot,"mac-reports","mac-a","daily-summary.csv"),"date,windows_seconds,mac_seconds,focus_seconds\n2026-01-08,0,5400,5400\n",new UTF8Encoding(false));
			var settledRepository=new ActivityRepository(settledRoot,settledLocal);
			settledRepository.SetWorkProcessNames(new[]{"Code"}); settledRepository.Refresh();
			Eq(settledRepository.GetDay(new DateTime(2026,1,8)).Focus,5400,"shared device summary supplies settlement floor");
			True(File.Exists(Path.Combine(settledLocal,"settled-daily.csv")),"settlement high-water mark persisted locally");
			Directory.Delete(Path.Combine(settledRoot,"mac-reports"),true);
			Directory.Delete(Path.Combine(settledRoot,"windows-reports"),true);
			var restartedRepository=new ActivityRepository(settledRoot,settledLocal);
			restartedRepository.SetWorkProcessNames(new[]{"Code"}); restartedRepository.Refresh();
			Eq(restartedRepository.GetDay(new DateTime(2026,1,8)).Focus,5400,"restart plus incomplete sync cannot lower settled day");

            string multi=Path.Combine(TestDirectory,"focus-multiwindows-tests-"+Guid.NewGuid().ToString("N"));
            string other="22222222-2222-2222-2222-222222222222";
            string ownDaily=Path.Combine(multi,"windows-data",device,"v2");
            string otherDaily=Path.Combine(multi,"windows-data",other,"v2");
            string macDaily=Path.Combine(multi,"mac-data","mac","v2");
            Directory.CreateDirectory(Path.Combine(multi,"data")); Directory.CreateDirectory(ownDaily); Directory.CreateDirectory(otherDaily); Directory.CreateDirectory(macDaily);
            string foreign=Path.Combine(multi,"data","2026-01-01.csv");
            string mixed=Path.Combine(multi,"data","2026-01-02.csv");
            string ownPast=Path.Combine(ownDaily,"2026-01-01.csv");
            string ownToday=Path.Combine(ownDaily,"2026-01-03.csv");
            File.WriteAllText(foreign,FileText(Row(1,10,30,11,30,"Work","Code","Windows",other)),new UTF8Encoding(false));
            File.WriteAllText(mixed,FileText(Row(2,10,0,10,30,"Work","Code","Windows",device),Row(2,10,15,10,45,"Work","Code","Windows",other)),new UTF8Encoding(false));
            File.WriteAllText(ownPast,FileText(Row(1,10,0,11,0,"Work","Code","Windows",device)),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(otherDaily,"2026-01-01.csv"),FileText(Row(1,10,30,11,30,"Work","Code","Windows",other)),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(macDaily,"2026-01-01.csv"),FileText(Row(1,10,45,11,15,"Work","Code","macOS","mac")),new UTF8Encoding(false));
            File.WriteAllText(ownToday,FileText(Row(3,9,0,9,1,"Work","Code","Windows",device)),new UTF8Encoding(false));
            var beforeMulti=Totals(multi);
            Eq(beforeMulti[new DateTime(2026,1,1)].Focus,5400,"two Windows devices and Mac overlap counted once");
            Eq(beforeMulti[new DateTime(2026,1,1)].Windows,5400,"Windows detail is union across devices");
            Eq(beforeMulti[new DateTime(2026,1,1)].Mac,1800,"Mac detail remains separate");
            Eq(beforeMulti[new DateTime(2026,1,2)].Windows,2700,"mixed legacy device file is unioned");
            var multiArchive=CrossDayArchive.ArchivePastWindows(multi,device,new DateTime(2026,1,3));
            True(multiArchive.Changed,"own v2 daily archived");
            True(!File.Exists(ownPast)&&File.Exists(foreign)&&File.Exists(mixed)&&File.Exists(ownToday),"only own past daily file cleaned");
            True(!multiArchive.Warnings.Any(w=>w.Contains("another Windows device")),"foreign legacy files do not produce sync warnings");
            Same(beforeMulti,Totals(multi),"multi-device archive keeps daily totals");
            True(File.Exists(Path.Combine(multi,"windows-data",device,"history.csv")),"own history written to device directory");

            Directory.Delete(root,true);
            Directory.Delete(interrupted,true);
            Directory.Delete(incomplete,true);
            Directory.Delete(bufferedRoot,true);
            Directory.Delete(bufferedLocal,true);
			Directory.Delete(settledRoot,true);
			Directory.Delete(settledLocal,true);
            Directory.Delete(multi,true);
            Console.WriteLine("PASS: "+count+" assertions. Temporary fixtures removed.");
        }
    }
}
