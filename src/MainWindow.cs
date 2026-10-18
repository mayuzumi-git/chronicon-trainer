using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

public sealed partial class MainWindow {
    Engine engine; Label connection,status; ListBox features; Panel[] pages;
    CheckBox enabled,pickupEnabled,qualityEnabled,materialsEnabled,trackingEnabled,autoReveal;
    NumericUpDown mf,pickup,materialMultiplier; ComboBox quality; Label liveValues,materialState,mapState; ListView targets;
    Timer timer; bool settingsBusy,closeAfterSettings; double? appliedMf,appliedQuality;
    readonly string[] featureNames={"寻宝值","拾取范围","装备掉落品质","击杀掉落 12 种材料","独特敌人追踪","地图点亮"};
    readonly string[] qualityNames={"普通（白色）","附魔（蓝色）","稀有（黄色）","独特（紫色）","传奇（橙色／套装）","真传奇（红色）"};
    readonly double[] qualityValues={0,1,2,3,4,4.2};
    static readonly Color Background=Color.FromArgb(19,26,36),Surface=Color.FromArgb(28,39,53),Accent=Color.FromArgb(150,223,199);
    Label TextAt(Control parent,string text,int x,int y,int w,int h,int size) { var l=new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h),ForeColor=Color.FromArgb(222,234,246),Font=new Font("Microsoft YaHei UI",size)};parent.Controls.Add(l);return l; }
    Button ButtonAt(Control parent,string text,int x,int y,int w,Action action) { var b=new Button{Text=text,Location=new Point(x,y),Size=new Size(w,36),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(45,66,88),ForeColor=Color.White};b.Click+=delegate {Run(action);};parent.Controls.Add(b);return b; }
    void Run(Action action) { try {action();} catch(Exception ex) {MessageBox.Show(this,ex.Message,"操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Error);} }
    CheckBox Toggle(Control parent,string text,int y) {var c=new CheckBox{Text=text,Location=new Point(24,y),Size=new Size(470,34),ForeColor=Color.White};parent.Controls.Add(c);return c;}
    NumericUpDown NumberAt(Control parent,int y,decimal value,decimal maximum,int decimals) {var n=new NumericUpDown{Location=new Point(24,y),Size=new Size(280,36),Minimum=0,Maximum=maximum,Value=value,DecimalPlaces=decimals,ThousandsSeparator=true,Font=new Font("Microsoft YaHei UI",15)};parent.Controls.Add(n);return n;}
    Panel Page(int index,string heading,string hint) {var p=new Panel{Location=new Point(270,113),Size=new Size(585,438),BackColor=Surface,Visible=false};Controls.Add(p);TextAt(p,heading,24,20,530,40,19);TextAt(p,hint,24,68,530,55,10);pages[index]=p;return p;}
    public MainWindow() {
        Text="Chronicon · 综合修改器 v3.0";ClientSize=new Size(880,712);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;BackColor=Background;ForeColor=Color.White;Font=new Font("Microsoft YaHei UI",10);StartPosition=FormStartPosition.CenterScreen;
        TextAt(this,"Chronicon 综合修改器",24,17,580,42,22);connection=TextAt(this,"尚未连接游戏 · 本机单人",26,68,620,27,10);ButtonAt(this,"连接游戏",725,56,130,Connect);
        features=new ListBox{Location=new Point(24,113),Size=new Size(226,438),IntegralHeight=false,ItemHeight=54,DrawMode=DrawMode.OwnerDrawFixed,BackColor=Surface,ForeColor=Color.White,BorderStyle=BorderStyle.None};features.Items.AddRange(featureNames);Controls.Add(features);
        features.DrawItem+=delegate(object sender,DrawItemEventArgs e) {if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;using(var brush=new SolidBrush(selected?Color.FromArgb(47,87,112):Surface))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,featureNames[e.Index],Font,new Rectangle(e.Bounds.X+14,e.Bounds.Y+16,e.Bounds.Width-20,28),Color.White,TextFormatFlags.Left);};
        pages=new Panel[featureNames.Length];
        var p=Page(0,"寻宝值","指定角色面板的寻宝值百分比。修改后切换一次装备刷新属性。");enabled=Toggle(p,"开启寻宝值覆盖",130);mf=NumberAt(p,183,82,1000000,2);TextAt(p,"%",320,190,45,30,13);ButtonAt(p,"应用此功能",24,262,150,ApplyFind);liveValues=TextAt(p,"0–1,000,000%，支持两位小数。",24,321,530,82,10);
        p=Page(1,"拾取范围","指定角色面板的拾取范围百分比。换装后继续保持所选值。");pickupEnabled=Toggle(p,"开启拾取范围覆盖",130);pickup=NumberAt(p,183,110,1000000,2);TextAt(p,"%",320,190,45,30,13);ButtonAt(p,"应用此功能",24,262,150,ApplyPickup);TextAt(p,"0% 保留基础拾取距离；范围 0–1,000,000%。\n关闭并应用后恢复自然属性。",24,321,530,75,10);
        p=Page(2,"装备掉落品质","覆盖随机生成装备的品质。固定品质奖励与制作配方继续遵循游戏规则。");qualityEnabled=Toggle(p,"开启指定掉落品质",130);quality=new ComboBox{Location=new Point(24,184),Size=new Size(390,34),DropDownStyle=ComboBoxStyle.DropDownList};quality.Items.AddRange(qualityNames);quality.SelectedIndex=4;p.Controls.Add(quality);ButtonAt(p,"应用此功能",24,262,150,ApplyQuality);TextAt(p,"关闭并应用后恢复原概率。\n真传奇仍受等级、物品池与掉落来源限制。",24,321,530,75,10);
        p=Page(3,"击杀掉落材料","847–858 共 12 种材料各按约 1/3 概率独立判定。\n倍率仅增加成功后的数量，范围 1–1,000 倍。");materialsEnabled=Toggle(p,"开启 12 种材料掉落",130);materialMultiplier=NumberAt(p,183,1,1000,0);materialMultiplier.Minimum=1;TextAt(p,"× 数量",320,190,200,30,12);ButtonAt(p,"应用此功能",24,262,150,ApplyMaterials);materialState=TextAt(p,"倍率只影响成功掉落后的数量。\n树人外皮 [847] 至富集灰烬 [858]。",24,321,530,87,10);
        p=Page(4,"独特敌人追踪","显示存活独特敌人与已适配的互动触发点。紫色为敌人，橙色为触发点。");trackingEnabled=Toggle(p,"显示独特敌人位置",119);ButtonAt(p,"应用此功能",24,162,150,ApplyTracking);targets=new ListView{Location=new Point(24,218),Size=new Size(537,194),View=View.Details,FullRowSelect=true,BackColor=Background,ForeColor=Color.White};targets.Columns.Add("目标",200);targets.Columns.Add("状态",80);targets.Columns.Add("坐标",145);targets.Columns.Add("距离",85);p.Controls.Add(targets);
        p=Page(5,"地图点亮","点亮当前已载入地图；探索结果会按游戏自身机制保存。");autoReveal=Toggle(p,"进入新地图时自动点亮迷雾",130);ButtonAt(p,"应用自动点亮开关",24,183,220,ApplyAutoReveal);ButtonAt(p,"点亮当前地图一次",24,252,220,RevealCurrent);mapState=TextAt(p,"自动点亮默认关闭。开启后先执行当前地图一次。",24,321,530,85,10);
        features.SelectedIndexChanged+=delegate {for(int i=0;i<pages.Length;i++)pages[i].Visible=i==features.SelectedIndex;};features.SelectedIndex=0;
        ButtonAt(this,"物品添加器",24,573,255,OpenInventory);ButtonAt(this,"附魔修改器",294,573,255,OpenEnchants);ButtonAt(this,"恢复全部功能",564,573,291,Restore);
        status=TextAt(this,"选择左侧功能，在右侧设置并点击应用。关闭窗口会恢复运行时补丁。",26,633,825,61,10);status.ForeColor=Accent;
        timer=new Timer{Interval=500};timer.Tick+=Poll;timer.Start();FormClosing+=HandleClosing;FormClosed+=delegate {timer.Dispose();};
    }
    void Connect() {if(engine!=null&&engine.Alive){connection.Text="已连接 Chronicon · PID "+engine.Pid;return;}if(engine!=null)engine.Dispose();engine=new Engine();connection.Text="已连接 Chronicon · PID "+engine.Pid;appliedMf=appliedQuality=null;}
    void EnsureConnected() {if(engine==null||!engine.Alive)Connect();}
    void ApplyFind() {EnsureConnected();double? value=enabled.Checked?(double?)mf.Value:null;ChangeSettings(delegate {engine.Set(value,appliedQuality);appliedMf=value;});}
    void ApplyQuality() {EnsureConnected();double? value=qualityEnabled.Checked?(double?)qualityValues[quality.SelectedIndex]:null;ChangeSettings(delegate {engine.Set(appliedMf,value);appliedQuality=value;});}
    void ApplyPickup() {EnsureConnected();double? value=pickupEnabled.Checked?(double?)pickup.Value:null;ChangeSettings(delegate {engine.SetPickup(value);});}
    void ApplyMaterials() {EnsureConnected();bool on=materialsEnabled.Checked;int multiplier=(int)materialMultiplier.Value;ChangeSettings(delegate {engine.SetKillMaterials(on,multiplier);});}
    void ApplyTracking() {EnsureConnected();bool on=trackingEnabled.Checked;ChangeSettings(delegate {engine.SetMapTracking(on);});}
    void ApplyAutoReveal() {EnsureConnected();bool on=autoReveal.Checked;ChangeSettings(delegate {engine.SetAutomaticMapReveal(on);});}
    void RevealCurrent() {EnsureConnected();ChangeSettings(engine.RevealMapFog);}
    async void ChangeSettings(Action work) {
        if(settingsBusy)return;settingsBusy=true;foreach(Control c in Controls)if(!(c is Label))c.Enabled=false;status.Text="正在处理；请切回游戏，暂停状态不会执行。";
        try {await Task.Run(work);status.Text="操作完成。各项功能独立应用；切换左侧页面不会开启或关闭功能。";}
        catch(OperationCanceledException){status.Text="请求已取消。";}
        catch(Exception ex){closeAfterSettings=false;status.Text="操作未完成："+ex.Message;MessageBox.Show(this,ex.Message,"操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        finally {settingsBusy=false;foreach(Control c in Controls)if(!(c is Label))c.Enabled=true;if(closeAfterSettings)Close();}
    }
    void OpenInventory() {EnsureConnected();using(var w=new InventoryWindow(engine))w.ShowDialog(this);}
    void OpenEnchants() {EnsureConnected();using(var w=new EnchantWindow(engine))w.ShowDialog(this);}
    void OpenMap() {features.SelectedIndex=4;}
    void Restore() {if(engine==null)return;ChangeSettings(delegate {engine.Restore();appliedMf=appliedQuality=null;BeginInvoke((Action)delegate {enabled.Checked=pickupEnabled.Checked=qualityEnabled.Checked=materialsEnabled.Checked=trackingEnabled.Checked=autoReveal.Checked=false;targets.Items.Clear();});});}
    void Poll(object sender,EventArgs args) {
        if(engine==null||settingsBusy)return;
        try {
            if(!engine.Alive){engine.Dispose();engine=null;connection.Text="游戏已退出，请重新连接。";return;}
            engine.PollMap();engine.RefreshPickup();engine.Refresh();engine.RefreshKillMaterials();
            liveValues.Text=String.Format("当前寻宝值：{0}%\n当前拾取范围：{1}",engine.Current.HasValue?engine.Current.Value.ToString("0.##"):"等待属性刷新",engine.PickupCurrent.HasValue?engine.PickupCurrent.Value.ToString("0.##")+"%":"未覆盖");
            materialState.Text=String.Format("数量倍率：{0}×；已处理击杀：{1}\n已生成材料堆：{2}；倍率不改变原生概率判定。",engine.KillMaterialMultiplier,engine.KillMaterialCalls,engine.KillMaterialDrops);
            mapState.Text=engine.MapArea+"\n"+engine.MapMessage;
            if(features.SelectedIndex==4){targets.BeginUpdate();targets.Items.Clear();foreach(var t in engine.MapTargets){var row=new ListViewItem(new[]{t.Name,t.Hidden?"待激活":"存活",String.Format("{0:0}, {1:0}",t.X,t.Y),t.Distance.ToString("0")});row.ForeColor=t.Hidden?Color.Orange:Color.Violet;targets.Items.Add(row);}targets.EndUpdate();}
        } catch(Exception ex) {status.Text="状态读取失败："+ex.Message;}
    }
    void HandleClosing(object sender,FormClosingEventArgs args) {if(engine==null)return;args.Cancel=true;closeAfterSettings=true;if(settingsBusy){engine.CancelInventory();return;}ChangeSettings(delegate {engine.Dispose();engine=null;});}
}
