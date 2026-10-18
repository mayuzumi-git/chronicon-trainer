using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public sealed partial class Engine {
    public long EnchantData; public bool EnchantInstalled; byte[][] enchantPatches;
    readonly Dictionary<string,long> enchantHelpers=new Dictionary<string,long>();
    long Helper(string key,byte[] code) {
        long rva; if(enchantHelpers.TryGetValue(key,out rva)) return rva;
        long address=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)4096,0x3000,4).ToInt64(); Check(address!=0); Write(address,code);
        uint old; Check(VirtualProtectEx(handle,new IntPtr(address),(UIntPtr)4096,0x20,out old)); Check(FlushInstructionCache(handle,new IntPtr(address),(UIntPtr)code.Length));
        rva=address-Base; inventoryHelpers.Add(rva); enchantHelpers.Add(key,rva); return rva;
    }
    static byte[] JoinCode(params byte[][] parts) { return parts.SelectMany(x=>x).ToArray(); }
    static byte[] Hex(string s) { return Enumerable.Range(0,s.Length/2).Select(i=>Convert.ToByte(s.Substring(i*2,2),16)).ToArray(); }
    public static double Number(byte[] b) {
        int kind=BitConverter.ToInt32(b,12)&0xffffff;
        if(kind==0 || kind==13) return BitConverter.ToDouble(b,0);
        if(kind==7) return BitConverter.ToInt32(b,0);
        if(kind==10) return BitConverter.ToInt64(b,0);
        throw new Exception("游戏返回的值不是数字。");
    }
    long GlobalAddress(string name) {
        var descriptors=new Dictionary<string,long>{{"enchanterItem",0x39D1B38},{"enchanterItemInv",0x39D1B48},{"enchantFix",0x3A55DD8},{"enchantSlotCount",0x39D5B18}};
        long descriptor; if(!descriptors.TryGetValue(name,out descriptor)) throw new Exception("未知的附魔数据。");
        long function=Helper("global:"+name,JoinCode(Hex("534883ec204c89c348b9"),BitConverter.GetBytes(Base+0x3A677C8),Hex("488b0948b8"),BitConverter.GetBytes(Base+descriptor),Hex("8b10488b01ff5008488903c7430c0a000000488bc34883c4205bc3")));
        return (long)Number(Invoke(function,1,1));
    }
    public byte[] Global(string name) { return Read(GlobalAddress(name),16); }
    public byte[][] ArrayValues(byte[] value) {
        if((BitConverter.ToInt32(value,12)&0xffffff)!=2) throw new Exception("游戏数据不是数组。");
        long reference=BitConverter.ToInt64(value,0); byte[] header=Read(reference,40); int length=BitConverter.ToInt32(header,36); long data=BitConverter.ToInt64(header,8);
        if(length<0 || length>20000 || data==0) throw new Exception("游戏数组长度无效。");
        byte[] bytes=Read(data,length*16); return Enumerable.Range(0,length).Select(i=>bytes.Skip(i*16).Take(16).ToArray()).ToArray();
    }
    public byte[] Builtin(string name,params byte[][] args) {
        var descriptors=new Dictionary<string,long>{{"ds_list_size",0x39ED878},{"ds_list_find_value",0x39ED648},{"ds_list_find_index",0x39E4A08},{"ds_map_find_value",0x39E8AD8}};
        long descriptor; if(!descriptors.TryGetValue(name,out descriptor)) throw new Exception("未知的附魔查询接口。");
        long function=Helper("builtin:"+name,JoinCode(Hex("4883ec38488b442460488944242849bb"),BitConverter.GetBytes(Base+descriptor),Hex("418b03488944242049bb"),BitConverter.GetBytes(Base+0x2897380),Hex("41ffd34883c438c3")));
        return Invoke(function,1,1,args);
    }
    public string GameString(byte[] value) {
        if((BitConverter.ToInt32(value,12)&0xffffff)!=1) return Number(value).ToString();
        long reference=BitConverter.ToInt64(value,0); byte[] header=Read(reference,16); long chars=BitConverter.ToInt64(header,0); int length=BitConverter.ToInt32(header,12);
        if(length<0 || length>1048576) throw new Exception("游戏文本长度无效。");
        if(length==0) return "";
        byte[] bytes=Read(chars,length); int end=Array.IndexOf(bytes,(byte)0); return Encoding.UTF8.GetString(bytes,0,end<0?bytes.Length:end);
    }
    public EnchantItem ReadEnchantItem() {
        if(!InventoryInstalled) InstallInventory();
        long address=GlobalAddress("enchanterItemInv"); byte[] value=ArrayValues(Read(address,16))[0]; string text=GameString(value);
        if(text.Length==0) throw new Exception("请先在游戏附魔师界面放入一件装备。");
        var item=EnchantItem.Parse(text); item.Reference=BitConverter.ToInt64(value,0); item.GlobalAddress=address;
        item.NativeSlotLimit=(int)Number(Global("enchantSlotCount")); if(item.NativeSlotLimit<1 || item.NativeSlotLimit>EnchantItem.SlotCapacity) throw new Exception("游戏词条槽位上限不匹配。"); return item;
    }
    public void InstallEnchants() {
        if(EnchantInstalled) return;
        InstallInventory();
        int count=EnchantProfile.Sites.Length;
        for(int k=0;k<count;k++) if(!Read(Base+EnchantProfile.Sites[k],EnchantProfile.Originals[k].Length).SequenceEqual(EnchantProfile.Originals[k])) throw new Exception("附魔功能指令不匹配。");
        EnchantData=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)4096,0x3000,4).ToInt64(); Check(EnchantData!=0); Write(EnchantData,new byte[4096]); enchantPatches=new byte[count][];
        long cave=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)8192,0x3000,4).ToInt64(); Check(cave!=0);
        for(int k=0;k<count;k++) { byte[] code=EnchantProfile.Code(Base,EnchantData,k); if(code.Length>1200) throw new Exception("附魔处理器超过分配空间。"); Write(cave+k*1280,code); enchantPatches[k]=Jump(cave+k*1280,EnchantProfile.Originals[k].Length); }
        uint old; Check(VirtualProtectEx(handle,new IntPtr(cave),(UIntPtr)8192,0x20,out old)); Check(FlushInstructionCache(handle,new IntPtr(cave),(UIntPtr)8192));
        Pause(delegate {
            for(int k=0;k<count;k++) if(!Read(Base+EnchantProfile.Sites[k],EnchantProfile.Originals[k].Length).SequenceEqual(EnchantProfile.Originals[k])) throw new Exception("附魔功能指令发生变化。");
            try { for(int k=0;k<count;k++) Patch(Base+EnchantProfile.Sites[k],enchantPatches[k]); EnchantInstalled=true; }
            catch { for(int k=0;k<count;k++) Patch(Base+EnchantProfile.Sites[k],EnchantProfile.Originals[k]); throw; }
        });
    }
    public void DisarmEnchant() { if(EnchantInstalled && Alive) Pause(delegate { Write(EnchantData,new byte[4]); }); }
    public void RestoreEnchants() {
        if(!EnchantInstalled || !Alive) return;
        Pause(delegate {
            Write(EnchantData,new byte[4]);
            for(int k=0;k<EnchantProfile.Sites.Length;k++) { byte[] current=Read(Base+EnchantProfile.Sites[k],EnchantProfile.Originals[k].Length); if(!current.SequenceEqual(enchantPatches[k]) && !current.SequenceEqual(EnchantProfile.Originals[k])) throw new Exception("附魔补丁被其他程序更改。"); }
            for(int k=0;k<EnchantProfile.Sites.Length;k++) Patch(Base+EnchantProfile.Sites[k],EnchantProfile.Originals[k]); EnchantInstalled=false;
        });
    }
    public int[][] EnchantPools(EnchantItem item) {
        InstallEnchants(); var pools=new int[3][]; long[] rolls={0xF7AE40,0xF7BE70,0xF814E0};
        try {
            for(int k=0;k<3;k++) {
                Pause(delegate { Write(EnchantData+52,new byte[4]); Write(EnchantData+56,BitConverter.GetBytes(k)); Write(EnchantData,BitConverter.GetBytes(2)); });
                Invoke(rolls[k],1,1,Value(item.Id)); byte[] state=Read(EnchantData,60); int count=BitConverter.ToInt32(state,52);
                if(BitConverter.ToInt32(state,0)!=0 || count<0 || count>256) throw new Exception("未能读取游戏的可用词条池。");
                byte[] ids=Read(EnchantData+64,count*8); pools[k]=Enumerable.Range(0,count).Select(i=>(int)BitConverter.ToDouble(ids,i*8)).Distinct().ToArray();
            }
        } finally { DisarmEnchant(); }
        var current=ReadEnchantItem(); if(current.Serialized!=item.Serialized) throw new Exception("装备已变化，请重新读取。"); return pools;
    }
    public void ArmEnchants(EnchantItem item,int[] targets,int[][] pools) {
        InstallEnchants(); var current=ReadEnchantItem();
        if(current.Serialized!=item.Serialized) throw new Exception("装备或锁定状态已变化，请重新读取。");
        int[] plan=item.Plan(targets,pools); int mask=0; byte[] table=new byte[EnchantItem.SlotCapacity*32]; int[] counts=new int[3];
        for(int slot=0;slot<EnchantItem.SlotCapacity;slot++) if(plan[slot]>0) {
            int kind=Array.FindIndex(pools,p=>p.Contains(plan[slot]));
            Value(plan[slot]).CopyTo(table,slot*32); BitConverter.GetBytes(kind).CopyTo(table,slot*32+16); counts[kind]++; mask|=1<<slot;
        }
        Pause(delegate {
            Write(EnchantData,new byte[4]); Write(EnchantData+4,BitConverter.GetBytes(mask)); Write(EnchantData+24,BitConverter.GetBytes(current.Reference)); Write(EnchantData+32,BitConverter.GetBytes(current.GlobalAddress)); Write(EnchantData+40,BitConverter.GetBytes(1)); Write(EnchantData+44,new byte[4]); Write(EnchantData+2560,table);
            for(int kind=0;kind<3;kind++) Write(EnchantData+3072+kind*16,Value(counts[kind]));
            Write(EnchantData,BitConverter.GetBytes(1));
        });
    }
    public int EnchantStatus { get { return EnchantInstalled?BitConverter.ToInt32(Read(EnchantData+40,4),0):0; } }
    public int EnchantAppliedMask { get { return EnchantInstalled?BitConverter.ToInt32(Read(EnchantData+44,4),0):0; } }
}
