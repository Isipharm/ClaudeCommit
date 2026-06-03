using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ClaudeCommit.UI
{
    internal enum InfoBarMessageType { Info, Error }

    internal static class InfoBarHelper
    {
        public static async Task ShowAsync(
            AsyncPackage package,
            string message,
            CancellationToken cancellationToken,
            InfoBarMessageType type = InfoBarMessageType.Info)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var infoBarFactory = await package.GetServiceAsync(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;
            if (infoBarFactory == null) return;

            var imageMoniker = type == InfoBarMessageType.Error
                ? KnownMonikers.StatusError
                : KnownMonikers.StatusInformation;

            var model    = new InfoBarModel(new[] { new InfoBarTextSpan(message) }, imageMoniker, isCloseButtonVisible: true);
            var infoBar  = infoBarFactory.CreateInfoBar(model);

            var shell = await package.GetServiceAsync(typeof(SVsShell)) as IVsShell;
            object hostObj = null;
            shell?.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out hostObj);

            if (hostObj is IVsInfoBarHost host)
                host.AddInfoBar(infoBar);
        }

        public static Task ShowErrorAsync(AsyncPackage package, string message, CancellationToken cancellationToken)
            => ShowAsync(package, message, cancellationToken, InfoBarMessageType.Error);
    }
}
