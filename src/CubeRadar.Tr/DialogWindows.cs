namespace CubeRadar.Tr;

/// <summary>Use a real top-level owner and keep sibling overlays behind modal input.</summary>
internal static class DialogWindows
{
    public static DialogResult Show(Form dialog,Control source)
    {
        using var scope=new ModalScope(source);
        dialog.TopMost=scope.Owner.TopMost;
        dialog.ShowInTaskbar=false;
        dialog.StartPosition=FormStartPosition.CenterParent;
        return dialog.ShowDialog(scope.Owner);
    }

    public static DialogResult Show(CommonDialog dialog,Control source)
    {
        using var scope=new ModalScope(source);
        return dialog.ShowDialog(scope.Owner);
    }

    public static DialogResult Message(Control source,string text,string caption,
        MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None)
    {
        using var scope=new ModalScope(source);
        return MessageBox.Show(scope.Owner,text,caption,buttons,icon);
    }

    private sealed class ModalScope : IDisposable
    {
        public Form Owner {get;}
        private readonly Form[] _overlays;
        public ModalScope(Control source)
        {
            Owner=source.FindForm()??throw new InvalidOperationException("İletişim kutusu için ana pencere bulunamadı.");
            _overlays=Application.OpenForms.Cast<Form>().Where(f=>f!=Owner&&f.Visible&&f.TopMost).ToArray();
            foreach(var overlay in _overlays)overlay.TopMost=false;
        }
        public void Dispose()
        {
            foreach(var overlay in _overlays)
                if(!overlay.IsDisposed&&!overlay.Disposing)overlay.TopMost=true;
        }
    }
}
