using System;
using System.Runtime.InteropServices;
public static class MaterialDropNativeTests {
 [DllImport("kernel32.dll")] static extern IntPtr VirtualAlloc(IntPtr a,UIntPtr n,uint t,uint p);
 [DllImport("kernel32.dll")] static extern bool VirtualFree(IntPtr a,UIntPtr n,uint t);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate IntPtr Script(IntPtr self,IntPtr other,IntPtr result,int count,IntPtr args);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Builtin(IntPtr result,IntPtr self,IntPtr other,int count,IntPtr args);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Release(IntPtr value);
 static int generated,replaced,released; static double expectedLevel,expectedCount; static IntPtr expectedSelf=new IntPtr(123),expectedOther=new IntPtr(456);
 static Exception failure;
 static void Assert(bool ok,string text) { if(!ok) throw new Exception(text); }
 static void Jump(IntPtr address,Delegate d) { var b=new byte[14]; b[0]=255;b[1]=37;BitConverter.GetBytes(Marshal.GetFunctionPointerForDelegate(d).ToInt64()).CopyTo(b,6);Marshal.Copy(b,0,address,b.Length); }
 public static void Main() {
  IntPtr module=VirtualAlloc(IntPtr.Zero,(UIntPtr)0x4000000,0x3000,0x40),arguments=Marshal.AllocHGlobal(128);Assert(module!=IntPtr.Zero,"allocation");
  try {
   Script generate=delegate(IntPtr self,IntPtr other,IntPtr result,int count,IntPtr args) { try {
    Assert(self==expectedSelf&&other==expectedOther&&count==3,"generate context");
    Assert(ReadNumber(Marshal.ReadIntPtr(args))==828&&ReadNumber(Marshal.ReadIntPtr(args,8))==expectedLevel&&ReadNumber(Marshal.ReadIntPtr(args,16))==-1,"generate args");
    Marshal.WriteInt64(result,0x200000200000123);Marshal.WriteInt64(result,8,((long)15)<<32);generated++;
   } catch(Exception ex){failure=ex;} return result; };
   Builtin replace=delegate(IntPtr result,IntPtr self,IntPtr other,int count,IntPtr args) { try {
    Assert(self==expectedSelf&&other==expectedOther&&count==3,"replace context");
    Assert(Marshal.ReadInt64(args)==0x200000200000123&&Marshal.ReadInt32(args,12)==15,"map handle");
    Assert(Marshal.ReadInt64(args,16)==0x123456789&&Marshal.ReadInt32(args,28)==1,"quantity key");
    Assert(ReadNumber(IntPtr.Add(args,32))==expectedCount,"stack quantity");
    Assert(Marshal.ReadInt32(result,12)==5,"initialized result");
    Marshal.Copy(EngineValue(1),0,result,16);replaced++;
   } catch(Exception ex){failure=ex;} };
   Release release=delegate(IntPtr value){try{Assert(ReadNumber(value)==1,"release result");released++;}catch(Exception ex){failure=ex;}};
   Jump(IntPtr.Add(module,0xEA0DC0),generate);Jump(IntPtr.Add(module,0x28F3860),replace);Jump(IntPtr.Add(module,0x115850),release);
   Marshal.WriteInt64(IntPtr.Add(module,0x3DE4500),0x123456789);Marshal.WriteInt64(IntPtr.Add(module,0x3DE4508),((long)1)<<32);
   byte[] code=MaterialDropProfile.Code(module.ToInt64());Marshal.Copy(code,0,module,code.Length);var call=(Script)Marshal.GetDelegateForFunctionPointer(module,typeof(Script));
   foreach(double level in new[]{1.0,7.0}) foreach(double quantity in new[]{1.0,7.0,1000000000.0}) {
    expectedLevel=level;expectedCount=quantity;
    double[] values={828,level,-1,quantity};for(int i=0;i<4;i++){Marshal.WriteIntPtr(arguments,i*8,IntPtr.Add(arguments,32+i*16));Marshal.Copy(EngineValue(values[i]),0,IntPtr.Add(arguments,32+i*16),16);}
    var result=IntPtr.Add(arguments,96);Marshal.Copy(new byte[16],0,result,16);
    Assert(call(expectedSelf,expectedOther,result,4,arguments)==result,"return pointer");Assert(Marshal.ReadInt64(result)==0x200000200000123&&Marshal.ReadInt32(result,12)==15,"returned material map");
    if(failure!=null)throw failure;
   }
   Assert(generated==6&&replaced==6&&released==6,"call counts");GC.KeepAlive(generate);GC.KeepAlive(replace);GC.KeepAlive(release);
   Console.WriteLine("PASS: 6 native stack cases, contexts, arguments, quantity key, cleanup and return value.");
  } finally {Marshal.FreeHGlobal(arguments);VirtualFree(module,UIntPtr.Zero,0x8000);}
 }
 static byte[] EngineValue(double value){var b=new byte[16];BitConverter.GetBytes(value).CopyTo(b,0);return b;}
 static double ReadNumber(IntPtr p){var b=new byte[8];Marshal.Copy(p,b,0,8);return BitConverter.ToDouble(b,0);}
}
