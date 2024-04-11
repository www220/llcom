using llcom.LuaEnv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static llcom.Pages.SocketClientPage;
using System.Net.NetworkInformation;

namespace llcom.Pages
{
    /// <summary>
    /// UdpLocalPage.xaml 的交互逻辑
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class UdpLocalPage : Page
    {
        public UdpLocalPage()
        {
            InitializeComponent();
        }


        public bool IsConnected { get; set; } = false;

        private static bool loaded = false;
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (loaded)
                return;
            loaded = true;
            RefreshIp();
            //绑定
            MainGrid.DataContext = this;
            IpPortTextBox.DataContext = Tools.Global.setting;

            //适配一下通用通道
            LuaApis.SendChannelsRegister("udp-server", (data, t) =>
            {
                if (Server != null)
                {
                    if (data != null)
                    {
                        return Send(data, recvAddr);
                    }
                    else if (t != null)
                    {
                        return Send(t.Get<byte[]>("data"), t.Get<string>("from"));
                    }
                    else
                        return false;
                }
                else
                    return false;
            });
        }

        private string recvAddr = null;
        private bool Send(byte[] buff, string hostname)
        {
            try
            {
                if (hostname == null)
                {
                    ShowData($"❗ Send data error hostname is null");
                    return false;
                }
                string[] parts = hostname.Split(':');
                if ((parts.Length == 2) && (IPAddress.TryParse(parts[0], out IPAddress ipAddress)) && (int.TryParse(parts[1], out int port)))
                {
                    Server.Send(buff, buff.Length, new IPEndPoint(ipAddress, port));
                    ShowData($" ← send ({(string)hostname})", buff, true);
                    return true;
                }
                else
                {
                    ShowData($"❗ Send data error hostname not invalid");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ShowData($"❗ Send data error {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 刷新本机ip列表
        /// </summary>
        private void RefreshIp()
        {
            IpListComboBox.Items.Clear();
            IpListComboBox.Items.Add("0.0.0.0");
            IpListComboBox.Items.Add("::");
            var temp = new List<string>();
            try
            {
                string name = Dns.GetHostName();
                IPAddress[] ipadrlist = Dns.GetHostAddresses(name);
                foreach (IPAddress ipa in ipadrlist)
                {
                    if (ipa.AddressFamily == AddressFamily.InterNetwork ||
                        ipa.AddressFamily == AddressFamily.InterNetworkV6)
                        temp.Add(ipa.ToString());
                }
            }
            catch { }
            //去重
            temp.Distinct().ToList().ForEach(ip => IpListComboBox.Items.Add(ip));
            IpListComboBox.SelectedIndex = 0;
        }
        private void ShowData(string title, byte[] data = null, bool send = false)
        {
            Tools.Logger.ShowDataRaw(new Tools.DataShowRaw
            {
                title = $"🗑 local udp server: {title}",
                data = data ?? new byte[0],
                color = send ? Brushes.DarkRed : Brushes.DarkGreen,
            });
        }


        private UdpClient Server = null;

        /// <summary>
        /// 开始监听服务器
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        private bool StartServer(string ip, int port)
        {
            if (Server != null)
                return false;
            IPAddress localAddr = IPAddress.Parse(ip);
            IPEndPoint IpEndPoint = new IPEndPoint(localAddr, port);
            Server = new UdpClient(IpEndPoint);

            var isV6 = ip.Contains(":");
            ShowData($"🗑 {(isV6 ? "[" : "")}{ip}{(isV6 ? "]" : "")}:{port}");

            AsyncCallback newConnectionCb = null;
            newConnectionCb = new AsyncCallback((ar) =>
            {
                try
                {
                    UdpClient u = ((UdpState)(ar.AsyncState)).u;
                    IPEndPoint e = ((UdpState)(ar.AsyncState)).e;

                    byte[] receiveBytes = u.EndReceive(ar, ref e);
                    var isV6 = e.Address.ToString().Contains(":");
                    recvAddr = $"{(isV6 ? "[" : "")}{e.Address}{(isV6 ? "]" : "")}:{e.Port}";
                    ShowData($" → receive ({recvAddr})", receiveBytes);
                    LuaApis.SendChannelsReceived("udp-server", new
                    {
                        from = recvAddr,
                        data = receiveBytes
                    });
                    Server.BeginReceive(newConnectionCb, ar.AsyncState);
                }
                catch { }
            }); 
            UdpState s = new UdpState();
            s.e = IpEndPoint;
            s.u = Server;
            try
            {
                Server.BeginReceive(newConnectionCb, s);
            }
            catch (Exception ex)
            {
                ShowData($"❗ Server create error {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 关闭服务器，断开所有连接
        /// </summary>
        private void StopServer()
        {
            Server?.Close();
            Server?.Dispose();
            Server = null;
        }

        private void RefreshIpButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshIp();
        }

        private void ListenButton_Click(object sender, RoutedEventArgs e)
        {
            int port;
            if (int.TryParse(IpPortTextBox.Text, out port))
            {
                try
                {
                    IsConnected = StartServer(IpListComboBox.Text, port);
                }
                catch (Exception err)
                {
                    Tools.MessageBox.Show(err.Message);
                }
            }
        }

        private void StopListenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StopServer();
                IsConnected = false;
                ShowData($"🚫 server closed");
            }
            catch { }
        }
    }

    public struct UdpState
    {
        public UdpClient u;
        public IPEndPoint e;
    }
}
