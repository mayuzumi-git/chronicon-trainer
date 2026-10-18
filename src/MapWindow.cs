using System;
using System.Drawing;
using System.Windows.Forms;

public sealed class MapWindow : Form {
    readonly Engine engine;readonly CheckBox tracking,autoReveal;readonly Label status;readonly ListView targets;readonly Timer timer;
    public MapWindow(Engine e) {
        engine=e;Text="独特敌人与地图";ClientSize=new Size(710,390);StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(19,26,36);ForeColor=Color.White;Font=new Font("Microsoft YaHei UI",10);
        tracking=new CheckBox{Text="显示独特敌人位置",Checked=engine.MapEnabled,Location=new Point(20,18),Size=new Size(240,30)};Controls.Add(tracking);
        var reveal=new Button{Text="点亮当前地图全部迷雾",Location=new Point(420,16),Size=new Size(265,36),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(45,66,88)};Controls.Add(reveal);
        autoReveal=new CheckBox{Text="进入新地图时自动点亮迷雾",Checked=engine.MapAutoReveal,Location=new Point(20,54),Size=new Size(360,28)};Controls.Add(autoReveal);
        status=new Label{Location=new Point(20,86),Size=new Size(665,27),ForeColor=Color.FromArgb(150,223,199)};Controls.Add(status);
        targets=new ListView{Location=new Point(20,116),Size=new Size(665,205),View=View.Details,FullRowSelect=true,BackColor=Color.FromArgb(29,38,50),ForeColor=Color.White};
        targets.Columns.Add("独特敌人",265);targets.Columns.Add("状态",100);targets.Columns.Add("世界坐标",165);targets.Columns.Add("距离",105);Controls.Add(targets);
        var info=new Label{Location=new Point(20,337),Size=new Size(670,40),Text="紫色：存活敌人　橙色：互动触发点。关闭此面板后已开启功能继续运行，主窗口“恢复原始状态”关闭全部功能。"};Controls.Add(info);
        tracking.CheckedChanged+=delegate { Try(delegate { engine.SetMapTracking(tracking.Checked); }); };
        autoReveal.CheckedChanged+=delegate { Try(delegate { engine.SetAutomaticMapReveal(autoReveal.Checked); }); };
        reveal.Click+=delegate { Try(engine.RevealMapFog); };
        timer=new Timer{Interval=500};timer.Tick+=delegate { RefreshState(); };timer.Start();FormClosed+=delegate { timer.Stop();timer.Dispose(); };
        RefreshState();
    }
    void Try(Action action) { try { action();RefreshState(); } catch(Exception ex) { MessageBox.Show(this,ex.Message,"地图操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Error); } }
    void RefreshState() {
        status.Text=engine.MapArea+" · "+engine.MapMessage;
        targets.BeginUpdate();targets.Items.Clear();
        foreach(var t in engine.MapTargets) { var row=new ListViewItem(new[]{t.Name,t.Hidden?"待激活":"存活",String.Format("{0:0}, {1:0}",t.X,t.Y),t.Distance.ToString("0")});row.ForeColor=t.Hidden?Color.Orange:Color.Violet;targets.Items.Add(row); }
        targets.EndUpdate();
    }
}
