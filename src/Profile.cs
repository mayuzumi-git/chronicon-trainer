using System;
public static class Profile {
 public const string Hash="ab163214859c2fb4dad2a433ea937ca9a8ee8d2b99d2a856b74310723d24800c";
 public const long MfRva=25967246, QualityRva=15284492;
 public static readonly byte[] MfOriginal=new byte[]{72,139,208,72,139,207,232,103,31,133,254,139,206,232,48,12,253,0};
 public static readonly byte[] QualityOriginal=new byte[]{72,141,77,96,232,235,32,40,255,199,68,36,80,29,0,0,0};
 public static byte[] MfCode(long module,long data) { byte[] b=new byte[]{72,139,208,72,139,207,73,187,0,90,17,0,0,0,0,0,65,255,211,156,80,65,83,73,187,0,0,0,0,0,0,0,0,73,255,67,56,73,137,123,32,77,57,115,48,15,132,11,0,0,0,77,137,115,48,72,139,7,73,137,67,40,65,131,123,24,0,15,132,13,0,0,0,73,139,3,72,137,7,199,71,12,0,0,0,0,65,91,88,157,139,206,73,187,208,70,137,2,0,0,0,0,65,255,211,255,37,0,0,0,0,0,0,0,0,0,0,0,0};
 Array.Copy(BitConverter.GetBytes(module+1137152),0,b,8,8);
 Array.Copy(BitConverter.GetBytes(data),0,b,25,8);
 Array.Copy(BitConverter.GetBytes(module+42550992),0,b,94,8);
 Array.Copy(BitConverter.GetBytes(module+25967264),0,b,111,8); return b; }
 public static byte[] QualityCode(long module,long data) { byte[] b=new byte[]{72,141,77,96,73,187,0,90,17,0,0,0,0,0,65,255,211,156,80,65,83,73,187,0,0,0,0,0,0,0,0,65,131,123,72,0,15,132,74,0,0,0,131,125,108,0,15,133,25,0,0,0,72,184,0,0,0,0,0,0,16,192,72,57,69,96,15,133,44,0,0,0,233,20,0,0,0,131,125,108,7,15,133,29,0,0,0,131,125,96,252,15,133,19,0,0,0,73,255,67,64,73,139,67,16,72,137,69,96,199,69,108,0,0,0,0,65,91,88,157,199,68,36,80,29,0,0,0,255,37,0,0,0,0,0,0,0,0,0,0,0,0};
 Array.Copy(BitConverter.GetBytes(module+1137152),0,b,6,8);
 Array.Copy(BitConverter.GetBytes(data),0,b,23,8);
 Array.Copy(BitConverter.GetBytes(module+15284509),0,b,134,8); return b; }
}
