namespace CubeRadar.Tr;

internal sealed partial class RadarForm
{
    internal void VerifyRadarOnly()
    {
        if(!_previewMode)throw new InvalidOperationException("Yalnız önizleme doğrulaması.");
        if(_pages.Count!=4||_navigation.Length!=4||_navigation.Any(n=>n.Text.Contains("Otomatik",StringComparison.Ordinal)))
            throw new InvalidOperationException("Kaldırılan otomatik av sayfası arayüzde kaldı.");
        var types=typeof(RadarForm).Assembly.GetTypes();
        if(types.Any(t=>t.Name.StartsWith("Hunt",StringComparison.Ordinal)||t.Name.StartsWith("WindowsHunt",StringComparison.Ordinal)||
            t.Name.StartsWith("MacroTrace",StringComparison.Ordinal)||t.Name.StartsWith("WindowsMacro",StringComparison.Ordinal))||
            typeof(CubeRadar.Core.Radar.RadarSnapshot).Assembly.GetTypes().Any(t=>t.Namespace=="CubeRadar.Core.Hunting"))
            throw new InvalidOperationException("Kaldırılan av/giriş kodu derlemede kaldı.");
        foreach(var type in types)
            foreach(var method in type.GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance))
            {
                var import=System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Runtime.InteropServices.DllImportAttribute>(method);
                if(import?.EntryPoint is {} name&&(name=="SendInput"||name.StartsWith("SetWindowsHookEx",StringComparison.Ordinal)))
                    throw new InvalidOperationException("Oyuna input/hook girişi derlemede kaldı.");
            }
    }
    internal void VerifyHudInteractions()
    {
        if(!_previewMode)throw new InvalidOperationException("Yalnız önizleme doğrulaması.");
        PreviewScenario();var row=_cubeRows[90001];_selected=90002;_lockTarget.Checked=true;
        for(var i=0;i<5;i++)UpdateView();
        if(_target?.ObjectId!=90002||!ReferenceEquals(row,_cubeRows[90001]))throw new InvalidOperationException("Küp satırı veya hedef kimliği korunmadı.");
        _heightFilter.SelectedIndex=2;UpdateView();
        if(_state?.Cubes.Count!=1||_state.Cubes[0].ObjectId!=90001||_target is not null||_targetTracker.LockedId!=90002)throw new InvalidOperationException("Yükseklik filtresi / gizlenen kilitli hedef bozuldu.");
        _heightFilter.SelectedIndex=0;_lockTarget.Checked=false;UpdateView();
        _navigation[1].PerformClick();if(_page!=1||!_pages[1].Visible||_pages[0].Visible)throw new InvalidOperationException("Boss gezinmesi çalışmadı.");
        _navigation[2].PerformClick();if(_page!=2||!_pages[2].Visible||!_traffic.Visible)throw new InvalidOperationException("Teknik bilgiler Ayarlar içinde erişilemiyor.");
        _volume.Value=37;if(_preferences.Value.Volume!=37)throw new InvalidOperationException("Ses seviyesi kaydedilmedi.");_volume.Value=85;
        _sound.Checked=false;if(_preferences.Value.Sound)throw new InvalidOperationException("Ses anahtarı çalışmadı.");_sound.Checked=true;
        Navigate(0);_stackedListExpanded=true;ClientSize=new(700,900);ReflowRadar();PerformLayout();
        if(!_list.Visible||!_lockTarget.Visible||_radar.Height<200)throw new InvalidOperationException("Dar pencere radar/listesi kullanılamıyor.");
        _stackedListExpanded=false;ReflowRadar();if(_list.Visible||_lockTarget.Visible)throw new InvalidOperationException("Küp listesi katlanmadı.");
        ClientSize=new(1080,840);_stackedListExpanded=null;ReflowRadar();
        if(!_list.Visible||!_lockTarget.Visible)throw new InvalidOperationException("Geniş pencere listesi geri gelmedi.");
        OpenMini();var mini=_mini!;var original=mini.Size;
        mini.SetCompact(true);if(!mini.IsCompact||mini.Width>=original.Width)throw new InvalidOperationException("Mini kompakt görünüm çalışmadı.");
        mini.SetClickThrough(true);if(!mini.NativeClickThrough)throw new InvalidOperationException("Mini tıklama geçişi açılmadı.");
        mini.SetClickThrough(false);if(mini.NativeClickThrough)throw new InvalidOperationException("Mini tıklama geçişi geri alınmadı.");
        WindowState=FormWindowState.Minimized;Application.DoEvents();
        try
        {
            if(mini.Owner is not null||!mini.Visible||!NativeWindowVisible(mini.Handle)||mini.WindowState==FormWindowState.Minimized||!mini.TopMost)
                throw new InvalidOperationException("Ana pencere küçültülünce mini radar oyun üzerinde bağımsız kalmadı.");
            UpdateView();
            if(_state?.Cubes.Count!=3||_target is null)throw new InvalidOperationException("Ana pencere küçültülünce radar güncellemesi durdu.");
        }
        finally {WindowState=FormWindowState.Normal;Application.DoEvents();}
        mini.SetCompact(false);mini.Close();UpdateView();
        if(_state?.Cubes.Count!=3||_target is null)throw new InvalidOperationException("Mini kapandıktan sonra ana radar bozuldu.");
    }
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="IsWindowVisible")]
    [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool NativeWindowVisible(IntPtr window);
    internal void PreviewExpandedList(){_stackedListExpanded=true;ReflowRadar();UpdateView();}
    internal void PreviewEmpty()
    {
        if(!_previewMode||_previewSnapshot is null)return;
        _lockTarget.Checked=false;_previewSnapshot=_previewSnapshot with {Cubes=[]};UpdateView();
    }
    internal void PreviewSettingsSection(bool advanced)
    {
        PreviewSettings();((Panel)_pages[2]).ScrollControlIntoView(advanced?_advancedCard:_volume.Parent!.Parent!);
    }
    internal static void ScalePreview(Form form,float factor)
    {
        // Deterministic control/text scaling proof. It does not change the user's Windows display settings.
        static IEnumerable<Control> All(Control parent)=>parent.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(All(c)));
        var controls=All(form).Prepend(form).Select(c=>(Control:c,Family:c.Font.FontFamily,Size:c.Font.Size,Style:c.Font.Style)).ToArray();
        form.Scale(new SizeF(factor,factor));
        foreach(var c in controls)c.Control.Font=new(c.Family,c.Size*factor,c.Style);
        foreach(var canvas in controls.Select(c=>c.Control).OfType<RadarCanvas>())canvas.PreviewScale=factor;
        form.PerformLayout();
    }
}
