using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class KillMaterialNativeTests {
 [DllImport("kernel32.dll")]static extern IntPtr VirtualAlloc(IntPtr a,UIntPtr n,uint t,uint p);
 [DllImport("kernel32.dll")]static extern bool VirtualFree(IntPtr a,UIntPtr n,uint t);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Native();
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate double Random(long maximum);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate IntPtr Script(IntPtr self,IntPtr other,IntPtr result,int count,IntPtr args);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Cleanup(IntPtr value);
 static long module,data,self;static int rolls,drops;static double multiplier;static int mode;static Exception failure;static List<int> ids=new List<int>();
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
 static byte[] Hex(string s){var b=new byte[s.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(s.Substring(i*2,2),16);return b;}
 static byte[] Join(params byte[][] arrays){var b=new List<byte>();foreach(var a in arrays)b.AddRange(a);return b.ToArray();}
 static byte[] Imm(string hex,long n){return Join(Hex(hex),BitConverter.GetBytes(n));}
 static void Put(long a,byte[] b){Marshal.Copy(b,0,new IntPtr(a),b.Length);}
 static byte[] Jump(long p){return Join(Hex("ff2500000000"),BitConverter.GetBytes(p));}
 static double Num(long p){var b=new byte[8];Marshal.Copy(new IntPtr(p),b,0,8);return BitConverter.ToDouble(b,0);}
 static long Arg(IntPtr args,int n){return Marshal.ReadInt64(args,n*8);}
 static double Roll(long maximum){try{Check(maximum==2,"Native irandom(2)");int n=rolls++;return mode==0?n%3:mode==1?0:2;}catch(Exception ex){failure=ex;return 2;}}
 static IntPtr Generate(IntPtr a,IntPtr b,IntPtr r,int count,IntPtr args){try{Check(a.ToInt64()==self&&b.ToInt64()==1234,"Generator context");Check(count==4,"Generator argument count");int id=(int)Num(Arg(args,0));Check(id>=847&&id<=858,"Material id bounds");Check(Num(Arg(args,1))==1&&Num(Arg(args,2))==-1,"Native generation level/type");Check(Num(Arg(args,3))==multiplier,"Quantity multiplier");ids.Add(id);Put(r.ToInt64(),Join(BitConverter.GetBytes((double)id),new byte[8]));}catch(Exception ex){failure=ex;}return r;}
 static IntPtr Drop(IntPtr a,IntPtr b,IntPtr r,int count,IntPtr args){try{Check(a.ToInt64()==self&&b.ToInt64()==1234,"Drop context uses dying monster");Check(count==1,"Drop argument count");Check((int)Num(Arg(args,0))==ids[ids.Count-1],"Generated item passed to native drop");drops++;}catch(Exception ex){failure=ex;}return r;}
 static void FreeValue(IntPtr p){}
 static void Run(long wrapper,int objectIndex,bool enabled,int pattern,double quantity,int expected){Marshal.WriteInt32(new IntPtr(self+0xb8),objectIndex);Marshal.WriteInt32(new IntPtr(data),enabled?1:0);Put(data+8,BitConverter.GetBytes(quantity));rolls=drops=0;ids.Clear();mode=pattern;multiplier=quantity;failure=null;((Native)Marshal.GetDelegateForFunctionPointer(new IntPtr(wrapper),typeof(Native)))();if(failure!=null)throw failure;Check(drops==expected,"Expected drop count");Check(rolls==(enabled&&objectIndex==104?12:0),"Exactly twelve independent native rolls");Check(Marshal.ReadInt32(new IntPtr(data+4))==0,"Reentry flag cleared");if(pattern==0&&expected>0)Check(String.Join(",",ids)=="847,850,853,856","Independent RNG outcomes mapped to ids");}
 public static void Main(){module=VirtualAlloc(IntPtr.Zero,(UIntPtr)0x2900000,0x3000,0x40).ToInt64();Check(module!=0,"Allocation");data=module+0x1000;self=module+0x5000;long cave=module+0x3000,wrapper=module+0x4000;
  Random rng=Roll;Script gen=Generate,drop=Drop;Cleanup cleanup=FreeValue;
  try{Put(module+0x28b77b0,Jump(Marshal.GetFunctionPointerForDelegate(rng).ToInt64()));Put(module+0xda1b70,Jump(Marshal.GetFunctionPointerForDelegate(drop).ToInt64()));Put(module+0x115850,Jump(Marshal.GetFunctionPointerForDelegate(cleanup).ToInt64()));Put(module+KillMaterialProfile.Entry+18,Hex("c3"));Marshal.WriteInt32(new IntPtr(data+32),1);Marshal.WriteInt32(new IntPtr(data+512),104);
   Put(cave,KillMaterialProfile.Code(module,data,Marshal.GetFunctionPointerForDelegate(gen).ToInt64()));Put(wrapper,Join(Hex("555756415441554881ec08010000"),Imm("48bf",self),Imm("48be",1234),Imm("48bd",module+0x6000),Imm("49bb",cave),Hex("41ffd34881c408010000415d415c5e5f5dc3")));
   Run(wrapper,104,false,0,1,0);Run(wrapper,484,true,0,1,0);Run(wrapper,699,true,0,1,0);Run(wrapper,104,true,0,3,4);Run(wrapper,104,true,1,1000,12);Run(wrapper,104,true,2,1,0);Run(wrapper,104,true,1,1,12);
   Console.WriteLine("PASS: 12 independent native rolls; zero/some/all successes; quantity 1/3/1000; monster context and ids; disabled/dummy/player exclusion; reentry release.");
  }finally{GC.KeepAlive(rng);GC.KeepAlive(gen);GC.KeepAlive(drop);GC.KeepAlive(cleanup);VirtualFree(new IntPtr(module),UIntPtr.Zero,0x8000);}
 }
}
