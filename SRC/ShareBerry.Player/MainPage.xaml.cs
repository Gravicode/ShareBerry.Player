using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Popups;
using System.Net;
using System.Xml.Linq;
using System.Xml;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Windows.Storage;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ShareBerry.Player
{
    public class FileSP
    {
        public string Name { set; get; }
        public string FilePath { set; get; }
        public string Ext { set; get; }
    }
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        Windows.Storage.StorageFolder temporaryFolder = ApplicationData.Current.TemporaryFolder;

        private HttpClient _client;
        public string TempFileName { set; get; }
        //this is only sample, you can change later
        public string UserName { set; get; } = "administrator";
        public string Password { get; set; } = "pass@word1";
        public string Domain { set; get; } = "blitz";
        public string SpServiceUrl
        {
            set; get;
        } = "http://redvelvet/_vti_bin/Lists.asmx";
        public string ListName { set; get; } = "MP3Album";
        public MainPage()
        {
            this.InitializeComponent();
            InitApp();
        }

        void InitApp()
        {
            TxtDomain.Text = Domain;
            TxtUsername.Text = UserName;
            TxtPassword.Password = Password;
            TxtUrl.Text = SpServiceUrl.Replace("/_vti_bin/Lists.asmx", string.Empty);
            PlayBtn.IsEnabled = false;
            StopBtn.IsEnabled = false;
            BtnRetrieve.IsEnabled = false;

            BtnConnect.Click += async (x, y) =>
            {
                Domain = TxtDomain.Text;
                UserName = TxtUsername.Text;
                Password = TxtPassword.Password;
                SpServiceUrl = TxtUrl.Text + "/_vti_bin/Lists.asmx";
                if (string.IsNullOrEmpty(Domain) || string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(TxtUrl.Text))
                {
                    var z = new MessageDialog("Please input data correctly");
                    await z.ShowAsync();
                    return;
                }
                var handler = new HttpClientHandler { Credentials = new NetworkCredential(UserName, Password, Domain) };
                _client = new HttpClient(handler);
                if (await LoadSpList())
                {
                    BtnRetrieve.IsEnabled = true;
                }
                else
                {
                    BtnRetrieve.IsEnabled = false;
                    PlayBtn.IsEnabled = false;
                    StopBtn.IsEnabled = false;
                }
            };
            BtnRetrieve.Click += async (x, y) =>
            {
                if (await LoadDataFromSP())
                {
                    PlayBtn.IsEnabled = true;
                    StopBtn.IsEnabled = true;
                }
                else
                {
                    PlayBtn.IsEnabled = false;
                    StopBtn.IsEnabled = false;
                }
            };
            PlayBtn.Click += PlayBtn_Click;
            StopBtn.Click += StopBtn_Click;
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MyPlayer.CurrentState == MediaElementState.Playing)
            {
                MyPlayer.Stop();
            }
        }

        private async void PlayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ListMedia.SelectedIndex > -1)
            {
                if (MyPlayer.CurrentState == MediaElementState.Playing) MyPlayer.Stop();
                var item = ListMedia.SelectedItem as FileSP;
                TempFileName = "temp." + item.Ext;
                if (await DownloadMedia(item.FilePath))
                {
                    PlayMedia();
                }
            }
        }
        SPVelvet.ListsSoapClient GetServiceInstance()
        {
            var Basebinding = new System.ServiceModel.BasicHttpBinding(System.ServiceModel.BasicHttpSecurityMode.TransportCredentialOnly);
            Basebinding.Security.Transport.ClientCredentialType = System.ServiceModel.HttpClientCredentialType.Windows;
            Basebinding.MaxReceivedMessageSize = 999999999;
            System.ServiceModel.Channels.Binding binding = Basebinding;
            System.ServiceModel.EndpointAddress addr = new System.ServiceModel.EndpointAddress(SpServiceUrl);
            SPVelvet.ListsSoapClient client = new SPVelvet.ListsSoapClient(binding, addr);
            client.ClientCredentials.Windows.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;
            client.ClientCredentials.Windows.ClientCredential = new NetworkCredential(UserName, Password, Domain);
            return client;
        }
        async Task<bool> LoadSpList()
        {
            try
            {
                var client = GetServiceInstance();
                XElement query = new XElement("Query");
                XElement viewFields = new XElement("ViewFields");
                XElement queryOptions = new XElement("QueryOptions");
                var lists = await client.GetListCollectionAsync();
                CmbListName.Items.Clear();
                foreach (XElement node in lists.Body.GetListCollectionResult.Elements())
                {
                    CmbListName.Items.Add(node.Attribute("Title").Value.ToString());
                }
                if (CmbListName.Items.Count > 0)
                    CmbListName.SelectedIndex = 0;
                return true;
            }
            catch
            {
                return false;
            }
        }
        private async Task<bool> LoadDataFromSP()
        {
            try
            {
                ListName = CmbListName.SelectedItem.ToString();
                var datas = new List<FileSP>();
                var client = GetServiceInstance();
                XElement query = new XElement("Query");
                XElement viewFields = new XElement("ViewFields");
                XElement queryOptions = new XElement("QueryOptions");
                var lists = await client.GetListItemsAsync(ListName, null, query, viewFields, "100", queryOptions, null);
                HashSet<string> AllowedExt = new HashSet<string>();
                AllowedExt.Add("mp3");
                AllowedExt.Add("avi");
                AllowedExt.Add("mp4");
                AllowedExt.Add("wav");

                foreach (XElement node in lists.Body.GetListItemsResult.Descendants().Elements())
                {
                    var ext = node.Attribute("ows_File_x0020_Type").Value;
                    if (!AllowedExt.Contains(ext))
                    {
                        continue;
                    }
                    var newNode = new FileSP();
                    newNode.Name = node.Attribute("ows_FileLeafRef").Value.Split('#')[1];
                    newNode.FilePath = node.Attribute("ows_EncodedAbsUrl").Value;
                    newNode.Ext = node.Attribute("ows_File_x0020_Type").Value;

                    datas.Add(newNode);
                }
                ListMedia.ItemsSource = datas;
                if (datas.Count > 0)
                    ListMedia.SelectedIndex = 0;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DownloadMedia(string strURL)
        {

            HttpWebRequest request;
            HttpWebResponse response = null;
            StorageFile sampleFile = await temporaryFolder.CreateFileAsync(TempFileName,
                CreationCollisionOption.ReplaceExisting);
            try
            {
                request = (HttpWebRequest)WebRequest.Create(strURL);
                request.Credentials = new NetworkCredential(UserName, Password, Domain);
                response = (HttpWebResponse)await request.GetResponseAsync();
                Stream s = response.GetResponseStream();

                MemoryStream ms = new MemoryStream();
                BinaryWriter binWriter = new BinaryWriter(ms);

                byte[] read = new byte[256];
                int count = s.Read(read, 0, read.Length);
                while (count > 0)
                {
                    binWriter.Write(read);
                    count = s.Read(read, 0, read.Length);
                }
                await FileIO.WriteBytesAsync(sampleFile, ms.ToArray());

                //Close everything
                ms.Dispose();
                s.Dispose();
                response.Dispose();
                return true;
            }
            catch
            {
                return false;
            }
        }
        async void PlayMedia()
        {
            try
            {
                StorageFile sampleFile = await temporaryFolder.GetFileAsync(TempFileName);
                var stream = await sampleFile.OpenAsync(Windows.Storage.FileAccessMode.Read);
                MyPlayer.SetSource(stream, sampleFile.ContentType);
                MyPlayer.Play();
            }
            catch (Exception ex)
            {
                var z = new MessageDialog("Error - " + ex.Message);
                await z.ShowAsync();
            }
        }
    }
}
