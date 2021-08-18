using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using Akka.Actor;

namespace ChartApp.Actors
{
    /// <summary>
    /// Actor responsible for translating UI calls into ActorSystem messages
    /// </summary>
    public class PerformanceCounterCoordinatorActor : ReceiveActor
    {
        #region Message types

        /// <summary>
        /// Subscribe the <see cref="ChartingActor"/> to updates for <see cref="Counter"/>
        /// </summary>
        public class Watch
        {
            public CounterType Counter { get; private set; }

            public Watch(CounterType counter)
            {
                Counter = counter;
            }
        }

        public class Unwatch
        {
            public CounterType Counter { get; private set; }

            public Unwatch(CounterType counter)
            {
                Counter = counter;
            }
        }

        #endregion

        /// <summary>
        /// Methods for generating new instances of all <see cref="PerformanceCounter"/>s we want to monitor
        /// </summary>
        private static readonly Dictionary<CounterType, Func<PerformanceCounter>> CounterGenerators = new Dictionary
            <CounterType, Func<PerformanceCounter>>()//读取数据字典
        {
            {CounterType.Cpu, () => new PerformanceCounter("Processor", "% Processor Time", "_Total", true)},   //cpu使用率
            {CounterType.Memory, () => new PerformanceCounter("Memory", "% Committed Bytes In Use", true)},     //内存使用率
            {CounterType.Disk, () => new PerformanceCounter("LogicalDisk", "% Disk Time", "_Total", true)},     //当前物理磁盘利用率
            {CounterType.Network, () => new PerformanceCounter("Network Interface", "Bytes Received/sec", "Qualcomm Atheros QCA61x4A Wireless Network Adapter", true)},//接收字节数
        };

        /// <summary>
        /// Methods for creating new <see cref="Series"/> with distinct colors and names corresponding to each
        /// <see cref="PerformanceCounter"/>
        /// </summary>
        private static readonly Dictionary<CounterType, Func<Series>> CounterSeries = new Dictionary
            <CounterType, Func<Series>>()
        {
            {
                //设置图形类型，折线类型
                CounterType.Cpu, () => new Series(CounterType.Cpu.ToString())
                {ChartType = SeriesChartType.Spline, Color = Color.LawnGreen}
            },
            {
                CounterType.Memory, () => new Series(CounterType.Memory.ToString())
                {ChartType = SeriesChartType.Spline, Color = Color.BlueViolet}
            },
            {
                CounterType.Disk, () => new Series(CounterType.Disk.ToString())
                {ChartType = SeriesChartType.Spline, Color = Color.OrangeRed}
            },
            {
                CounterType.Network, () => new Series(CounterType.Network.ToString())
                {ChartType = SeriesChartType.Spline, Color = Color.DarkCyan}
           
            },
        };

        private Dictionary<CounterType, IActorRef> _counterActors;
        private IActorRef _chartingActor;

        public PerformanceCounterCoordinatorActor(IActorRef chartingActor, int time_span)
            : this(chartingActor, new Dictionary<CounterType, IActorRef>(), time_span)
        {
        }

        private PerformanceCounterCoordinatorActor(IActorRef chartingActor,
            Dictionary<CounterType, IActorRef> counterActors, int time_span)
        {
            _chartingActor = chartingActor;
            _counterActors = counterActors;

            Receive<Watch>(watch =>
            {
                if (!_counterActors.ContainsKey(watch.Counter))
                {
                    // create a child actor to monitor this counter if one doesn't exist already
                    var counterActor =
                        Context.ActorOf(
                            Props.Create(
                                () =>
                                    new PerformanceCounterActor(watch.Counter.ToString(),
                                        CounterGenerators[watch.Counter], time_span)));
                    _counterActors[watch.Counter] = counterActor;
                }

                // register this series with the ChartingActor
                _chartingActor.Tell(new ChartingActor.AddSeries(CounterSeries[watch.Counter]()));
                // tell the counter actor to being publishing its stats to the _chartingActor
                _counterActors[watch.Counter].Tell(new SubscribeCounter(watch.Counter, _chartingActor));
            });

            Receive<Unwatch>(unwatch =>
            {
                if (!_counterActors.ContainsKey(unwatch.Counter))
                {
                    return;
                }
                // unsubscribe the ChartingActor from receiving updates
                _counterActors[unwatch.Counter].Tell(new UnsubscribeCounter(unwatch.Counter, _chartingActor));
                // remove this series
                _chartingActor.Tell(new ChartingActor.RemoveSeries(unwatch.Counter.ToString()));
            });
        }
    }
}