using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

public sealed partial class Engine {
    readonly HashSet<long> inventoryHelpers=new HashSet<long>();
    public long InventoryData; byte[] inventoryPatch;
    public bool InventoryInstalled; public bool InventoryBusy;
    public static byte[] Value(double n) { byte[] b=new byte[16]; BitConverter.GetBytes(n).CopyTo(b,0); return b; }
    public void InstallInventory() {
        if(InventoryInstalled) return;
        if(!Read(Base+InventoryProfile.Entry,14).SequenceEqual(InventoryProfile.Original)) throw new Exception("物品功能的游戏指令不匹配。");
        InventoryData=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)4096,0x3000,4).ToInt64(); Check(InventoryData!=0);
        long cave=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)4096,0x3000,0x40).ToInt64(); Check(cave!=0);
        byte[] initial=new byte[4096];
        for(int i=0;i<8;i++) BitConverter.GetBytes(InventoryData+512+i*16).CopyTo(initial,128+i*8);
        BitConverter.GetBytes(5).CopyTo(initial,256+12); BitConverter.GetBytes(5).CopyTo(initial,272+12);
        Write(InventoryData,initial); Write(cave,InventoryProfile.Code(Base,InventoryData));
        uint ignored; Check(VirtualProtectEx(handle,new IntPtr(cave),(UIntPtr)4096,0x20,out ignored)); Check(FlushInstructionCache(handle,new IntPtr(cave),(UIntPtr)4096));
        inventoryPatch=Jump(cave,14);
        Pause(delegate { if(!Read(Base+InventoryProfile.Entry,14).SequenceEqual(InventoryProfile.Original)) throw new Exception("物品功能指令发生变化。"); Patch(Base+InventoryProfile.Entry,inventoryPatch); InventoryInstalled=true; });
    }
    public void CancelInventory() { if(InventoryInstalled && Alive) Pause(delegate { Write(InventoryData+28,BitConverter.GetBytes(1)); if(BitConverter.ToInt32(Read(InventoryData,4),0)==1) Write(InventoryData,BitConverter.GetBytes(3)); }); }
    public void RestoreInventory() {
        if(!InventoryInstalled || !Alive) return;
        Pause(delegate {
            Write(InventoryData+28,BitConverter.GetBytes(1)); Write(InventoryData,BitConverter.GetBytes(0));
            byte[] current=Read(Base+InventoryProfile.Entry,14);
            if(!current.SequenceEqual(inventoryPatch) && !current.SequenceEqual(InventoryProfile.Original)) throw new Exception("物品功能补丁已被其他程序更改。");
            Patch(Base+InventoryProfile.Entry,InventoryProfile.Original); InventoryInstalled=false;
        });
    }
    static readonly long[] AllowedCalls={0xEB0380,0xF51130,0xF4D9F0,0x12997F0,0x129B3B0,0x69B140,0x920F90,0x921960,0xEA0DC0,0xF7AE40,0xF7BE70,0xF814E0,0x170CB00,0xF55B50};
    public byte[] Invoke(long function,int action,int count,params byte[][] args) {
        if(!InventoryInstalled) throw new Exception("请先连接物品功能。");
        if((!AllowedCalls.Contains(function) && !inventoryHelpers.Contains(function)) || args.Length>8 || count<1 || count>1000 || (action!=1 && action!=2)) throw new Exception("物品请求无效。");
        if(InventoryBusy) throw new Exception("上一项物品请求尚未完成。"); InventoryBusy=true;
        try {
            Pause(delegate {
                Write(InventoryData,BitConverter.GetBytes(0)); Write(InventoryData+4,BitConverter.GetBytes(action)); Write(InventoryData+8,BitConverter.GetBytes(Base+function));
                Write(InventoryData+16,BitConverter.GetBytes(args.Length)); Write(InventoryData+20,BitConverter.GetBytes(count)); Write(InventoryData+24,new byte[8]);
                for(int i=0;i<args.Length;i++) { if(args[i].Length!=16) throw new Exception("参数长度错误。"); Write(InventoryData+512+i*16,args[i]); }
                Write(InventoryData,BitConverter.GetBytes(1));
            });
            var clock=Stopwatch.StartNew();
            while(BitConverter.ToInt32(Read(InventoryData,4),0)!=3) {
                if(!Alive) throw new Exception("游戏已退出。");
                if(clock.Elapsed.TotalSeconds>120) { CancelInventory(); throw new Exception("游戏尚未执行请求。请进入角色、切回游戏后重试。"); }
                Thread.Sleep(10);
            }
            return Read(InventoryData+32,16);
        } finally { InventoryBusy=false; }
    }
    public double Query(long function,double id) {
        byte[] b=Invoke(function,1,1,Value(id)); int kind=BitConverter.ToInt32(b,12)&0xffffff;
        if(kind==0 || kind==13) return BitConverter.ToDouble(b,0);
        if(kind==7) return BitConverter.ToInt32(b,0);
        if(kind==10) return BitConverter.ToInt64(b,0);
        return Double.NaN;
    }
    public void AddEquipment(int id,int count,int level) { if(id==731)throw new Exception("法典需要通过游戏配方绑定技能，不能使用通用物品生成，请在游戏中合成。");Invoke(0xEA0DC0,2,count,Value(id),Value(level),Value(-1)); }
    public void AddMaterial(int id,long count) { if(count<1 || count>1000000000) throw new Exception("数量须为 1 至 1,000,000,000。"); Invoke(0x12997F0,1,1,Value(id),Value(count)); }
    public double ReadMaterialCount(int id) {
        bool abstractMaterial=Query(0xF4D9F0,id)>0.5;
        if(BitConverter.ToInt32(Read(InventoryData+28,4),0)!=0) return Double.NaN;
        return abstractMaterial?Query(0x69B140,id):Number(Invoke(0x170CB00,1,1,Value(0),Value(id),Value(-4)));
    }
    public bool DropMaterial(int id,long count) {
        if(!ItemCatalog.Materials.Any(x=>x.Id==id)) throw new Exception("材料种类无效。");
        if(count<1 || count>1000000000) throw new Exception("数量须为 1 至 1,000,000,000。");
        bool abstractMaterial=Query(0xF4D9F0,id)>0.5;
        if(BitConverter.ToInt32(Read(InventoryData+28,4),0)!=0) return false;
        // Abstract resources use the level field as their drop amount; inventory materials use quantity.
        long function=Helper("material-drop",MaterialDropProfile.Code(Base));
        Invoke(function,2,1,Value(id),Value(abstractMaterial?count:1),Value(-1),Value(count));
        return BitConverter.ToInt32(Read(InventoryData+24,4),0)==1;
    }
    public void AddGem(int id,long count) { if(!ItemCatalog.Gems.Any(x=>x.Id==id)) throw new Exception("宝石种类无效。"); AddMaterial(id,count); }
    public void AddBlankRunestones(int count) { Invoke(0xEA0DC0,2,count,Value(741),Value(1),Value(-1)); }
    public void AddDroppedLesserRune(int id,int count) {
        if(!RuneCatalog.DroppedLesser.Any(x=>x.Id==id)) throw new Exception("请选择掉落池中的次级符文。");
        if(count<1 || count>1000) throw new Exception("单次符文数量须为 1 至 1,000。");
        Invoke(0xF55B50,2,count,Value(id));
    }
    public void AddCrystals(long count) {
        if(count<1 || count>1000000000) throw new Exception("数量须为 1 至 1,000,000,000。");
        Invoke(0x920F90,1,1,Read(Base+0x3DC8670,16),Value(count));
    }
}
