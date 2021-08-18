using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Akka.Actor;
using System.IO;
namespace ChartApp.Actors
{
    public class ChartingActor : ReceiveActor, IWithUnboundedStash
    {
        /// <summary>
        /// Maximum number of points we will allow in a series
        /// </summary>
        public const int MaxPoints = 300;

        /// <summary>
        /// Incrementing counter we use to plot along the X-axis
        /// </summary>
        private int xPosCounter = 0;

        #region Messages

        public class InitializeChart//初始化图
        {
            public InitializeChart(Dictionary<string, Series> initialSeries)
            {
                InitialSeries = initialSeries;
            }

            public Dictionary<string, Series> InitialSeries { get; private set; }
        }

        /// <summary>
        /// Add a new <see cref="Series"/> to the chart
        /// </summary>
        public class AddSeries  //增加序列
        {
            public AddSeries(Series series)
            {
                Series = series;
            }

            public Series Series { get; private set; }
        }

        /// <summary>
        /// Remove an existing <see cref="Series"/> from the chart
        /// </summary>
        public class RemoveSeries //减少序列
        {
            public RemoveSeries(string seriesName)
            {
                SeriesName = seriesName;
            }

            public string SeriesName { get; private set; }
        }

        /// <summary>
        /// Toggles the pausing between charts
        /// </summary>
        public class TogglePause { } //暂停

        
        public class ClickSave
        {    //点击保存消息类
            public DirectoryInfo savepath;
            public ClickSave(DirectoryInfo filepath) {
                savepath = filepath;
            }
        }
        #endregion

        private readonly Chart _chart;
        private Dictionary<string, Series> _seriesIndex;
        private Dictionary<string, Series> all_points_series;
        private readonly Button _pauseButton;
        private readonly Main parent;
        public ChartingActor(Chart chart, Button pauseButton) : this(chart, new Dictionary<string, Series>(), pauseButton)
        {
        }

        public ChartingActor(Chart chart, Button pauseButton, Main main) : this(chart, new Dictionary<string, Series>(), pauseButton)
        {
            this.parent = main;//设置刷新时间时使用
        }

        public ChartingActor(Chart chart, Dictionary<string, Series> seriesIndex, Button pauseButton)
        {
            _chart = chart;//构造函数
            _seriesIndex = seriesIndex;
            _pauseButton = pauseButton;
            Charting();
        }

        private void Charting()
        {
            //消息处理机制进行图表的一些操作，消息机制就是窗体和后台通信
            Receive<InitializeChart>(ic => HandleInitialize(ic));
            Receive<AddSeries>(addSeries => HandleAddSeries(addSeries));
            Receive<RemoveSeries>(removeSeries => HandleRemoveSeries(removeSeries));
            Receive<Metric>(metric => HandleMetrics(metric));//得到数值
            Receive<TogglePause>(pause =>
            {
                SetPauseButtonText(true);
                BecomeStacked(Paused);
            });
            Receive<ClickSave>(clicksave =>
            {
                save_all_points(clicksave.savepath);
                //SaveToFile("abc", "abc", true);
            });
        }

        private void SetPauseButtonText(bool paused)
        {
            _pauseButton.Text = string.Format("{0}", !paused ? "PAUSE ||" : "RESUME |>");
        }

        private void Paused()
        {
            // while paused, we need to stash AddSeries & RemoveSeries messages
            Receive<AddSeries>(addSeries => Stash.Stash());
            Receive<RemoveSeries>(addSeries => Stash.Stash());
            Receive<Metric>(metric => HandleMetricsPaused(metric));
            Receive<TogglePause>(pause =>
            {
                SetPauseButtonText(false);
                UnbecomeStacked();
                // ChartingActor is leaving the paused state, put messages back into mailbox for processing
                // under new behavior
                Stash.UnstashAll();
            });
            Receive<ClickSave>(click =>
            {
                save_all_points(click.savepath);
                //SaveToFile("abc", "abc", true);
            });
        }

        private void save_all_points(DirectoryInfo directory)
        {
            string[] names = { "Cpu", "Network", "Disk", "Memory" };
            string filepath = directory.FullName + "/test.txt";
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(filepath, false))
            {
                file.WriteLine("in all points");
                
                foreach (var seriesName in names)
                {
                    file.WriteLine("data " + seriesName);
                    if (all_points_series.ContainsKey(seriesName))
                    {
                        var series = all_points_series[seriesName];
                        //List lst = series.Points.ToList();
                        for (int i = 0; i < series.Points.Count; i++)
                            file.WriteLine(series.Points[i]);
                    }
                }
            }
            //Console.WriteLine("in all points");
            ////string[] names = {"Cpu", "Network", "Disk", "Memory"};
            //foreach (var seriesName in names) {
            //    Console.WriteLine("name is " + seriesName);
            //    if (all_points_series.ContainsKey(seriesName)){
            //        var series = all_points_series[seriesName];
            //        //List lst = series.Points.ToList();
            //        for(int i = 0; i<series.Points.Count; i++)
            //            Console.WriteLine(series.Points[i]);
            //    }
            //}
        }

        #region Individual Message Type Handlers

        private void HandleMetricsPaused(Metric metric)
        {
            if (!string.IsNullOrEmpty(metric.Series) && _seriesIndex.ContainsKey(metric.Series))
            {
                var series = _seriesIndex[metric.Series];
                var series_cp = all_points_series[metric.Series];
                series_cp.Points.AddXY(xPosCounter, 0.0d);
                series.Points.AddXY(xPosCounter++, 0.0d); //set the Y value to zero when we're paused
                try
                {
                    while (series.Points.Count > MaxPoints) series.Points.RemoveAt(0);

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        
                }
                SetChartBoundaries();
            }
        }

        private void HandleInitialize(InitializeChart ic)
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            all_points_series = new Dictionary<string, Series>();
            if (ic.InitialSeries != null)
            {
                //swap the two series out
                _seriesIndex = ic.InitialSeries;
           
            }

            //delete any existing series
            _chart.Series.Clear();

            //set the axes up
            var area = _chart.ChartAreas[0];
            area.AxisX.IntervalType = DateTimeIntervalType.Number;
            area.AxisY.IntervalType = DateTimeIntervalType.Number;

            SetChartBoundaries();

            //attempt to render the initial chart
            if (_seriesIndex.Any())
            {
                foreach (var series in _seriesIndex)
                {
                    //force both the chart and the internal index to use the same names
                    series.Value.Name = series.Key;
                    _chart.Series.Add(series.Value);
                }
            }

            SetChartBoundaries();
        }

        private void HandleAddSeries(AddSeries series)
        {
            if (!string.IsNullOrEmpty(series.Series.Name) && !_seriesIndex.ContainsKey(series.Series.Name))
            {
                _seriesIndex.Add(series.Series.Name, series.Series);
                all_points_series.Add(series.Series.Name, new Series());
                _chart.Series.Add(series.Series);
                SetChartBoundaries();
            }
        }

        private void HandleRemoveSeries(RemoveSeries series)
        {
            if (!string.IsNullOrEmpty(series.SeriesName) && _seriesIndex.ContainsKey(series.SeriesName))
            {
                var seriesToRemove = _seriesIndex[series.SeriesName];
                all_points_series.Remove(series.SeriesName);
                _seriesIndex.Remove(series.SeriesName);
                _chart.Series.Remove(seriesToRemove);
                SetChartBoundaries();
            }
        }

        private void HandleMetrics(Metric metric)
        {
            if (!string.IsNullOrEmpty(metric.Series) && _seriesIndex.ContainsKey(metric.Series))
            {
                try
                {
                    var series = _seriesIndex[metric.Series];//画图数据
                    var series_cp = all_points_series[metric.Series];//全部数据——历史数据
                    series_cp.Points.AddXY(xPosCounter, metric.CounterValue);//点数据写入chart
                    series.Points.AddXY(xPosCounter++, metric.CounterValue);//写入全部数据用于保存
                    //Console.WriteLine("in Handle Metrics, ")
                    parent.TB2update(metric.Series, metric.CounterValue);//实时更新数值
                    while (series.Points.Count > MaxPoints) series.Points.RemoveAt(0);
                    
                    //parent.setLabel(metric.Series + "利用率：\n" + Convert.ToString(metric.CounterValue));
                    SetChartBoundaries();
                }
                catch (NullReferenceException nre)
                {
                    Console.WriteLine(nre.ToString());
                }
                catch (Exception nre)
                {
                    Console.WriteLine(nre.ToString());
                }
                
                
            }
        }

        #endregion

        private void SetChartBoundaries()
        {
            double maxAxisX, maxAxisY, minAxisX, minAxisY = 0.0d;
            var allPoints = _seriesIndex.Values.Aggregate(new HashSet<DataPoint>(),
                (set, series) => new HashSet<DataPoint>(set.Concat(series.Points)));
            var yValues = allPoints.Aggregate(new List<double>(), (list, point) => list.Concat(point.YValues).ToList());
            maxAxisX = xPosCounter;
            minAxisX = xPosCounter - MaxPoints;
            maxAxisY = yValues.Count > 0 ? Math.Ceiling(yValues.Max()) : 1.0d;
            minAxisY = yValues.Count > 0 ? Math.Floor(yValues.Min()) : 0.0d;
            

            if (allPoints.Count > 2)
            {
                var area = _chart.ChartAreas[0];
                area.AxisX.Minimum = minAxisX;
                area.AxisX.Maximum = maxAxisX;
                area.AxisY.Minimum = minAxisY;
                area.AxisY.Maximum = maxAxisY;
            }
            
            var area1 = _chart.ChartAreas[0];
            if (area1.AxisY.Minimum >= area1.AxisY.Maximum)
                area1.AxisY.Maximum = area1.AxisY.Minimum + 1;
            if (area1.AxisX.Minimum >= area1.AxisX.Maximum)
                area1.AxisX.Maximum = area1.AxisX.Minimum + 1;
            //Console.WriteLine("areaAxis " + area1.AxisX.Minimum + " " +
            //area1.AxisX.Maximum + " " +
            //area1.AxisY.Minimum + " " +
            //area1.AxisY.Maximum);
            
        }

        public IStash Stash { get; set; }


       
    }
}