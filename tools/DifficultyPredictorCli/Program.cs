using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json.Linq;
using Syncfusion.PMML;
using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

if (args.Length == 0) {
    Console.WriteLine("Usage: dotnet run --project tools/DifficultyPredictorCli -- <zipPathOrUrl>");
    return;
}

var input = args[0];
var work = Path.Combine(Path.GetTempPath(), "edda-cli-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(work);
var zipPath = input.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? Path.Combine(work, "map.zip") : input;
if (input.StartsWith("http", StringComparison.OrdinalIgnoreCase)) {
    using var http = new HttpClient();
    await File.WriteAllBytesAsync(zipPath, await http.GetByteArrayAsync(input));
}
ZipFile.ExtractToDirectory(zipPath, work, true);

var datFiles = Directory.GetFiles(work, "*.dat", SearchOption.AllDirectories)
    .Where(p => Path.GetFileName(p).StartsWith("notes", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p).ToList();
var info = Directory.GetFiles(work, "info.dat", SearchOption.AllDirectories).FirstOrDefault();
if (info == null || datFiles.Count == 0) throw new Exception("Could not find info.dat or notes*.dat");

var infoJson = JObject.Parse(File.ReadAllText(info));
double bpm = infoJson["_beatsPerMinute"]?.Value<double>() ?? infoJson["beatsPerMinute"]?.Value<double>() ?? 120;
double songDuration = infoJson["_songDuration"]?.Value<double>() ?? infoJson["songDuration"]?.Value<double>() ?? 0;

var models = LoadModels("Properties/Resources.resx");

Console.WriteLine($"BPM: {bpm}");
Console.WriteLine("Difficulty\tMelchior\tNytilde\tPKBeam\tTimeline");
foreach (var f in datFiles) {
    var notes = ParseNotes(f);
    var mel = Melchior(notes, bpm);
    var nyt = Nytilde(notes, bpm, songDuration, models.nytilde, models.nytildeFallback);
    var pk = PKBeam(notes, bpm, songDuration, models.pkbeamPmml);
    var tl = Timeline(notes, bpm, songDuration);
    Console.WriteLine($"{Path.GetFileNameWithoutExtension(f)}\t{mel:0.00}\t{nyt:0.00}\t{pk:0.00}\t{tl:0.00}");
}

static List<Note> ParseNotes(string path) {
    var json = JObject.Parse(File.ReadAllText(path));
    var arr = (json["_notes"] ?? json["notes"]) as JArray ?? new JArray();
    return arr.Select(n => new Note(n["_time"]?.Value<double>() ?? n["time"]!.Value<double>(), n["_lineIndex"]?.Value<int>() ?? n["lineIndex"]!.Value<int>())).OrderBy(n => n.Beat).ThenBy(n => n.Col).ToList();
}

static (byte[] nytilde, byte[] nytildeFallback, byte[] pkbeamPmml) LoadModels(string resxPath) {
    var doc = XDocument.Load(resxPath);
    byte[] Get(string name) {
        var data = doc.Root!.Elements("data").First(e => e.Attribute("name")?.Value == name);
        var val = data.Element("value")!.Value.Trim();
        return Convert.FromBase64String(val);
    }
    return (Get("Edda_MLDP_Nytilde"), Get("Edda_MLDP_Nytilde_Fallback"), Get("Edda_MLDP_PKBeam"));
}

static double Melchior(List<Note> notes, double bpm) { if (notes.Count == 0) return 0; var score=0.0; var hands=notes.Take(2).OrderBy(n=>n.Col).ToList(); foreach(var n in notes.Skip(2)){var pts=hands.Select(h=>(Math.Abs(h.Col-n.Col)+1)/Math.Max(0.02,Math.Pow(60*(n.Beat-h.Beat)/bpm,2))).ToList();var best=pts.Min();score+=best;hands[pts.IndexOf(best)]=n;} var maxTime=60/bpm*(notes.Last().Beat-notes.First().Beat); return maxTime==0?0:0.6632333348*Math.Sqrt(score/maxTime);} 

static double Nytilde(List<Note> notes,double bpm,double songDuration,byte[] model,byte[] fallback){ if(notes.Count==0) return 0; var times=notes.Select(n=>60/bpm*n.Beat).ToList(); var dif=times.Zip(times.Skip(1),(a,b)=>b-a).ToList(); var no0=dif.Where(d=>Math.Abs(d)>1e-9).ToList(); var maxTime=times.Last()-times.First(); var noteDensity=times.Count/maxTime; var avg=no0.Count>0?no0.Average():0; var mel=MelchiorRaw(notes,bpm); var local=times.Select(t=>{var s=t-2; var e=t+2; return (times.Count(x=>x>=s&&x<=e))/2.0;}).ToList(); var high=Quantile(local,0.95); var typ=no0.Count>0?Quantile(no0,0.3):0; var count=no0.Count(x=>2>=Math.Abs(x)); var inRange = noteDensity<=7.615101 && avg>=0.152743 && count<=1578.5 && high<=25.5 && typ>=0.091463; var m=inRange?model:fallback; var path=Path.GetTempFileName(); File.WriteAllBytes(path,m); using var session=new InferenceSession(path); var source=new float[]{(float)noteDensity,(float)avg,(float)mel,(float)high,(float)typ,(float)count}; var tensor=new DenseTensor<float>(source,new[]{1,source.Length}); var inputs=new List<NamedOnnxValue>{NamedOnnxValue.CreateFromTensor("input",tensor)}; using var outp=session.Run(inputs); return outp.First().AsTensor<float>().First(); }
static double MelchiorRaw(List<Note> notes,double bpm){ if(notes.Count<2)return 0; var hands=notes.Take(2).OrderBy(n=>n.Col).ToList(); var score=0.0; foreach(var n in notes.Skip(2)){var pts=hands.Select(h=>(Math.Abs(h.Col-n.Col)+1)/Math.Max(0.02,Math.Pow(60*(n.Beat-h.Beat)/bpm,2))).ToList(); var best=pts.Min(); score+=best; hands[pts.IndexOf(best)]=n;} return score; }
static double PKBeam(List<Note> notes,double bpm,double songDuration,byte[] pmml){ var duration=notes.Count>0?60/bpm*notes.Last().Beat:songDuration; var nd=notes.Count/duration; var local=new List<double>(); var low=0.0; var high=2.75; do {var c=notes.Count(n=>{var t=60/bpm*n.Beat; return low<=t&&t<=high;}); local.Add(c/2.75); low+=0.25; high+=0.25;} while(high<duration); var peak=Quantile(local,0.95); var path=Path.GetTempFileName(); File.WriteAllBytes(path,pmml); using var reader=File.OpenText(path); var doc=new PMMLDocument(reader); var curr=CultureInfo.CurrentCulture; CultureInfo.CurrentCulture=CultureInfo.InvariantCulture; try{ var svm=new SupportVectorMachineModelEvaluator(doc); var res=svm.GetResult(new { BPM=bpm, NoteDensity=nd, HighNoteDensity2s=peak},null); svm.Dispose(); var xml=new XmlDocument(); xml.Load(path); var constants=xml.GetElementsByTagName("Constant"); var unvar=double.Parse(constants[0].InnerText,CultureInfo.InvariantCulture); var unmean=double.Parse(constants[1].InnerText,CultureInfo.InvariantCulture); return (double)res.PredictedValue*unvar+unmean; } finally {CultureInfo.CurrentCulture=curr;} }
static double Timeline(List<Note> notes,double bpm,double songDuration){ if(notes.Count==0)return 0; var windows=new List<List<Note>>(); var max=Math.Max(songDuration,60d/bpm*notes.Last().Beat); for(double s=0;s<max;s+=0.5){var e=s+4; windows.Add(notes.Where(n=>{var t=60d/bpm*n.Beat; return t>=s&&t<e;}).ToList());} var strains=windows.Select(w=>{if(w.Count==0)return 0d; var times=w.Select(n=>60/bpm*n.Beat).OrderBy(x=>x).ToList(); var intervals=times.Zip(times.Skip(1),(a,b)=>b-a).Where(i=>i>0).ToList(); var nps=w.Count/4d; var peak=intervals.Count==0?nps:1/Math.Max(0.001,intervals.Min()); var iv=Var(intervals); var jumps=w.Zip(w.Skip(1),(a,b)=>(double)Math.Abs(a.Col-b.Col)).ToList(); var jm=jumps.Count>0?jumps.Average():0; var jv=Var(jumps); var rep=jumps.Count>0?jumps.Count(x=>x==0)/(double)jumps.Count:1; var alt=jumps.Count>1?jumps.Zip(jumps.Skip(1),(a,b)=>Math.Abs(a-b)>0?1d:0d).Average():0; var rec=intervals.Count>0?intervals.Count(x=>x>0.30)/(double)intervals.Count:1; var speed=0.6*nps+0.4*peak; var stamina=nps*(1-rec*0.5); var rhythm=iv+alt; var awk=jm+jv+rep*0.3; return speed*0.40+stamina*0.25+rhythm*0.20+awk*0.15; }).ToList(); var ord=strains.OrderBy(x=>x).ToList(); var p95=Quantile(ord,0.95); var mx=ord.Last(); var top=ord.TakeLast(Math.Max(1,(int)Math.Ceiling(strains.Count*0.1))).Average(); var th=Quantile(ord,0.8); var sustained=strains.Count(x=>x>=th)*0.5; var mean=strains.Average(); var varc=strains.Sum(x=>(x-mean)*(x-mean))/strains.Count; return p95*0.32+mx*0.22+top*0.22+sustained*0.16+varc*0.08; }
static double Var(List<double> xs)=>xs.Count>1?xs.Sum(x=>Math.Pow(x-xs.Average(),2))/xs.Count:0;
static double Quantile(List<double> sorted,double q){ if(sorted.Count==0)return 0; if(!sorted.SequenceEqual(sorted.OrderBy(x=>x)))sorted=sorted.OrderBy(x=>x).ToList(); var i=(sorted.Count-1)*q; var lo=(int)Math.Floor(i); var hi=(int)Math.Ceiling(i); if(lo==hi)return sorted[lo]; var f=i-lo; return sorted[lo]*(1-f)+sorted[hi]*f; }

record Note(double Beat, int Col);
