using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

public sealed class EnchantWindow : Form {
    readonly Engine engine; EnchantItem item; int[][] pools; int[] targets,displaySlots; ListBox slots,choices; TextBox search; Label description,status,selected; Button load,arm,cancel,set; Timer timer; bool busy,closing,armed,stale; int chosenCount;
    int SelectedSlot { get { int row=slots.SelectedIndex; return displaySlots!=null && row>=0 && row<displaySlots.Length?displaySlots[row]:-1; } }
    static readonly string[] Kinds={"主要","次级","史诗"};
    public EnchantWindow(Engine value) {
        engine=value; Text="选择已有装备的附魔词条"; ClientSize=new Size(760,715); Font=new Font("Microsoft YaHei UI",10); BackColor=Color.FromArgb(19,26,36); FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false; StartPosition=FormStartPosition.CenterParent;
        LabelAt("已有装备 · 附魔词条选择",24,17,710,36,18);
        LabelAt("1. 在游戏附魔师放入装备，解除需要修改的词条锁定。\n2. 读取装备，逐个槽位选择目标并点击「设为此槽位目标」。\n3. 点击「准备整套洗牌」，回游戏点一次「洗牌」，同时应用全部预选。",24,65,710,75,10);
        load=ButtonAt("读取附魔师装备",24,149,174,ReadItem); selected=LabelAt("尚未读取",215,153,515,48,10);
        LabelAt("当前词条 → 预选目标（固定能力、符文和锁定词条保持原样）",24,204,710,25,10);
        slots=new ListBox{Location=new Point(24,236),Size=new Size(710,144),BackColor=Color.FromArgb(28,39,53),ForeColor=Color.White,IntegralHeight=false}; Controls.Add(slots);
        LabelAt("搜索目标词条",24,391,140,28,10); search=new TextBox{Location=new Point(169,388),Size=new Size(565,28)}; Controls.Add(search);
        choices=new ListBox{Location=new Point(24,430),Size=new Size(710,116),BackColor=Color.FromArgb(28,39,53),ForeColor=Color.White,IntegralHeight=false}; Controls.Add(choices);
        description=LabelAt("读取后只列出此装备的游戏默认候选词条。",24,553,710,48,10);
        set=ButtonAt("设为此槽位目标",24,611,190,SetTarget); set.Enabled=false; arm=ButtonAt("准备整套洗牌",231,611,190,Arm); arm.Enabled=false; cancel=ButtonAt("取消整套预选",438,611,170,Disarm); cancel.Enabled=false;
        status=LabelAt("未锁定槽位默认预选原词条；洗牌时数值重新随机。费用按游戏规则扣除。",24,660,710,48,10); status.ForeColor=Color.FromArgb(150,223,199);
        slots.SelectedIndexChanged+=delegate { Filter(); }; search.TextChanged+=delegate { Filter(); }; choices.SelectedIndexChanged+=delegate { var entry=choices.SelectedItem as EnchantEntry; description.Text=entry==null?"":entry.Text.Replace("AMOUNT","游戏随机数值"); };
        timer=new Timer{Interval=400}; timer.Tick+=Poll; timer.Start();
        FormClosing+=delegate(object sender,FormClosingEventArgs args) { try { if(busy) { args.Cancel=true; closing=true; if(engine!=null) engine.CancelInventory(); } else if(engine!=null) engine.RestoreEnchants(); } catch(Exception ex) { args.Cancel=true;status.Text=ex.Message; } };
        FormClosed+=delegate { timer.Dispose(); };
    }
    Label LabelAt(string text,int x,int y,int width,int height,int size) { var label=new Label{Text=text,Location=new Point(x,y),Size=new Size(width,height),ForeColor=Color.FromArgb(225,235,246),Font=new Font("Microsoft YaHei UI",size)}; Controls.Add(label);return label; }
    Button ButtonAt(string text,int x,int y,int width,Action action) { var button=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,36),BackColor=Color.FromArgb(45,66,88),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};button.Click+=delegate { action(); };Controls.Add(button);return button; }
    void SetBusy(bool value) { busy=value; UpdateButtons(); }
    void UpdateButtons() {
        load.Enabled=!busy; slots.Enabled=search.Enabled=choices.Enabled=!busy && !armed && !stale;
        set.Enabled=!busy && !armed && !stale && choices.SelectedItem!=null; arm.Enabled=false;
        if(!busy && !armed && !stale && item!=null) { try { item.Plan(targets,pools); arm.Enabled=true; } catch {} }
    }
    internal void ShowItem(EnchantItem value,int[][] allowed) {
        item=value; pools=allowed; stale=false; targets=Enumerable.Range(0,EnchantItem.SlotCapacity).Select(i=>(int)item.Get("enchant"+i)).ToArray(); displaySlots=item.OccupiedSlots; slots.Items.Clear(); search.Clear();
        selected.Text=ItemCatalog.Equipment.First(x=>x.Id==item.Id).Name+" · 等级 "+item.Get("level")+" · "+displaySlots.Length+" 条词条";
        foreach(int slot in displaySlots) {
            slots.Items.Add(SlotText(slot));
        }
        int first=Array.FindIndex(displaySlots,i=>item.Editable(i) && item.Kind(i,pools)>=0);slots.SelectedIndex=first>=0?first:displaySlots.Length>0?0:-1; Filter();
    }
    string SlotText(int slot) {
        int id=(int)item.Get("enchant"+slot),kind=item.Kind(slot,pools);
        string state=item.Get("enchant_solid"+slot)!=0?"固定":item.Get("enchant_rune"+slot)!=0?"符文":item.Get("enchant_locked"+slot)!=0?"锁定":kind<0?"不可替换":Kinds[kind];
        if(item.Editable(slot) && slot==item.ConvertibleSlot(pools)) state="主要／史诗";
        return (slot+1)+". ["+state+"] "+EnchantCatalog.Get(id).Name+" · "+item.Get("enchant_value"+slot).ToString("0.##")+(item.Editable(slot)?" → "+EnchantCatalog.Get(targets[slot]).Name:"");
    }
    void SetTarget() {
        int slot=SelectedSlot; var entry=choices.SelectedItem as EnchantEntry;
        if(busy || armed || stale || item==null || entry==null || slot<0) return;
        targets[slot]=entry.Id;slots.Items[slots.SelectedIndex]=SlotText(slot);
        try { item.Plan(targets,pools);status.Text="已预选第 "+(slot+1)+" 条「"+entry.Name+"」。可继续设置其他槽位，完成后准备整套洗牌。"; }
        catch(Exception ex) { status.Text=ex.Message; }
        UpdateButtons();
    }
    void Filter() {
        choices.Items.Clear(); UpdateButtons(); int slot=SelectedSlot;
        if(item==null || slot<0 || !item.Editable(slot)) return;
        int kind=item.Kind(slot,pools); if(kind<0) return; string term=search.Text.Trim();
        var ids=item.Allowed(slot,pools).Where(id=>!Enumerable.Range(0,EnchantItem.SlotCapacity).Any(i=>!item.Editable(i) && item.Get("enchant"+i)==id));
        choices.Items.AddRange(ids.Select(EnchantCatalog.Get).Where(x=>x.Name.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0 || x.Id.ToString().Contains(term)).OrderBy(x=>x.Name).Cast<object>().ToArray());
        if(choices.Items.Count>0) { var current=choices.Items.Cast<EnchantEntry>().FirstOrDefault(x=>x.Id==targets[slot]); choices.SelectedItem=current??choices.Items[0]; } UpdateButtons();
    }
    async void ReadItem() {
        if(busy) return; SetBusy(true); status.Text="正在读取。请切回游戏；暂停状态下不会执行查询。";
        try { Disarm(); status.Text="正在读取，请切回游戏。"; var result=await Task.Run(delegate { var current=engine.ReadEnchantItem();return Tuple.Create(current,engine.EnchantPools(current)); });ShowItem(result.Item1,result.Item2); status.Text="已读取，未锁定槽位默认预选原词条。可逐条修改目标，再一次洗出整套。"; }
        catch(Exception ex) { status.Text=ex.Message; }
        finally { SetBusy(false); if(closing) Close(); }
    }
    async void Arm() {
        if(busy || armed || stale || item==null) return; int[] preset=(int[])targets.Clone();
        SetBusy(true);
        try { chosenCount=item.Plan(preset,pools).Count(id=>id>0); await Task.Run(()=>engine.ArmEnchants(item,preset,pools));armed=true;cancel.Enabled=true;status.Text="已准备整套 "+chosenCount+" 条预选。切回游戏点击一次「洗牌」；数值重新随机。"; }
        catch(Exception ex) { status.Text=ex.Message; }
        finally { SetBusy(false); if(closing) Close(); }
    }
    void Disarm() { try { if(engine!=null) engine.DisarmEnchant();armed=false;cancel.Enabled=false;UpdateButtons();status.Text="整套预选已取消。已完成的游戏洗牌不会撤销。"; } catch(Exception ex) { status.Text=ex.Message; } }
    void Poll(object sender,EventArgs args) {
        if(!armed || busy || engine==null) return;
        try {
            int state=engine.EnchantStatus;
            if(state==1) return;
            armed=false;stale=true;cancel.Enabled=false;UpdateButtons();
            int applied=Convert.ToString(engine.EnchantAppliedMask,2).Count(c=>c=='1');
            status.Text=state==2?"整套 "+chosenCount+" 条已提交。请查看游戏洗牌结果；继续修改需重新读取。":"本次已提交 "+applied+" 条，剩余预选已取消。"+(state==3?"目标不在游戏候选池中。":state==4?"附魔师装备已变化。":state==5?"检测到重复词条。":state==6?"游戏槽位与预选不符。":"洗牌未生成目标词条。")+"请查看游戏结果并重新读取。";
        } catch(Exception ex) { armed=false;status.Text=ex.Message; }
    }
}
