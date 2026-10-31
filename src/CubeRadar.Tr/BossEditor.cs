using CubeRadar.Core.Radar;

namespace CubeRadar.Tr;

internal sealed class BossEditor : Form
{
    public BossEvent? Result {get;private set;}
    private readonly bool _preview;
    protected override bool ShowWithoutActivation=>_preview;
    public BossEditor(BossEvent? existing=null,bool preview=false)
    {
        _preview=preview;
        var e=existing??new BossEvent {OnceAt=DateTimeOffset.UtcNow.AddMinutes(30)};
        Text=existing is null?"Boss / etkinlik ekle":"Boss / etkinlik düzenle";
        BackColor=RadarForm.Bg;ForeColor=RadarForm.Fg;Font=new Font("Segoe UI",10);ClientSize=new(540,660);
        AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);MinimumSize=new(540,560);StartPosition=FormStartPosition.CenterParent;
        var scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true};Controls.Add(scroll);
        var layout=new TableLayoutPanel {Dock=DockStyle.Top,Height=600,Padding=new(16),ColumnCount=2,RowCount=13};
        layout.ColumnStyles.Add(new(SizeType.Absolute,150));layout.ColumnStyles.Add(new(SizeType.Percent,100));scroll.Controls.Add(layout);
        TextBox Input(string label,string value,int row)
        {var text=new TextBox {Text=value,Dock=DockStyle.Fill,MaxLength=row==3?1000:300};UiTheme.Input(text);Add(label,text,row);return text;}
        void Add(string label,Control c,int row)
        {layout.RowStyles.Add(new(SizeType.Absolute,row==7?80:40));layout.Controls.Add(new Label {Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);layout.Controls.Add(c,1,row);}
        var name=Input("Boss / etkinlik",e.Name,0);var map=Input("Harita",e.Map,1);var location=Input("Bölge / konum",e.Location,2);var link=Input("Harita bağlantısı",e.MapUrl,3);
        var zone=ClockZone.Picker(e.ZoneId);UiTheme.Input(zone);Add("Saatin ait olduğu yer",zone,4);
        var kind=new ComboBox {Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};kind.Items.AddRange(["Tek sefer · tarih/saat","Haftalık / günlük"]);kind.SelectedIndex=e.OnceAt is null?1:0;Add("Takvim türü",kind,5);
        UiTheme.Input(kind);
        var date=new DateTimePicker {Format=DateTimePickerFormat.Custom,CustomFormat="dd.MM.yyyy HH:mm",Dock=DockStyle.Fill,Value=TimeZoneInfo.ConvertTime(e.OnceAt??DateTimeOffset.UtcNow.AddMinutes(30),TimeZoneInfo.FindSystemTimeZoneById(e.ZoneId)).DateTime};Add("Tek seferlik saat",date,6);
        var days=new CheckedListBox {Dock=DockStyle.Fill,CheckOnClick=true,MultiColumn=true,ColumnWidth=90,BackColor=UiTheme.Raised,ForeColor=UiTheme.Text,BorderStyle=BorderStyle.None};
        DayOfWeek[] order=[DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday,DayOfWeek.Saturday,DayOfWeek.Sunday];
        string[] labels=["Pzt","Sal","Çar","Per","Cum","Cmt","Paz"];
        for(var i=0;i<7;i++)days.Items.Add(labels[i],e.Days.Count==0||e.Days.Contains(order[i]));Add("Haftanın günleri",days,7);
        var times=Input("Saatler · 01:00,04:00",string.Join(",",e.Times),8);
        var alerts=Input("Uyarı · dakika önce",string.Join(",",e.AlertMinutes),9);
        var enabled=new ToggleSwitch {Text="Sesli uyarı açık",Checked=e.Enabled,AutoSize=true};Add("Boss uyarısı",enabled,10);
        var confirmed=new ToggleSwitch {Text="Saati doğruladım",Checked=e.ClockConfirmed,AutoSize=true};Add("Saat doğrulaması",confirmed,11);
        var save=new HudButton {Text="Kaydet",Dock=DockStyle.Fill,Tone=ButtonTone.Primary};Add("",save,12);
        void SetKind(){date.Enabled=kind.SelectedIndex==0;days.Enabled=times.Enabled=kind.SelectedIndex==1;}
        kind.SelectedIndexChanged+=(_,_)=>SetKind();SetKind();
        save.Click+=(_,_)=>
        {
            try
            {
                var selectedZone=((ClockZone)zone.SelectedItem!).Id;
                var updated=e with {Name=name.Text.Trim(),Map=map.Text.Trim(),Location=location.Text.Trim(),MapUrl=link.Text.Trim(),ZoneId=selectedZone,
                    Enabled=enabled.Checked,ClockConfirmed=confirmed.Checked,Source=existing is null?"Kullanıcı tarafından girildi":e.Source,
                    OnceAt=kind.SelectedIndex==0?BossClock.FromLocal(date.Value,selectedZone):null,
                    Times=kind.SelectedIndex==1?times.Text.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Distinct().ToList():[],
                    Days=Enumerable.Range(0,7).Where(days.GetItemChecked).Select(i=>order[i]).ToList(),
                    AlertMinutes=alerts.Text.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).Distinct().ToList()};
                var error=BossClock.Validate(updated);
                if(kind.SelectedIndex==1&&(updated.Times.Count==0||updated.Days.Count==0))error="En az bir gün ve saat seç.";
                if(error is not null)throw new ArgumentException(error);
                Result=updated;DialogResult=DialogResult.OK;Close();
            }
            catch(Exception ex)when(ex is ArgumentException or FormatException or OverflowException){DialogWindows.Message(this,ex.Message,"Takvim kaydedilemedi");}
        };
        AcceptButton=save;
    }
}
