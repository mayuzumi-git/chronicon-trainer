using System;
using System.Linq;

public sealed partial class Engine {
    public bool KillMaterialsInstalled,KillMaterialsEnabled;public long KillMaterialData;
    public int KillMaterialMultiplier=1;public ulong KillMaterialCalls,KillMaterialDrops;byte[] killMaterialPatch;
    public void SetKillMaterials(bool enabled,int multiplier) {
        if(multiplier<1||multiplier>1000)throw new Exception("材料数量倍率须为 1 至 1,000 的整数。");
        if(!enabled&&!KillMaterialsInstalled)return;
        if(!KillMaterialsInstalled) {
            if(!Read(Base+KillMaterialProfile.Entry,KillMaterialProfile.Original.Length).SequenceEqual(KillMaterialProfile.Original))throw new Exception("击杀材料掉落指令不匹配。");
            KillMaterialData=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)4096,0x3000,4).ToInt64();Check(KillMaterialData!=0);
            long cave=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)4096,0x3000,4).ToInt64();Check(cave!=0);
            byte[] initial=new byte[4096];int[] enemies=MapCatalog.Enemies.Where(x=>x!=484).ToArray();BitConverter.GetBytes(enemies.Length).CopyTo(initial,32);
            for(int i=0;i<enemies.Length;i++)BitConverter.GetBytes(enemies[i]).CopyTo(initial,512+i*4);
            Write(KillMaterialData,initial);Write(cave,KillMaterialProfile.Code(Base,KillMaterialData,cave+2048));Write(cave+2048,MaterialDropProfile.Code(Base));
            uint ignored;Check(VirtualProtectEx(handle,new IntPtr(cave),(UIntPtr)4096,0x20,out ignored));Check(FlushInstructionCache(handle,new IntPtr(cave),(UIntPtr)4096));killMaterialPatch=Jump(cave,KillMaterialProfile.Original.Length);
            Pause(delegate {if(!Read(Base+KillMaterialProfile.Entry,KillMaterialProfile.Original.Length).SequenceEqual(KillMaterialProfile.Original))throw new Exception("击杀材料掉落指令发生变化。");Patch(Base+KillMaterialProfile.Entry,killMaterialPatch);KillMaterialsInstalled=true;});
        }
        Pause(delegate {Write(KillMaterialData+8,BitConverter.GetBytes((double)multiplier));Write(KillMaterialData,BitConverter.GetBytes(enabled?1:0));});KillMaterialsEnabled=enabled;KillMaterialMultiplier=multiplier;
    }
    public void RefreshKillMaterials() {if(!KillMaterialsInstalled)return;byte[] b=Read(KillMaterialData+16,16);KillMaterialCalls=BitConverter.ToUInt64(b,0);KillMaterialDrops=BitConverter.ToUInt64(b,8);}
    public void RestoreKillMaterials() {
        if(!KillMaterialsInstalled)return;
        if(Alive)Pause(delegate {if(!Read(Base+KillMaterialProfile.Entry,killMaterialPatch.Length).SequenceEqual(killMaterialPatch))throw new Exception("击杀材料补丁被其他程序修改，请重启游戏。");Write(KillMaterialData,new byte[4]);Patch(Base+KillMaterialProfile.Entry,KillMaterialProfile.Original);});
        KillMaterialsInstalled=KillMaterialsEnabled=false;KillMaterialCalls=KillMaterialDrops=0;
    }
}
