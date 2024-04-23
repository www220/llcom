using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

namespace llcom.Pages
{
    /// <summary>
    /// PlotPage.xaml 的交互逻辑
    /// </summary>
    public partial class PlotPage : Page
    {
        public PlotPage()
        {
            InitializeComponent();
        }

        //最多十个图像
        private SignalPlotXY[] PlotXY = new SignalPlotXY[10];
        //最大点数量
        private static int MaxPoints = 60000;

        private ScottPlot.Plottable.Crosshair ch = null;

        private ScottPlot.Styles.IStyle[] Styles = ScottPlot.Style.GetStyles();
        private int StyleNow = -1;

        private bool NeedRefresh = true;

        bool first = true;
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!first)
                return;
            first = false;

            //先把曲线都创建出来
            for (int i = 0; i < PlotXY.Length; i++) 
            {
                PlotXY[i] = Plot.Plot.AddSignalXY(new double[MaxPoints/10], new double[MaxPoints/10]);
                PlotXY[i].IsVisible = false;
            }
            Plot.Plot.SetAxisLimitsX(Environment.TickCount-MaxPoints, Environment.TickCount);
            Plot.Plot.SetAxisLimitsY(-1, 1);
            ch = Plot.Plot.AddCrosshair(0,0);

            ch.Color = System.Drawing.Color.LightGray;
            ch.LineWidth = 2;

            //定时刷吧，要不然卡
            new Thread(() =>
            {
                while (true)
                {
                    if(NeedRefresh)
                    {
                        NeedRefresh = false;
                        this.Dispatcher.Invoke(new Action(delegate
                        {
                            try
                            {
                                Plot.Render();
                            }
                            catch { }
                        }));
                    }
                    Thread.Sleep(100);
                    if (Tools.Global.isMainWindowsClosed)
                        return;
                }
            }).Start();

            LuaEnv.LuaApis.LinePlotAdd += (s, e) => AddPoint(e.N, e.Line);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < PlotXY.Length; i++)
            {
                //所有曲线都不显示
                PlotXY[i].IsVisible = false;
            }
            Refresh();
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            StyleNow++;
            if(StyleNow >= Styles.Length)
                StyleNow = 0;
            Plot.Plot.Style(Styles[StyleNow]);
            Refresh();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Plot.Plot.SetAxisLimitsX(Environment.TickCount-MaxPoints, Environment.TickCount);
            //防止最大值最小值错误
            double min = int.MaxValue;
            double max = int.MinValue;
            for (int line = 0; line < PlotXY.Length; line++)
            {
                if (PlotXY[line].IsVisible)
                {
                    double ai = PlotXY[line].Ys.Min();
                    if (ai < min) min = ai;
                    double ax = PlotXY[line].Ys.Max();
                    if (ax > max) max = ax;
                }
            }
            if (min < max)
                Plot.Plot.SetAxisLimitsY(min-1, max+1);
            else
                Plot.Plot.SetAxisLimitsY(-1, 1);
            Refresh();
        }

        private void Plot_MouseMove(object sender, MouseEventArgs e)
        {
            var p = Plot.GetMouseCoordinates();
            ch.X = p.x;
            ch.Y = p.y;
            Refresh();
        }

        private void Refresh() => NeedRefresh = true;

        private void AddPoint(double d, int line)
        {
            if (line >= 10)
                return;
            double[] x = PlotXY[line].Xs;
            double[] y = PlotXY[line].Ys;
            //满了往前移动挤掉
            for (int i = 0; i < x.Length-1; i++)
            {
                x[i] = x[i+1];
                y[i] = y[i + 1];
            }
            x[x.Length - 1] = Environment.TickCount;
            y[x.Length - 1] = d;
            //超出数据范围的不进行显示
            int idx = 0;
            for (int i = x.Length - 1; i >= 0; i--)
            {
                if (x[i] < Environment.TickCount - MaxPoints)
                {
                    idx = i+1;
                    break;
                }
            }
            PlotXY[line].MinRenderIndex = idx;
            PlotXY[line].MaxRenderIndex = x.Length-1;
            PlotXY[line].IsVisible = true;
            Refresh();
        }
    }
}
