using Avalonia.Controls;
using Avalonia.Controls.Templates;
using SpotDesk.Core.Models;
using SpotDesk.UI.ViewModels;

namespace SpotDesk.UI.Views;

/// <summary>
/// Selects the correct session view (RdpView / SshView / VncView) based on
/// the tab's Protocol property. Used as ContentTemplate on the main ContentControl.
/// Implements IDataTemplate explicitly — no reflection, NativeAOT-safe.
/// </summary>
public sealed class SessionViewSelector : IDataTemplate
{
    public bool Match(object? data) => data is SessionTabViewModel;

    public Control Build(object? data)
    {
        if (data is not SessionTabViewModel vm)
            return new TextBlock { Text = "No session" };

        Control view = vm.Protocol switch
        {
            Protocol.Ssh => new SshView(),
            Protocol.Vnc => new VncView(),
            _            => new RdpView(),
        };

        view.DataContext = data;
        return view;
    }
}
