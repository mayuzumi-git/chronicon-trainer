using System;
using System.Linq;

public sealed partial class Engine {
    public bool PickupInstalled; public long PickupData; byte[] pickupPatch;
    public double? PickupCurrent; public double PickupNatural;
    byte[] PickupCall(bool write,params byte[][] args) {
        long function=Helper(write?"pickup:set":"pickup:get",Jump(Base+(write?0x1C65510:0x1C634F0),14));
        byte[] result=Invoke(function,1,1,args);
        if(BitConverter.ToInt32(Read(InventoryData+28,4),0)!=0) throw new OperationCanceledException();
        return result;
    }
    public void InstallPickup() {
        if(PickupInstalled) return;
        if(!Read(Base+PickupProfile.Entry,PickupProfile.Original.Length).SequenceEqual(PickupProfile.Original)) throw new Exception("拾取范围指令不匹配。");
        InstallInventory();
        PickupData=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)4096,0x3000,4).ToInt64();Check(PickupData!=0);
        long cave=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)4096,0x3000,4).ToInt64();Check(cave!=0);
        byte[] initial=new byte[4096];
        for(int i=0;i<3;i++) BitConverter.GetBytes(PickupData+256+i*16).CopyTo(initial,128+i*8);
        Value(36).CopyTo(initial,272);BitConverter.GetBytes(5).CopyTo(initial,332);BitConverter.GetBytes(5).CopyTo(initial,348);
        Write(PickupData,initial);Write(cave,PickupProfile.Code(Base,PickupData));
        uint old;Check(VirtualProtectEx(handle,new IntPtr(cave),(UIntPtr)4096,0x20,out old));Check(FlushInstructionCache(handle,new IntPtr(cave),(UIntPtr)4096));
        pickupPatch=Jump(cave,PickupProfile.Original.Length);
        Pause(delegate { if(!Read(Base+PickupProfile.Entry,PickupProfile.Original.Length).SequenceEqual(PickupProfile.Original)) throw new Exception("拾取范围指令发生变化。");Patch(Base+PickupProfile.Entry,pickupPatch);PickupInstalled=true; });
    }
    public void RefreshPickup() {
        PickupCurrent=null;if(!PickupInstalled) return;
        byte[] data=Read(PickupData,48);PickupNatural=BitConverter.ToDouble(data,16)*100;
        if(BitConverter.ToInt32(data,24)!=0) PickupCurrent=BitConverter.ToDouble(data,40)*100;
    }
    public void SetPickup(double? percent) {
        if(percent.HasValue && (Double.IsNaN(percent.Value)||Double.IsInfinity(percent.Value)||percent.Value<0||percent.Value>1000000)) throw new Exception("拾取范围须为 0 至 1,000,000 之间的百分比。");
        if(!percent.HasValue && !PickupInstalled) return;
        InstallPickup();
        double current=Number(PickupCall(false,Value(0),Value(36))),target=current;bool change=false;
        Pause(delegate {
            byte[] data=Read(PickupData,32);bool enabled=BitConverter.ToInt32(data,0)!=0,valid=BitConverter.ToInt32(data,24)!=0;double previous=BitConverter.ToDouble(data,8);
            if(percent.HasValue) {
                if(!enabled || !valid || current!=previous) { Write(PickupData+16,BitConverter.GetBytes(current));Write(PickupData+24,BitConverter.GetBytes(1)); }
                target=percent.Value/100;Write(PickupData+8,BitConverter.GetBytes(target));Write(PickupData,BitConverter.GetBytes(1));change=true;
            } else {
                Write(PickupData,new byte[4]);
                if(enabled && valid && current==previous) { target=BitConverter.ToDouble(data,16);change=true; }
            }
        });
        if(change) PickupCall(true,Value(0),Value(36),Value(target));
        Write(PickupData+40,BitConverter.GetBytes(target));RefreshPickup();
    }
    public void RestorePickup() {
        if(!PickupInstalled) return;
        if(Alive) {
            if(!Read(Base+PickupProfile.Entry,pickupPatch.Length).SequenceEqual(pickupPatch)) throw new Exception("拾取范围补丁被其他程序修改，请重启游戏。");
            SetPickup(null);
            Pause(delegate { Write(PickupData,new byte[4]);Patch(Base+PickupProfile.Entry,PickupProfile.Original); });
        }
        PickupInstalled=false;PickupCurrent=null;
    }
}
