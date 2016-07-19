using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace NetworkSample
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        StorageFolder folder;
        List<DownloadOperation> operations;

        public MainPage()
        {
            this.InitializeComponent();
            string localFolderPath = ApplicationData.Current.LocalFolder.Path;
            string defaultPath = localFolderPath.Substring(0, localFolderPath.IndexOf("AppData\\Local")) + "Downloads\\"+Windows.ApplicationModel.Package.Current.Id.FamilyName+"!App";
            folderTbx.Text = defaultPath;
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            await InitCurrentDownloads();
        }

        private async Task InitCurrentDownloads()
        {
            operations = new List<DownloadOperation>();
               var downloads = await BackgroundDownloader.GetCurrentDownloadsAsync();
            if(downloads.Count > 0)
            {
                List<Task> tasks = new List<Task>();
                foreach(var download in downloads)
                {
                    tasks.Add(HandleDownloadAsync(download,false));
                }
                await Task.WhenAll(tasks);
            }
        }

        private async void begin_Click(object sender, RoutedEventArgs e)
        {
            Uri source = null;
            StorageFile file = null;
            if (!Uri.TryCreate(urlTbx.Text.Trim(), UriKind.Absolute, out source))
                return;
            
            try
            {
                if (null == folder)
                    file = await DownloadsFolder.CreateFileAsync(urlTbx.Text.Substring(urlTbx.Text.LastIndexOf('/') + 1), CreationCollisionOption.GenerateUniqueName);
                else
                {
                    file = await folder.CreateFileAsync(urlTbx.Text.Substring(urlTbx.Text.LastIndexOf('/') + 1), CreationCollisionOption.GenerateUniqueName);
                }
            }
            catch { return; }

            BackgroundDownloader downloader = new BackgroundDownloader();
            var operation = downloader.CreateDownload(source, file);
            operation.Priority = BackgroundTransferPriority.Default;
            await HandleDownloadAsync(operation,true);
        }

        private async Task HandleDownloadAsync(DownloadOperation operation,bool isStart)
        {
            operations.Add(operation);
            Progress<DownloadOperation> progress = new Progress<DownloadOperation>((o) =>
            {
                var currentProgress = o.Progress;
                result.Text = string.Format("{0}/{1}    {2}%", currentProgress.BytesReceived, currentProgress.TotalBytesToReceive, currentProgress.BytesReceived * 100 / currentProgress.TotalBytesToReceive);
            });
            try
            {
                if (isStart)
                    await operation.StartAsync().AsTask(cts.Token, progress);
                else
                    await operation.AttachAsync().AsTask(cts.Token, progress);
                status.Text = "StatusCode: " + operation.GetResponseInformation().StatusCode.ToString();
            }
            catch (TaskCanceledException te)
            {

            }
            catch (Exception ex)
            {
                status.Text = "StatusCode: " + operation.GetResponseInformation().StatusCode.ToString();
            }
            finally
            {
                operations.Remove(operation);
            }
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {
          foreach(var operation in operations)
                operation.Pause();
        }

        private async void browser_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.FileTypeFilter.Add("*");
            folder = await picker.PickSingleFolderAsync();
            if (null != folder)
            {
                folderTbx.Text = folder.Path;
            }
        }

        private void cancel_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
            cts.Dispose();
            cts = new CancellationTokenSource();
            operations = new List<DownloadOperation>();
        }

        private void resume_Click(object sender, RoutedEventArgs e)
        {
            foreach (var operation in operations)
                operation.Resume();
        }
    }
}
