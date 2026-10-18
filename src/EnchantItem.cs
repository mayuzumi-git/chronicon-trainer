using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public sealed class EnchantItem {
    public const int SlotCapacity=10;
    public int NativeSlotLimit=SlotCapacity;
    public string Serialized; public long Reference,GlobalAddress; public readonly Dictionary<string,object> Fields=new Dictionary<string,object>();
    public int Id { get { return (int)Get("id"); } }
    public double Get(string key) { object value; if(!Fields.TryGetValue(key,out value) || !(value is double)) throw new Exception("装备字段缺失："+key); return (double)value; }
    static object ReadValue(BinaryReader reader) {
        int type=reader.ReadInt32();
        if(type==0 || type==13) return reader.ReadDouble();
        if(type==1) { int length=reader.ReadInt32(); if(length<0 || length>65536 || length>reader.BaseStream.Length-reader.BaseStream.Position) throw new Exception("装备文本长度无效。"); return Encoding.Default.GetString(reader.ReadBytes(length)); }
        throw new Exception("不支持的装备字段类型："+type);
    }
    public static EnchantItem Parse(string text) {
        if(text.Length<16 || text.Length>131072 || text.Length%2!=0) throw new Exception("附魔师中没有有效装备。");
        byte[] bytes=new byte[text.Length/2]; for(int i=0;i<bytes.Length;i++) bytes[i]=Convert.ToByte(text.Substring(i*2,2),16);
        var item=new EnchantItem{Serialized=text};
        using(var reader=new BinaryReader(new MemoryStream(bytes))) {
            if(reader.ReadInt32()!=403) throw new Exception("装备序列化版本不匹配。");
            int count=reader.ReadInt32(); if(count<1 || count>1000) throw new Exception("装备字段数量无效。");
            for(int i=0;i<count;i++) { var key=ReadValue(reader) as string; if(key==null || item.Fields.ContainsKey(key)) throw new Exception("装备字段无效。"); item.Fields.Add(key,ReadValue(reader)); }
            if(reader.BaseStream.Position!=bytes.Length) throw new Exception("装备数据长度不匹配。");
        }
        if(!ItemCatalog.Equipment.Any(x=>x.Id==item.Id)) throw new Exception("附魔师中不是已识别的装备。");
        for(int i=0;i<SlotCapacity;i++) foreach(string prefix in new[]{"enchant","enchant_value","enchant_locked","enchant_solid","enchant_rune"}) item.Get(prefix+i);
        return item;
    }
    public int[] OccupiedSlots { get { return Enumerable.Range(0,SlotCapacity).Where(i=>Get("enchant"+i)>0).ToArray(); } }
    public bool Editable(int slot) { return slot>=0 && slot<NativeSlotLimit && Get("enchant"+slot)>0 && Get("enchant_solid"+slot)==0 && Get("enchant_rune"+slot)==0 && Get("enchant_locked"+slot)==0; }
    public int Kind(int slot,int[][] pools) { int id=(int)Get("enchant"+slot); for(int k=0;k<3;k++) if(pools[k].Contains(id)) return k; return -1; }
    public int ConvertibleSlot(int[][] pools) {
        int[] types={2,12,13,14,21,22,23,24,36};
        if(Get("quality")<=2 || !types.Contains((int)Get("type"))) return -1;
        var ordinary=OccupiedSlots.Where(i=>Get("enchant_solid"+i)==0 && Get("enchant_rune"+i)==0 && (Kind(i,pools)==0 || Kind(i,pools)==2)).ToArray();
        // Necklaces/rings retain their first guaranteed epic. Their first major
        // (or a previously converted extra epic) is the selectable major/epic row.
        int guaranteed=((int)Get("type")==12 || (int)Get("type")==13)?ordinary.Where(i=>Kind(i,pools)==2).DefaultIfEmpty(-1).First():-1;
        return ordinary.Where(i=>i!=guaranteed).DefaultIfEmpty(-1).First();
    }
    public int[] Allowed(int slot,int[][] pools) {
        int kind=Kind(slot,pools); if(!Editable(slot) || kind<0) return new int[0];
        return slot==ConvertibleSlot(pools)?pools[0].Concat(pools[2]).Distinct().ToArray():pools[kind];
    }
    public int[] Plan(int[] targets,int[][] pools) {
        if(targets==null || targets.Length!=SlotCapacity) throw new Exception("请为每个未锁定的普通词条选择目标。");
        var editable=Enumerable.Range(0,NativeSlotLimit).Where(Editable).ToArray();
        if(editable.Length==0) throw new Exception("没有可洗牌的词条，请在游戏中解除目标词条的锁定后重新读取。");
        foreach(int slot in editable) {
            int kind=Kind(slot,pools);
            if(kind<0 || !Allowed(slot,pools).Contains(targets[slot])) throw new Exception("第 "+(slot+1)+" 条目标不属于该槽位允许的默认词条池。");
        }
        for(int i=0;i<SlotCapacity;i++) if(!Editable(i) && targets[i]!=(int)Get("enchant"+i)) throw new Exception("固定、符文、锁定或空槽位不能修改。");
        var final=targets.Where(id=>id>0).ToArray();
        if(final.Distinct().Count()!=final.Length) throw new Exception("预选结果包含重复词条，请调整后再准备洗牌。");
        // The game fills free slots in epic, major, minor order. Keep that ordering,
        // including equipment whose original enchant slots have a different order.
        var ordered=new[]{2,0,1}.SelectMany(kind=>editable.Where(slot=>Array.FindIndex(pools,p=>p.Contains(targets[slot]))==kind)).ToArray();
        var free=Enumerable.Range(0,NativeSlotLimit).Where(i=>Editable(i) || (Get("enchant"+i)==0 && Get("enchant_solid"+i)==0 && Get("enchant_rune"+i)==0 && Get("enchant_locked"+i)==0)).ToArray();
        if(free.Length<ordered.Length) throw new Exception("装备可用词条槽位不足。");
        var plan=Enumerable.Repeat(-1,SlotCapacity).ToArray();
        for(int n=0;n<ordered.Length;n++) plan[free[n]]=targets[ordered[n]];
        return plan;
    }
}
