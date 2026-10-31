using System.Drawing.Drawing2D;

namespace CubeRadar.Tr;

internal static class UiTheme
{
    internal static readonly Color Background=Color.FromArgb(15,19,25),Surface=Color.FromArgb(24,30,39),Raised=Color.FromArgb(32,40,51),
        Text=Color.FromArgb(237,242,247),Muted=Color.FromArgb(162,175,190),Accent=Color.FromArgb(235,180,91),
        Green=Color.FromArgb(113,209,168),Danger=Color.FromArgb(233,139,132),Line=Color.FromArgb(44,54,67);
    internal static int Px(Control c,int value)=>(int)Math.Round(value*c.DeviceDpi/96.0);
    internal static GraphicsPath Rounded(RectangleF r,float radius=8)
    {
        var p=new GraphicsPath();var d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
        if(d<=0)return p;
        p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);
        p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
    internal static Label Label(string text,float size=10,Color? color=null,FontStyle weight=FontStyle.Regular)=>new()
    {Text=text,UseMnemonic=false,Font=new("Segoe UI",size,weight),ForeColor=color??Text,BackColor=Color.Transparent,AutoSize=false,AutoEllipsis=true,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty};
    internal static void Grid(DataGridView grid)
    {
        grid.BackgroundColor=Surface;grid.BorderStyle=BorderStyle.None;grid.CellBorderStyle=DataGridViewCellBorderStyle.None;
        grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;grid.RowHeadersVisible=false;grid.EnableHeadersVisualStyles=false;
        grid.ColumnHeadersDefaultCellStyle=new() {BackColor=Surface,ForeColor=Muted,SelectionBackColor=Surface,Font=new("Segoe UI",9),Padding=new(8,0,8,0)};
        grid.ColumnHeadersHeight=38;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.DefaultCellStyle=new() {BackColor=Surface,ForeColor=Text,SelectionBackColor=Raised,SelectionForeColor=Text,Padding=new(8,0,8,0),Font=new("Segoe UI",10)};
        grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(26,33,43);grid.GridColor=Surface;grid.RowTemplate.Height=44;
    }
    internal static void Input(Control c)
    {
        c.BackColor=Raised;c.ForeColor=Text;
        if(c is TextBox t)t.BorderStyle=BorderStyle.FixedSingle;
        if(c is ComboBox combo)
        {
            combo.FlatStyle=FlatStyle.Flat;combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=22;
            combo.DrawItem+=(_,e)=>{using var fill=new SolidBrush((e.State&DrawItemState.Selected)!=0?Line:Raised);e.Graphics.FillRectangle(fill,e.Bounds);if(e.Index>=0)TextRenderer.DrawText(e.Graphics,combo.Items[e.Index]?.ToString(),combo.Font,e.Bounds,Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};
        }
    }
    internal static ContextMenuStrip Menu()=>new() {BackColor=Raised,ForeColor=Text,ShowImageMargin=false,Renderer=new ToolStripProfessionalRenderer(new MenuColors())};
    private sealed class MenuColors:ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground=>Raised;
        public override Color MenuItemSelected=>Line;
        public override Color MenuItemBorder=>Line;
        public override Color MenuBorder=>Line;
        public override Color ImageMarginGradientBegin=>Raised;
        public override Color ImageMarginGradientEnd=>Raised;
    }
}

internal class SurfaceCard:Panel
{
    public SurfaceCard(){DoubleBuffered=true;ResizeRedraw=true;BackColor=UiTheme.Surface;Padding=new(16);}
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??UiTheme.Background);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var path=UiTheme.Rounded(new RectangleF(0,0,Width-1,Height-1),UiTheme.Px(this,8));using var brush=new SolidBrush(BackColor);e.Graphics.FillPath(brush,path);
    }
}

internal enum ButtonTone {Secondary,Primary,Ghost,Danger,Navigation}
internal sealed class HudButton:Button
{
    private bool _hover,_pressed;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal ButtonTone Tone {get;set;}
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool Active {get;set;}
    public HudButton()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
        FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Font=new("Segoe UI",10);Cursor=Cursors.Hand;Height=40;
        BackColor=UiTheme.Raised;ForeColor=UiTheme.Text;Margin=new(0,0,8,0);
        MouseEnter+=(_,_)=>{_hover=true;Invalidate();};MouseLeave+=(_,_)=>{_hover=false;_pressed=false;Invalidate();};
        MouseDown+=(_,_)=>{_pressed=true;Invalidate();};MouseUp+=(_,_)=>{_pressed=false;Invalidate();};
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent?.BackColor??UiTheme.Background);
        var fill=Tone==ButtonTone.Primary?UiTheme.Accent:Tone is ButtonTone.Ghost or ButtonTone.Navigation?Parent?.BackColor??UiTheme.Background:UiTheme.Raised;
        if(_hover&&Enabled)fill=Tone==ButtonTone.Primary?Color.FromArgb(247,197,119):UiTheme.Line;
        if(_pressed&&Enabled)fill=UiTheme.Line;
        using var path=UiTheme.Rounded(new RectangleF(0,0,Width-1,Height-1),UiTheme.Px(this,7));using var brush=new SolidBrush(fill);g.FillPath(brush,path);
        var ink=!Enabled?Color.FromArgb(105,117,132):Tone==ButtonTone.Primary?UiTheme.Background:Tone==ButtonTone.Danger?UiTheme.Danger:Active?UiTheme.Accent:UiTheme.Text;
        var alignment=TextAlign is ContentAlignment.MiddleLeft or ContentAlignment.TopLeft or ContentAlignment.BottomLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter;
        TextRenderer.DrawText(g,Text,Font,ClientRectangle,ink,TextFormatFlags.NoPrefix|alignment|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Active){using var accent=new SolidBrush(UiTheme.Accent);g.FillRectangle(accent,UiTheme.Px(this,12),Height-3,Width-UiTheme.Px(this,24),3);}
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-4,-4),ink,fill);
    }
}

internal sealed class ToggleSwitch:CheckBox
{
    public ToggleSwitch(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AutoSize=true;Cursor=Cursors.Hand;Font=new("Segoe UI",10);Margin=new(0,4,16,4);}
    public override Size GetPreferredSize(Size proposedSize)=>new(TextRenderer.MeasureText(Text,Font).Width+UiTheme.Px(this,54),UiTheme.Px(this,32));
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??UiTheme.Background);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        var w=UiTheme.Px(this,34);var h=UiTheme.Px(this,18);var x=UiTheme.Px(this,2);var y=(Height-h)/2;
        using var track=new SolidBrush(Enabled&&Checked?UiTheme.Accent:UiTheme.Line);using var p=UiTheme.Rounded(new RectangleF(x,y,w,h),h/2f);e.Graphics.FillPath(track,p);
        var knob=h-UiTheme.Px(this,4);using var ink=new SolidBrush(Checked?UiTheme.Background:UiTheme.Muted);
        e.Graphics.FillEllipse(ink,Checked?x+w-knob-2:x+2,y+2,knob,knob);
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(w+UiTheme.Px(this,12),0,Math.Max(0,Width-w-12),Height),Enabled?UiTheme.Text:UiTheme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-1,-1),UiTheme.Accent,UiTheme.Surface);
    }
}

internal sealed class HudGrid:DataGridView
{
    private int _hover=-1;
    public HudGrid()
    {
        DoubleBuffered=true;
        CellMouseEnter+=(_,e)=>{var old=_hover;_hover=e.RowIndex;if(old>=0&&old<Rows.Count)InvalidateRow(old);if(_hover>=0)InvalidateRow(_hover);};
        MouseLeave+=(_,_)=>{var old=_hover;_hover=-1;if(old>=0&&old<Rows.Count)InvalidateRow(old);};
    }
    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        if(e.RowIndex>=0&&e.RowIndex==_hover&&!Rows[e.RowIndex].Selected&&e.CellStyle is {} style)style.BackColor=UiTheme.Raised;
        base.OnCellPainting(e);
    }
}
