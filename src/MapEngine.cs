using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

public sealed class MapTarget {
    public int Id,Index; public long Instance,Hp,Death,Ready; public string Name; public bool Hidden; public float X,Y; public double Distance;
}

public sealed partial class Engine {
    public bool MapInstalled,MapEnabled; public bool MapAutoReveal { get; private set; } public long MapData; public string MapArea="",MapMessage="尚未开启地图功能。";
    public double MapReadMilliseconds; public int MapFullScans; public List<MapTarget> MapTargets=new List<MapTarget>();
    readonly Dictionary<string,int> mapKeys=new Dictionary<string,int>();
    readonly Dictionary<string,long> mapGlobals=new Dictionary<string,long>();
    readonly HashSet<int> mapEnemyTypes=new HashSet<int>(MapCatalog.Enemies);
    long mapGlobalObject,mapGlobalTable,mapRoom,mapPlayer; byte[] mapPatch,spawnPatch,mapSurface,mapExplore; string mapSignature="",mapPublished="";
    ulong mapSeenSpawn; bool mapNeedsScan=true,mapSettling,mapAutoFogQueued,mapAutoFogOwnRequest; DateTime mapSettleAt;
    public static bool MapNeedsScan(string previous,string current,ulong previousSpawn,ulong currentSpawn,bool initial,bool settlingDue) {
        return initial || previous!=current || previousSpawn!=currentSpawn || settlingDue;
    }
    public static bool MapIsTrackable(int index) { return index!=484 && Array.IndexOf(MapCatalog.Enemies,index)>=0; }
    public static bool MapShouldAutoReveal(bool enabled,bool queued,bool settled,int pending) { return enabled && !queued && settled && pending!=1 && pending!=2 && pending!=3; }
    public static int MapFogStatusAfterChange(int pending) { return pending==1?4:pending==2?2:0; }
    int MapKey(string name) { return mapKeys[name]; }
    static long Q(byte[] b,int offset) { return BitConverter.ToInt64(b,offset); }
    static int I(byte[] b,int offset) { return BitConverter.ToInt32(b,offset); }
    static int Kind(byte[] b) { return I(b,12)&0xffffff; }
    Dictionary<int,long> MapVariables(long instance,HashSet<int> wanted) {
        long map=Q(Read(instance+8,8),0); var values=new Dictionary<int,long>(); if(map==0) return values;
        byte[] h=Read(map,24); int capacity=I(h,0),count=I(h,4); long entries=Q(h,16);
        if(count==0) return values;
        if(capacity<1 || capacity>16384 || count>capacity || entries==0) throw new InvalidOperationException("地图数据正在初始化。");
        byte[] table=Read(entries,(capacity+1)*16);
        for(int k=0;k<=capacity;k++) { int hash=I(table,k*16+12),key=I(table,k*16+8); if(hash>0 && wanted.Contains(key)) values[key]=Q(table,k*16); }
        return values;
    }
    void MapResolveGlobals() {
        long obj=Q(Read(Base+0x3A677C8,8),0); if(obj==0) throw new InvalidOperationException("地图尚未载入。");
        long map=Q(Read(obj+8,8),0); if(map==0) throw new InvalidOperationException("地图尚未载入。");
        long table=Q(Read(map+16,8),0);
        if(obj==mapGlobalObject && table==mapGlobalTable && mapGlobals.Count==4) return;
        string[] names={"areaName","minimapicons","mapexplore","DUNGEON"};
        var fields=MapVariables(obj,new HashSet<int>(names.Select(n=>MapKey(n)))); mapGlobals.Clear();
        foreach(string n in names) { long p; if(!fields.TryGetValue(MapKey(n),out p)) throw new InvalidOperationException("地图变量尚未就绪。"); mapGlobals[n]=p; }
        mapGlobalObject=obj;mapGlobalTable=table;
    }
    string MapReadState(out long room,out byte[] surface,out byte[] explore,out string area) {
        MapResolveGlobals(); byte[] name=Read(mapGlobals["areaName"],16);surface=Read(mapGlobals["minimapicons"],16);explore=Read(mapGlobals["mapexplore"],16);
        byte[] dungeon=Read(mapGlobals["DUNGEON"],16);room=Q(Read(Base+0x3D86AC0,8),0);
        if(room==0 || Kind(name)!=1 || Kind(surface)!=15 || Kind(explore)!=15 || Kind(dungeon)!=15) throw new InvalidOperationException("地图图层正在初始化。");
        area=GameString(name);
        return room.ToString("x")+":"+area+":"+Q(surface,0).ToString("x")+":"+Q(explore,0).ToString("x")+":"+Q(dungeon,0).ToString("x");
    }
    public void InstallMap() {
        if(MapInstalled) return;
        if(!Read(Base+MapProfile.MapSite,MapProfile.MapOriginal.Length).SequenceEqual(MapProfile.MapOriginal) || !Read(Base+MapProfile.SpawnSite,MapProfile.SpawnOriginal.Length).SequenceEqual(MapProfile.SpawnOriginal))
            throw new Exception("地图指令已被修改。请先关闭旧版独特敌人标记工具。");
        mapKeys.Clear(); foreach(var pair in MapCatalog.Descriptors) mapKeys[pair.Key]=I(Read(Base+pair.Value,4),0);
        MapData=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)16384,0x3000,4).ToInt64();Check(MapData!=0);
        long cave=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)8192,0x3000,4).ToInt64();Check(cave!=0);
        byte[] initial=new byte[16384];Value(1).CopyTo(initial,208);BitConverter.GetBytes(5).CopyTo(initial,332);Write(MapData,initial);
        Write(cave,MapProfile.Render(Base,MapData));Write(cave+4096,MapProfile.Spawn(Base,MapData));
        uint ignored;Check(VirtualProtectEx(handle,new IntPtr(cave),(UIntPtr)8192,0x20,out ignored));Check(FlushInstructionCache(handle,new IntPtr(cave),(UIntPtr)8192));
        mapPatch=Jump(cave,MapProfile.MapOriginal.Length);spawnPatch=Jump(cave+4096,MapProfile.SpawnOriginal.Length);
        Pause(delegate {
            if(!Read(Base+MapProfile.MapSite,MapProfile.MapOriginal.Length).SequenceEqual(MapProfile.MapOriginal) || !Read(Base+MapProfile.SpawnSite,MapProfile.SpawnOriginal.Length).SequenceEqual(MapProfile.SpawnOriginal)) throw new Exception("地图指令发生变化。");
            try { Patch(Base+MapProfile.MapSite,mapPatch);Patch(Base+MapProfile.SpawnSite,spawnPatch);MapInstalled=true; }
            catch { Patch(Base+MapProfile.SpawnSite,MapProfile.SpawnOriginal);Patch(Base+MapProfile.MapSite,MapProfile.MapOriginal);throw; }
        });
        mapNeedsScan=true;mapSeenSpawn=0;mapSignature="";mapPublished="";mapGlobalObject=0;mapGlobalTable=0;mapAutoFogQueued=false;mapAutoFogOwnRequest=false;
    }
    public void SetMapTracking(bool enabled) {
        InstallMap();MapEnabled=enabled;if(!enabled) MapTargets.Clear();mapNeedsScan=true;mapPublished="";PollMap();
    }
    public void SetAutomaticMapReveal(bool enabled) {
        InstallMap();MapAutoReveal=enabled;
        if(!enabled && mapAutoFogOwnRequest && I(Read(MapData+240,4),0)==1) { Write(MapData+240,new byte[4]);mapAutoFogQueued=false;mapAutoFogOwnRequest=false; }
        PollMap();
    }
    MapTarget MapCandidate(long p,byte[] h) {
        int index=I(h,0xb8);string hiddenName;bool hidden=MapCatalog.Hidden.TryGetValue(index,out hiddenName);
        if(!hidden && !mapEnemyTypes.Contains(index)) return null;
        var fields=MapVariables(p,new HashSet<int>(new[]{MapKey("isunique"),MapKey("hp"),MapKey("death"),MapKey("name"),MapKey("ready")}));
        long ptr; var target=new MapTarget{Instance=p,Id=I(h,0xb4),Index=index,Hidden=hidden,X=BitConverter.ToSingle(h,0xec),Y=BitConverter.ToSingle(h,0xf0)};
        if(hidden) {
            if(fields.TryGetValue(MapKey("ready"),out ptr)) { target.Ready=ptr; if(Number(Read(ptr,16))==0) return null; }
            target.Name=hiddenName;return target;
        }
        if(!fields.TryGetValue(MapKey("isunique"),out ptr) || Number(Read(ptr,16))==0) return null;
        if(!fields.TryGetValue(MapKey("hp"),out target.Hp) || Number(Read(target.Hp,16))<=0) return null;
        if(fields.TryGetValue(MapKey("death"),out target.Death) && Number(Read(target.Death,16))!=0) return null;
        target.Name=fields.TryGetValue(MapKey("name"),out ptr)?GameString(Read(ptr,16)):"独特敌人";return target;
    }
    List<MapTarget> MapScan(long room) {
        var found=new List<MapTarget>();var seen=new HashSet<long>();long player=0;
        foreach(int offset in new[]{0x88,0xa0}) {
            long p=Q(Read(room+offset,8),0);
            while(p!=0) {
                if(!seen.Add(p) || seen.Count>50000) throw new InvalidOperationException("地图实例正在切换。");
                byte[] h=Read(p,0x1a8);long current=p;p=Q(h,0x198);int flags=I(h,0xb0),index=I(h,0xb8);
                if((flags&0x100001)!=0) continue;
                if(index==MapCatalog.Player) player=current;
                MapTarget target=MapCandidate(current,h);if(target!=null) found.Add(target);
            }
        }
        mapPlayer=player;MapFullScans++;return found;
    }
    bool MapRefreshTarget(MapTarget target) {
        byte[] h=Read(target.Instance,0x1a8);
        if(I(h,0xb4)!=target.Id || I(h,0xb8)!=target.Index || (I(h,0xb0)&0x100001)!=0) return false;
        target.X=BitConverter.ToSingle(h,0xec);target.Y=BitConverter.ToSingle(h,0xf0);
        if(target.Hidden) {
            if(target.Ready==0) { long p;var vars=MapVariables(target.Instance,new HashSet<int>(new[]{MapKey("ready")}));if(vars.TryGetValue(MapKey("ready"),out p)) target.Ready=p; }
            return target.Ready==0 || Number(Read(target.Ready,16))!=0;
        }
        return Number(Read(target.Hp,16))>0 && (target.Death==0 || Number(Read(target.Death,16))==0);
    }
    void MapPublish(long room,byte[] surface,byte[] explore) {
        var selected=MapEnabled?MapTargets.Take(32).ToArray():new MapTarget[0];
        string fingerprint=mapSignature+":"+String.Join(";",selected.Select(t=>t.Id+":"+Math.Round(t.X,1)+":"+Math.Round(t.Y,1)+":"+t.Hidden));
        if(fingerprint==mapPublished) return;
        var bytes=new List<byte>();foreach(var target in selected) {
            double col=target.Hidden?0x00a5ff:0xff40ff;
            foreach(var ring in new[]{new[]{3.5,16777215.0},new[]{2.3,col}}) foreach(double n in new[]{target.X/8.0,target.Y/8.0,ring[0],ring[1],ring[1],0}) bytes.AddRange(Value(n));
        }
        // Only publication of a small marker buffer is paused; map reads never pause the game.
        Pause(delegate {
            Write(MapData,new byte[8]);if(bytes.Count>0) Write(MapData+512,bytes.ToArray());Write(MapData+8,BitConverter.GetBytes(room));Write(MapData+224,surface);Write(MapData+256,explore);
            Write(MapData,BitConverter.GetBytes(1));Write(MapData+4,BitConverter.GetBytes(selected.Length*2));
        });mapPublished=fingerprint;
    }
    public void PollMap() {
        if(!MapInstalled || !Alive) return;var clock=Stopwatch.StartNew();
        try {
            long room;byte[] surface,explore;string area;string signature=MapReadState(out room,out surface,out explore,out area);
            ulong spawned=BitConverter.ToUInt64(Read(MapData+24,8),0);bool changed=signature!=mapSignature;
            if(changed) { Write(MapData,new byte[8]);Write(MapData+240,BitConverter.GetBytes(MapFogStatusAfterChange(I(Read(MapData+240,4),0))));MapTargets.Clear();mapNeedsScan=true;mapSettling=true;mapSettleAt=DateTime.UtcNow.AddSeconds(1);mapPublished="";mapAutoFogQueued=false;mapAutoFogOwnRequest=false; }
            bool settlingDue=mapSettling && DateTime.UtcNow>=mapSettleAt;
            if(MapEnabled && MapNeedsScan(mapSignature,signature,mapSeenSpawn,spawned,mapNeedsScan,settlingDue)) {
                var candidates=MapScan(room);
                long verifyRoom;byte[] verifySurface,verifyExplore;string verifyArea;
                if(MapReadState(out verifyRoom,out verifySurface,out verifyExplore,out verifyArea)!=signature) throw new InvalidOperationException("地图正在切换。");
                MapTargets=candidates;mapNeedsScan=false;mapSeenSpawn=spawned;if(settlingDue) mapSettling=false;
            } else if(MapEnabled) {
                var live=new List<MapTarget>();foreach(var t in MapTargets) { try { if(MapRefreshTarget(t)) live.Add(t); } catch(System.ComponentModel.Win32Exception) { } }
                MapTargets=live;
            }
            mapSignature=signature;mapRoom=room;mapSurface=surface;mapExplore=explore;MapArea=area;
            if(mapPlayer!=0 && MapEnabled) { byte[] player=Read(mapPlayer,0x1a8);if(I(player,0xb8)==MapCatalog.Player) { double x=BitConverter.ToSingle(player,0xec),y=BitConverter.ToSingle(player,0xf0);foreach(var t in MapTargets) t.Distance=Math.Sqrt((t.X-x)*(t.X-x)+(t.Y-y)*(t.Y-y)); } }
            MapPublish(room,surface,explore);int fog=I(Read(MapData+240,4),0);
            if(fog==1 || fog==2 || fog==3) mapAutoFogQueued=true;
            if(fog==3 || fog==4) mapAutoFogOwnRequest=false;
            if(MapShouldAutoReveal(MapAutoReveal,mapAutoFogQueued,DateTime.UtcNow>=mapSettleAt,fog)) {
                QueueMapFog();mapAutoFogQueued=true;mapAutoFogOwnRequest=true;fog=1;
            }
            if(fog==3) MapMessage="当前地图全部迷雾已点亮。";else if(fog==4) MapMessage="地图已切换，未执行上张地图的点亮请求。";else if(fog==1) MapMessage="等待游戏自然刷新地图后点亮迷雾……";
            else MapMessage=MapEnabled?"已跟踪 "+MapTargets.Count+" 个目标。":"独特敌人标记已关闭，可使用去迷雾。";
        } catch(System.ComponentModel.Win32Exception) { MapWaiting(); }
        catch(InvalidOperationException) { MapWaiting(); }
        finally { MapReadMilliseconds=clock.Elapsed.TotalMilliseconds; }
    }
    void MapWaiting() { MapMessage="地图载入中，等待初始化……";MapTargets.Clear();mapNeedsScan=true;mapPublished="";if(Alive) Write(MapData,new byte[8]); }
    public void RevealMapFog() {
        InstallMap();PollMap();QueueMapFog();mapAutoFogQueued=true;
    }
    void QueueMapFog() {
        if(mapSurface==null || mapExplore==null || mapPublished.Length==0) throw new InvalidOperationException("请进入角色并等待地图载入完成。");
        long room;byte[] surface,explore;string area;
        if(MapReadState(out room,out surface,out explore,out area)!=mapSignature) throw new InvalidOperationException("地图正在切换，请稍后重试。");
        int pending=I(Read(MapData+240,4),0);if(pending==1 || pending==2) throw new Exception("点亮请求已提交，请切回游戏等待地图自然刷新。");
        Write(MapData+256,explore);Write(MapData+240,BitConverter.GetBytes(1));MapMessage="等待游戏自然刷新地图后点亮迷雾……";
    }
    public void RestoreMap() {
        if(!MapInstalled) return;
        if(Alive) Pause(delegate {
            Write(MapData,new byte[8]);Write(MapData+240,new byte[4]);
            if(!Read(Base+MapProfile.MapSite,mapPatch.Length).SequenceEqual(mapPatch) || !Read(Base+MapProfile.SpawnSite,spawnPatch.Length).SequenceEqual(spawnPatch)) throw new Exception("地图补丁被其他程序修改，请重启游戏。");
            Patch(Base+MapProfile.SpawnSite,MapProfile.SpawnOriginal);Patch(Base+MapProfile.MapSite,MapProfile.MapOriginal);
        });MapInstalled=false;MapEnabled=false;MapAutoReveal=false;mapAutoFogQueued=false;mapAutoFogOwnRequest=false;MapTargets.Clear();MapMessage="地图补丁已恢复。";mapGlobals.Clear();
    }
}
