using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace CatPost_Scanner
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public String Scope = "wall,groups";
        public String Token;
        public String version = "5.64";
        public String User = "";

        private void CenterWindowOnScreen(Window window)
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double windowWidth = window.Width;
            double windowHeight = window.Height;
            window.Left = (screenWidth / 2) - (windowWidth / 2);
            window.Top = (screenHeight / 2) - (windowHeight / 2);
        }

        public MainWindow()
        {
            InitializeComponent();
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate { }));
            CenterWindowOnScreen(this);
            browser.Source = new Uri("https://oauth.vk.com/authorize?client_id=6024733&redirect_uri=https://oauth.vk.com/blank.html&display=popup&scope=" + Scope + "&response_type=token&v=" + version);
            browser.LoadingFrameComplete += Browser_LoadingFrameComplete;
        }

        private void Browser_LoadingFrameComplete(object sender, Awesomium.Core.FrameEventArgs e)
        {
            String url = browser.Source.ToString();
            if (url.Contains("blank.html#"))
            {
                var start = url.IndexOf("access_token=") + 13;
                var end = url.IndexOf("&", start);
                Token = url.Substring(start, end - start);
                var start1 = url.IndexOf("user_id=") + 8;
                User = url.Substring(start1);
                Hide();
                Window1 window1 = new Window1(this);
                CenterWindowOnScreen(window1);
                window1.Show();
            }
        }
    }
}
