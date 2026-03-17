using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using SpotDesk.UI.ViewModels;

namespace SpotDesk.UI.Dialogs;

public partial class ImportWizard : Window
{
    public ImportWizard()
    {
        InitializeComponent();
        SetupDragDrop();
    }

    private void SetupDragDrop()
    {
        var dropZone = this.FindControl<Border>("DropZone");
        if (dropZone is null) return;

        DragDrop.SetAllowDrop(dropZone, true);

#pragma warning disable CS0618 // DataFormats.Files / DragEventArgs.Data are deprecated but still functional
        dropZone.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = e.Data.Contains(DataFormats.Files)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        });

        dropZone.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (e.Data.GetFiles() is IEnumerable<IStorageItem> files)
            {
                var first = files.FirstOrDefault();
                if (first is IStorageFile sf && DataContext is ImportWizardViewModel vm)
                    vm.SetFilePath(sf.Path.LocalPath);
            }
        });
#pragma warning restore CS0618
    }
}
