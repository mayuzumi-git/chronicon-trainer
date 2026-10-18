using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

public sealed partial class Engine : IDisposable {
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr done);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr done);
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr a,UIntPtr n,uint type,uint protect);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool VirtualProtectEx(IntPtr h,IntPtr a,UIntPtr n,uint protect,out uint old);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool FlushInstructionCache(IntPtr h,IntPtr a,UIntPtr n);
    [DllImport("ntdll.dll")] static extern int NtSuspendProcess(IntPtr h);
    [DllImport("ntdll.dll")] static extern int NtResumeProcess(IntPtr h);
    IntPtr handle; Process process; public long Base,Data; public bool Installed; public int Pid { get { return process.Id; } }
    public bool Alive { get { return !process.HasExited; } }
    byte[] mfPatch,qPatch;
    public Engine() {
        Process[] found=Process.GetProcessesByName("Chronicon");
        if(found.Length!=1) throw new Exception("请先启动一个 Chronicon 游戏进程。");
        process=found[0];
        using(var sha=SHA256.Create()) using(var stream=File.OpenRead(process.MainModule.FileName)) {
            string digest=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            if(digest!=Profile.Hash) throw new Exception("游戏版本不匹配，未写入游戏。此修改器仅支持已分析的版本。");
        }
        Base=process.MainModule.BaseAddress.ToInt64();
        handle=OpenProcess(0x838,false,process.Id); Check(handle!=IntPtr.Zero);
        try {
            if(!Read(Base+Profile.MfRva,Profile.MfOriginal.Length).SequenceEqual(Profile.MfOriginal) ||
               !Read(Base+Profile.QualityRva,Profile.QualityOriginal.Length).SequenceEqual(Profile.QualityOriginal))
                throw new Exception("游戏指令不匹配。可能已有其他修改器运行，请关闭其他修改器或重启游戏。");
        } catch { CloseHandle(handle); handle=IntPtr.Zero; throw; }
    }
    static void Check(bool ok) { if(!ok) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
    public byte[] Read(long a,int n) { byte[] b=new byte[n]; UIntPtr done; Check(ReadProcessMemory(handle,new IntPtr(a),b,(UIntPtr)n,out done)); if(done.ToUInt64()!=(ulong)n) throw new Exception("读取不完整。"); return b; }
    void Write(long a,byte[] b) { UIntPtr done; Check(WriteProcessMemory(handle,new IntPtr(a),b,(UIntPtr)b.Length,out done)); if(done.ToUInt64()!=(ulong)b.Length) throw new Exception("写入不完整。"); }
    void Pause(Action fn) { if(NtSuspendProcess(handle)<0) throw new Exception("无法暂停游戏以安全更新补丁。"); try { fn(); } finally { if(NtResumeProcess(handle)<0) throw new Exception("游戏进程恢复失败，请重启游戏。"); } }
    void Patch(long a,byte[] b) { uint old; Check(VirtualProtectEx(handle,new IntPtr(a),(UIntPtr)b.Length,0x40,out old)); try { Write(a,b); if(!Read(a,b.Length).SequenceEqual(b)) throw new Exception("补丁校验失败。"); Check(FlushInstructionCache(handle,new IntPtr(a),(UIntPtr)b.Length)); } finally { uint ignored; Check(VirtualProtectEx(handle,new IntPtr(a),(UIntPtr)b.Length,old,out ignored)); } }
    static byte[] Jump(long address,int length) { byte[] b=Enumerable.Repeat((byte)0x90,length).ToArray(); Array.Copy(new byte[]{255,37,0,0,0,0},b,6); Array.Copy(BitConverter.GetBytes(address),0,b,6,8); return b; }
    public void Install() {
        if(Installed) return;
        Data=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)4096,0x3000,4).ToInt64(); Check(Data!=0);
        long cave=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)4096,0x3000,0x40).ToInt64(); Check(cave!=0);
        Write(Data,new byte[96]); Write(cave,Profile.MfCode(Base,Data)); Write(cave+512,Profile.QualityCode(Base,Data));
        uint old; Check(VirtualProtectEx(handle,new IntPtr(cave),(UIntPtr)4096,0x20,out old)); Check(FlushInstructionCache(handle,new IntPtr(cave),(UIntPtr)4096));
        mfPatch=Jump(cave,Profile.MfOriginal.Length); qPatch=Jump(cave+512,Profile.QualityOriginal.Length);
        Pause(delegate {
            if(!Read(Base+Profile.MfRva,Profile.MfOriginal.Length).SequenceEqual(Profile.MfOriginal) || !Read(Base+Profile.QualityRva,Profile.QualityOriginal.Length).SequenceEqual(Profile.QualityOriginal)) throw new Exception("指令已发生变化。");
            try { Patch(Base+Profile.MfRva,mfPatch); Patch(Base+Profile.QualityRva,qPatch); Installed=true; }
            catch { Patch(Base+Profile.MfRva,Profile.MfOriginal); Patch(Base+Profile.QualityRva,Profile.QualityOriginal); throw; }
        });
    }
    public double? Current; public double Natural; public ulong MfCalls,QualityCalls; public long Pointer;
    public void Refresh() {
        if(!Installed) return; byte[] b=Read(Data,80); Pointer=BitConverter.ToInt64(b,32); Natural=BitConverter.ToDouble(b,40); MfCalls=BitConverter.ToUInt64(b,56); QualityCalls=BitConverter.ToUInt64(b,64); Current=null;
        if(Pointer!=0) { byte[] v=Read(Pointer,16); if(BitConverter.ToInt32(v,12)==0) Current=BitConverter.ToDouble(v,0); }
    }
    public void Set(double? mf,double? quality) {
        if(mf.HasValue && (Double.IsNaN(mf.Value)||Double.IsInfinity(mf.Value)||mf.Value<0||mf.Value>1000000)) throw new Exception("寻宝值须为 0 至 1,000,000 之间的数字。");
        Install(); Pause(delegate {
            Write(Data,BitConverter.GetBytes(mf.GetValueOrDefault())); Write(Data+16,BitConverter.GetBytes(quality.GetValueOrDefault())); Write(Data+24,BitConverter.GetBytes(mf.HasValue?1:0)); Write(Data+72,BitConverter.GetBytes(quality.HasValue?1:0));
            Refresh(); if(Current.HasValue) Write(Pointer,BitConverter.GetBytes(mf.HasValue?mf.Value:Natural));
        });
    }
    public void Restore() {
        RestoreMap();
        RestoreEnchants();
        RestoreKillMaterials();
        RestorePickup();
        RestoreInventory();
        if(Installed && Alive) Pause(delegate {
            Refresh(); bool enabled=BitConverter.ToInt32(Read(Data+24,4),0)!=0; double chosen=BitConverter.ToDouble(Read(Data,8),0);
            Write(Data+24,new byte[4]); Write(Data+72,new byte[4]);
            if(enabled && Current.HasValue && Current.Value==chosen) Write(Pointer,BitConverter.GetBytes(Natural));
            byte[] a=Read(Base+Profile.MfRva,Profile.MfOriginal.Length), b=Read(Base+Profile.QualityRva,Profile.QualityOriginal.Length);
            if((!a.SequenceEqual(mfPatch)&&!a.SequenceEqual(Profile.MfOriginal)) || (!b.SequenceEqual(qPatch)&&!b.SequenceEqual(Profile.QualityOriginal))) throw new Exception("补丁被其他程序改动，请重启游戏以恢复。");
            Patch(Base+Profile.QualityRva,Profile.QualityOriginal); Patch(Base+Profile.MfRva,Profile.MfOriginal); Installed=false;
        });
    }
    public void Dispose() { Restore(); if(handle!=IntPtr.Zero) { CloseHandle(handle); handle=IntPtr.Zero; } }
}

public sealed partial class MainWindow : Form {
    [STAThread] public static void Main(string[] args) {
        if(args.Length>0 && args[0]=="--quality-test") { QualityTest(); return; }
        if(args.Length>0 && (args[0]=="--probe" || args[0]=="--test")) { Probe(args[0]=="--test"); return; }
        bool created; using(var mutex=new System.Threading.Mutex(true,"ChroniconMagicFindTrainerLocal",out created)) { if(!created) { MessageBox.Show("修改器已经运行。"); return; } Application.EnableVisualStyles();var window=new MainWindow();if(args.Contains("--map")) window.Shown+=delegate { window.Run(window.OpenMap); };Application.Run(window); }
    }
    static void QualityTest() {
        string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"quality_test.log");
        try {
            using(var t=new Engine()) {
                t.Set(null,4); var clock=Stopwatch.StartNew();
                while(clock.Elapsed.TotalSeconds<60) {
                    System.Threading.Thread.Sleep(500); t.Refresh();
                    File.AppendAllText(path,String.Format("{0:0.0}s mf={1}; legendary_overrides={2}\r\n",clock.Elapsed.TotalSeconds,t.Current,t.QualityCalls));
                    if(t.QualityCalls>=8 && clock.Elapsed.TotalSeconds>=15) break;
                }
                t.Restore();
                if(!t.Read(t.Base+Profile.MfRva,Profile.MfOriginal.Length).SequenceEqual(Profile.MfOriginal) || !t.Read(t.Base+Profile.QualityRva,Profile.QualityOriginal.Length).SequenceEqual(Profile.QualityOriginal)) throw new Exception("恢复回读失败");
            }
            File.AppendAllText(path,"ORIGINAL_INSTRUCTIONS_RESTORED_AND_VERIFIED\r\n");
        } catch(Exception ex) { File.AppendAllText(path,ex.ToString()+"\r\n"); }
    }
    static void Probe(bool test) {
        string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,test?"live_test.log":"native_probe.log");
        try {
            using(var t=new Engine()) {
                t.Install(); var clock=Stopwatch.StartNew(); int stage=0; double changed=0;
                while(clock.Elapsed.TotalSeconds<(test?90:10)) {
                    System.Threading.Thread.Sleep(500); t.Refresh();
                    File.AppendAllText(path,String.Format("{0:0.0}s mf={1}; natural={2}; calls={3}; quality={4}\r\n",clock.Elapsed.TotalSeconds,t.Current,t.Natural,t.MfCalls,t.QualityCalls));
                    if(test && stage==0 && t.Current.HasValue && Math.Abs(t.Current.Value-82)<0.01) { t.Set(125,null); stage=1; changed=clock.Elapsed.TotalSeconds; File.AppendAllText(path,"SET_125\r\n"); }
                    else if(stage==1 && clock.Elapsed.TotalSeconds-changed>=10) { t.Set(999,null); stage=2; changed=clock.Elapsed.TotalSeconds; File.AppendAllText(path,"SET_999\r\n"); }
                    else if(stage==2 && clock.Elapsed.TotalSeconds-changed>=10) { t.Set(82,null); stage=3; changed=clock.Elapsed.TotalSeconds; File.AppendAllText(path,"SET_82\r\n"); }
                    else if(stage==3 && clock.Elapsed.TotalSeconds-changed>=3) break;
                }
                t.Restore();
                if(!t.Read(t.Base+Profile.MfRva,Profile.MfOriginal.Length).SequenceEqual(Profile.MfOriginal) || !t.Read(t.Base+Profile.QualityRva,Profile.QualityOriginal.Length).SequenceEqual(Profile.QualityOriginal)) throw new Exception("恢复回读失败");
            }
            File.AppendAllText(path,"ORIGINAL_INSTRUCTIONS_RESTORED_AND_VERIFIED\r\n");
        } catch(Exception ex) { File.AppendAllText(path,ex.ToString()+"\r\n"); }
    }
}
