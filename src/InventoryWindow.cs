using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

public sealed class InventoryWindow : Form {
    readonly Engine engine; TreeView category; int kind; TextBox search; ListBox items; NumericUpDown amount,level; Label info,status,levelLabel; Button add,read,cancel; Timer progress; bool busy,closing; volatile bool cancelled;
    public InventoryWindow(Engine e) {
        engine=e; if(engine!=null) engine.InstallInventory();
        Text="物品添加器"; ClientSize=new Size(880,610); FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false; StartPosition=FormStartPosition.CenterParent; Font=new Font("Microsoft YaHei UI",10); BackColor=Color.FromArgb(19,26,36);
        LabelAt("选择种类和数量",24,20,640,36,18);
        category=new TreeView{Location=new Point(25,70),Size=new Size(170,310),BackColor=Color.FromArgb(28,39,53),ForeColor=Color.White,HideSelection=false,FullRowSelect=true,ItemHeight=36};Controls.Add(category);
        var equipment=new TreeNode("装备（697 种）"){Tag=0};equipment.Nodes.Add(new TreeNode("法器（23 种）"){Tag=1});category.Nodes.Add(equipment);category.Nodes.Add(new TreeNode("材料（44 种）"){Tag=2});category.Nodes.Add(new TreeNode("宝石（63 种）"){Tag=3});var runes=new TreeNode("符文"){Tag=4};runes.Nodes.Add(new TreeNode("空白符文石"){Tag=5});runes.Nodes.Add(new TreeNode("掉落次级符文"){Tag=6});category.Nodes.Add(runes);category.Nodes.Add(new TreeNode("水晶 [0]"){Tag=7});category.ExpandAll();
        search=new TextBox{Location=new Point(215,70),Size=new Size(640,30)}; Controls.Add(search); LabelAt("输入名称、职业或编号搜索",217,105,620,22,9);
        items=new ListBox{Location=new Point(215,135),Size=new Size(640,245),BackColor=Color.FromArgb(28,39,53),ForeColor=Color.White,IntegralHeight=false}; Controls.Add(items);
        LabelAt("数量",25,399,60,27,10); amount=new NumericUpDown{Location=new Point(83,396),Size=new Size(200,30),Minimum=1,Maximum=1000,Value=1,ThousandsSeparator=true}; Controls.Add(amount);
        levelLabel=LabelAt("装备等级",324,399,92,27,10); level=new NumericUpDown{Location=new Point(418,396),Size=new Size(120,30),Minimum=1,Maximum=100,Value=100}; Controls.Add(level);
        info=LabelAt("",25,441,650,48,10);
        add=ButtonAt("按数量生成",25,505,158,delegate { Add(); }); read=ButtonAt("读取当前数量",198,505,158,delegate { ReadAmount(); }); cancel=ButtonAt("取消剩余操作",371,505,158,Cancel); cancel.Enabled=false;
        status=LabelAt("装备生成到角色脚边；添加后请切回游戏执行。",25,557,650,48,10); status.ForeColor=Color.FromArgb(150,223,199);
        category.AfterSelect+=delegate {kind=(int)category.SelectedNode.Tag;Filter();};search.TextChanged+=delegate {Filter();};items.SelectedIndexChanged+=delegate {UpdateSelection();};category.SelectedNode=equipment;
        progress=new Timer{Interval=200}; progress.Tick+=delegate { if(busy && engine.InventoryInstalled) { try { int done=BitConverter.ToInt32(engine.Read(engine.InventoryData+24,4),0); status.Text="正在处理；已完成 "+done+" 次。请切回游戏，暂停状态不会执行。"; } catch {} } }; progress.Start();
        FormClosing+=delegate(object sender,FormClosingEventArgs args) { if(busy) { args.Cancel=true; closing=true; Cancel(); } }; FormClosed+=delegate { progress.Dispose(); };
    }
    Label LabelAt(string text,int x,int y,int w,int h,int size) { var l=new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h),ForeColor=Color.FromArgb(225,235,246),Font=new Font("Microsoft YaHei UI",size)}; Controls.Add(l); return l; }
    Button ButtonAt(string text,int x,int y,int w,Action action) { var b=new Button{Text=text,Location=new Point(x,y),Size=new Size(w,36),BackColor=Color.FromArgb(45,66,88),ForeColor=Color.White,FlatStyle=FlatStyle.Flat}; b.Click+=delegate { action(); }; Controls.Add(b); return b; }
    void Filter() {
        ItemEntry[] source=kind==0?ItemCatalog.Equipment:kind==1?ItemCatalog.Implements:kind==2?ItemCatalog.Materials:kind==3?ItemCatalog.Gems:kind==4?ItemCatalog.BlankRunestones.Concat(RuneCatalog.DroppedLesser).ToArray():kind==5?ItemCatalog.BlankRunestones:kind==6?RuneCatalog.DroppedLesser:new[]{new ItemEntry(0,25,"水晶")};
        string term=search.Text.Trim(); items.BeginUpdate(); items.Items.Clear(); items.Items.AddRange(source.Where(x=>x.Name.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0 || x.Id.ToString().Contains(term)).Cast<object>().ToArray()); items.EndUpdate(); if(items.Items.Count>0) items.SelectedIndex=0;
        bool gear=kind<=1, stone=kind==5, gem=kind==3, material=kind==2, rune=kind==4||kind==6; amount.Maximum=(gear || stone || rune)?1000:1000000000; level.Visible=levelLabel.Visible=gear; read.Enabled=!gear && !stone && !rune; add.Text=gear?"按数量生成装备":stone?"生成空白符文石":rune?"生成所选符文":material?"生成材料掉落物":"添加指定数量";
        info.Text=gear?"装备与法器按自身品质生成到脚边，每件独立随机词条。单次最多 1,000 件。\n恢复补丁不会撤销已经生成、拾取或保存的物品。":stone?"生成未吸收能力的空白符文石（游戏名称：符文石），在脚边拾取。\n单次最多 1,000 个；恢复补丁不会删除已生成的符文石。":rune?"符文分类合并空白符文石与 120 种掉落次级符文，可按名称、职业搜索。\n在角色脚边拾取；能力与适用限制由游戏生成，单次最多 1,000 个。":gem?"按名称选择宝石种类、形状和品质，直接增加到宝石页。\n支持方形／球形／星形及粗糙／切削／磨光；恢复补丁不会扣回。":material?"按填写数量生成一堆材料掉落物，在角色脚边正常拾取。单次最多 10 亿。\n普通材料进入背包；精华类按游戏规则入账，可能自动拾取。":"水晶 [0] 直接入账，填写要增加的数量，单次最多 10 亿。\n恢复补丁不会扣回；其他代币可在材料分类中生成掉落物。";
        UpdateSelection();
    }
    void UpdateSelection() {if(!busy)add.Enabled=items.SelectedItem is ItemEntry;}
    void SetBusy(bool value) { busy=value; category.Enabled=search.Enabled=items.Enabled=amount.Enabled=level.Enabled=add.Enabled=!value; read.Enabled=!value && (kind==2||kind==3||kind==7); cancel.Enabled=value;if(!value)UpdateSelection(); }
    void Cancel() { cancelled=true; try { engine.CancelInventory(); status.Text="已取消，正在结束当前操作。"; } catch(Exception ex) { status.Text=ex.Message; } }
    async void ReadAmount() {
        var item=items.SelectedItem as ItemEntry; if(item==null || busy) return; int selectedKind=kind; cancelled=false; SetBusy(true);
        try { double value=await Task.Run(()=>selectedKind==2?engine.ReadMaterialCount(item.Id):engine.Query(0x69B140,item.Id)); status.Text=cancelled?"读取已取消。":item.Name+" 当前数量："+value.ToString("#,0.##"); }
        catch(Exception ex) { MessageBox.Show(this,ex.Message,"读取未完成"); }
        finally { SetBusy(false); if(closing) Close(); }
    }
    async void Add() {
        var item=items.SelectedItem as ItemEntry; if(item==null || busy) return; int selectedKind=kind, count=(int)amount.Value, itemLevel=(int)level.Value; cancelled=false; SetBusy(true);
        try {
            string result=await Task.Run(delegate {
                if(selectedKind<=1 || selectedKind>=4&&selectedKind<=6) { if(selectedKind<=1) engine.AddEquipment(item.Id,count,itemLevel); else if(item.Id==741) engine.AddBlankRunestones(count); else engine.AddDroppedLesserRune(item.Id,count); int done=BitConverter.ToInt32(engine.Read(engine.InventoryData+24,4),0); return "已生成 "+done+(selectedKind<=1?" 件「":" 个「")+item.Name+"」，请在角色脚边拾取。"; }
                if(selectedKind==2) { bool dropped=engine.DropMaterial(item.Id,count); return dropped?"已在角色脚边生成 "+count.ToString("#,0")+" 个「"+item.Name+"」，请正常拾取。":"操作已取消，未生成材料。"; }
                double before=engine.Query(0x69B140,item.Id); if(cancelled) return "操作已取消，未添加。";
                if(selectedKind==3) engine.AddGem(item.Id,count); else engine.AddCrystals(count);
                if(cancelled) return "剩余操作已取消；已执行的入账会保留。";
                double after=engine.Query(0x69B140,item.Id); double delta=after-before;
                if(Math.Abs(delta-count)>0.001) throw new Exception("实际增量为 "+delta.ToString("#,0.##")+"，与请求不符；请检查游戏当前余额。");
                return "已增加 "+count.ToString("#,0")+" 「"+item.Name+"」；现有 "+after.ToString("#,0.##")+"。";
            });
            status.Text=result;
        } catch(Exception ex) { MessageBox.Show(this,ex.Message,"添加未完成"); }
        finally { SetBusy(false); if(closing) Close(); }
    }
}
