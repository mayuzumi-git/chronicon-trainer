using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
public static class EnchantItemParserTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Reject(Action action,string message){try{action();}catch{return;}throw new Exception(message);}
 static void Write(BinaryWriter w,object value){if(value is string){byte[] b=Encoding.Default.GetBytes((string)value);w.Write(1);w.Write(b.Length);w.Write(b);}else if(value is int){w.Write(7);w.Write((int)value);}else if(value is long){w.Write(10);w.Write((long)value);}else if(value is bool){w.Write(13);w.Write((bool)value?1.0:0.0);}else{w.Write(0);w.Write((double)value);}}
 static Dictionary<string,object> Gear(){var f=new Dictionary<string,object>{{"id",806.0},{"level",100.0},{"type",24.0},{"quality",4.0},{"curse_id",-4.0},{"bless_id",-4.0},{"cursed",false},{"purified",false}};for(int i=0;i<10;i++)foreach(string prefix in new[]{"enchant","enchant_value","enchant_locked","enchant_solid","enchant_rune"})f[prefix+i]=0.0;f["enchant0"]=1092.0;f["enchant_solid0"]=1.0;f["enchant1"]=6.0;f["enchant2"]=50.0;return f;}
 static string Serialize(Dictionary<string,object> f){using(var m=new MemoryStream()){using(var w=new BinaryWriter(m,Encoding.Default,true)){w.Write(403);w.Write(f.Count);foreach(var pair in f){Write(w,pair.Key);Write(w,pair.Value);}}return BitConverter.ToString(m.ToArray()).Replace("-","").ToLowerInvariant();}}
 static void Plan(EnchantItem item){int[][] pools={new[]{6,7},new[]{50,55},new[]{65}};int[] targets=Enumerable.Range(0,10).Select(i=>(int)item.Get("enchant"+i)).ToArray();targets[1]=7;targets[2]=55;Check(item.Plan(targets,pools).Count(x=>x>0)==2,"Ordinary affixes still support selection");targets[0]=7;Reject(()=>item.Plan(targets,pools),"Fixed ability became editable");}
 public static void Main(string[] args){
  var f=Gear();var plain=EnchantItem.Parse(Serialize(f));Check(plain.Id==806&&plain.Get("cursed")==0,"Original numeric and boolean formats");Plan(plain);
  f["curse_id"]=7L;f["cursed"]=true;string curse=Serialize(f);var cursed=EnchantItem.Parse(curse);Check(cursed.Get("curse_id")==7&&cursed.Get("cursed")==1&&cursed.Fields["curse_id"] is long&&cursed.Serialized==curse,"Cursed int64 parsed and original serialization retained");Plan(cursed);
  f["curse_id"]=-4.0;f["cursed"]=false;f["bless_id"]=5L;f["purified"]=true;var blessed=EnchantItem.Parse(Serialize(f));Check(blessed.Get("bless_id")==5&&blessed.Get("purified")==1&&blessed.Fields["bless_id"] is long,"Blessed int64 field parsed");Plan(blessed);
  f["id"]=806L;f["level"]=100;f["enchant_locked2"]=1;f["native_token"]=long.MaxValue;var integer=EnchantItem.Parse(Serialize(f));Check(integer.Id==806&&integer.Get("level")==100&&!integer.Editable(2),"Int32/int64 work in existing numeric access and lock guard");Check((long)integer.Fields["native_token"]==long.MaxValue,"Unused int64 metadata retains full precision");
  f["trailing_integer"]=7L;string truncated=Serialize(f);Reject(()=>EnchantItem.Parse(truncated.Substring(0,truncated.Length-2)),"Truncated int64 accepted");
  foreach(string path in args){string text=File.ReadAllText(path);var actual=EnchantItem.Parse(text);Check(actual.Serialized==text,"Actual equipment bytes changed");Console.WriteLine("REAL_SAMPLE_OK id="+actual.Id+" curse="+actual.Get("curse_id")+" blessing="+actual.Get("bless_id"));}
  Console.WriteLine("PASS: cursed/blessed int64, int32 numeric access, full-width metadata, booleans, unchanged serialization, affix planning, fixed/locked guards, truncated input rejection.");
 }
}
