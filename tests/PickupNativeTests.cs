using System;
using System.Runtime.InteropServices;
public static class PickupNativeTests {
 [DllImport("kernel32.dll")] static extern IntPtr VirtualAlloc(IntPtr a,UIntPtr n,uint t,uint p);
 [DllImport("kernel32.dll")] static extern bool VirtualFree(IntPtr a,UIntPtr n,uint t);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate IntPtr Script(IntPtr self,IntPtr other,IntPtr result,int count,IntPtr args);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Native();
 static double gameValue;static int sets;static Exception failure;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
 static void Put(long a,byte[] b){Marshal.Copy(b,0,new IntPtr(a),b.Length);}
 static byte[] Value(double v){var b=new byte[16];BitConverter.GetBytes(v).CopyTo(b,0);return b;}
 static double Get(long a){var b=new byte[8];Marshal.Copy(new IntPtr(a),b,0,8);return BitConverter.ToDouble(b,0);}
 static byte[] Join(params byte[][] p){var b=new System.Collections.Generic.List<byte>();foreach(var a in p)b.AddRange(a);return b.ToArray();}
 static byte[] Imm(string hex,long n){var b=new System.Collections.Generic.List<byte>();for(int i=0;i<hex.Length;i+=2)b.Add(Convert.ToByte(hex.Substring(i,2),16));b.AddRange(BitConverter.GetBytes(n));return b.ToArray();}
 static byte[] Hex(string s){var b=new byte[s.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(s.Substring(i*2,2),16);return b;}
 static void Jump(long a,Delegate d){Put(a,Join(Hex("ff2500000000"),BitConverter.GetBytes(Marshal.GetFunctionPointerForDelegate(d).ToInt64())));}
 public static void Main(){long module=VirtualAlloc(IntPtr.Zero,(UIntPtr)0x1D00000,0x3000,0x40).ToInt64();Check(module!=0,"allocate");
  try {long data=module+0x1000,cave=module+0x3000,wrap=module+0x4000,ret=module+0x5000;
   Script get=delegate(IntPtr self,IntPtr other,IntPtr result,int count,IntPtr args){try{Check(self.ToInt64()==123&&other.ToInt64()==456&&count==2,"get context");Check(Get(Marshal.ReadIntPtr(args).ToInt64())==0&&Get(Marshal.ReadIntPtr(args,8).ToInt64())==36,"get args");Put(result.ToInt64(),Value(gameValue));}catch(Exception e){failure=e;}return result;};
   Script set=delegate(IntPtr self,IntPtr other,IntPtr result,int count,IntPtr args){try{Check(self.ToInt64()==123&&other.ToInt64()==456&&count==3,"set context");Check(Get(Marshal.ReadIntPtr(args).ToInt64())==0&&Get(Marshal.ReadIntPtr(args,8).ToInt64())==36,"set args");gameValue=Get(Marshal.ReadIntPtr(args,16).ToInt64());sets++;}catch(Exception e){failure=e;}return result;};
   Jump(module+0x1C634F0,get);Jump(module+0x1C65510,set);Put(module+0x115850,Hex("c3"));Put(module+0x115690,Hex("c3"));Put(ret,Hex("c3"));
   for(int i=0;i<3;i++)Marshal.WriteInt64(new IntPtr(data+128+i*8),data+256+i*16);Put(data+256,Value(0));Put(data+272,Value(36));
   byte[] code=PickupProfile.Code(module,data);BitConverter.GetBytes(ret).CopyTo(code,code.Length-8);Put(cave,code);
   Put(wrap,Join(Hex("55415641574881ec08010000"),Imm("48bd",data+512),Imm("49be",123),Imm("49bf",456),Imm("49bb",cave),Hex("41ffd34881c408010000415f415e5dc3")));
   var run=(Native)Marshal.GetDelegateForFunctionPointer(new IntPtr(wrap),typeof(Native));
   gameValue=1.1;run();Check(Get(data+16)==1.1&&Get(data+40)==1.1&&sets==0,"disabled capture");
   Marshal.WriteInt32(new IntPtr(data),1);Put(data+8,BitConverter.GetBytes(10.0));run();Check(gameValue==10&&Get(data+16)==1.1&&Get(data+40)==10,"override");
   run();Check(Get(data+16)==1.1,"baseline preserved on repeated cap");gameValue=2.2;run();Check(Get(data+16)==2.2&&gameValue==10,"refreshed equipment baseline");
   foreach(double v in new[]{0.0,1.25,9.99,50,10000}){gameValue=2.2;Put(data+8,BitConverter.GetBytes(v));run();Check(gameValue==v&&Get(data+40)==v,"range "+v);}
   Marshal.WriteInt32(new IntPtr(data),0);gameValue=3.5;run();Check(Get(data+16)==3.5&&Get(data+40)==3.5,"disabled after refresh");
   if(failure!=null)throw failure;GC.KeepAlive(get);GC.KeepAlive(set);Console.WriteLine("PASS: native x64 pickup override, zero/fraction/max values, context/arguments, refreshed and repeated baselines, disabled passthrough.");
  }finally{VirtualFree(new IntPtr(module),UIntPtr.Zero,0x8000);}
 }
}
