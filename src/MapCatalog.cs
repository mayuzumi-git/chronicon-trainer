using System.Collections.Generic;
public static class MapCatalog {
 public const int Player=699;
 public static readonly int[] Enemies=new int[]{2,6,17,104,285,288,291,297,298,299,300,301,302,303,304,305,306,307,308,309,310,311,312,313,314,315,316,317,318,319,320,332,338,339,350,426,481,485,486,507,508,510,512,513,514,515,516,517,518,535,536,540,561,564,565,618,619,622,623,624,625,636,637,638,640,641,682,683,694,696,714,718,730,733,734,735,743,747,748,752,753,776,777,778,779,784,785,805,807,808,810,813,814,816,821,823,833,834,835,836,837,845,847,848,856,859,878,881,882};
 public static readonly Dictionary<int,string> Hidden=new Dictionary<int,string>{{907,"沃尔坎姆（伪装石头）"},{902,"尼（特殊草丛）"},{905,"格洛德（珍稀烛台）"}};
 public static readonly Dictionary<string,long> Descriptors=new Dictionary<string,long>{{"isunique",60744664},{"hp",60720760},{"death",60701064},{"name",60734088},{"ready",61166024},{"areaName",60661784},{"minimapicons",60659592},{"DUNGEON",60779320},{"mapexplore",0x39d3d58}};
}
