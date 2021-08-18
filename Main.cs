using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Akka.Actor;
using Akka.Util.Internal;
using ChartApp.Actors;
using Microsoft.VisualBasic;



namespace ChartApp
{
    public partial class Main : Form
    {
        private IActorRef _chartActor;
        private readonly AtomicCounter _seriesCounter = new AtomicCounter(1);
        private IActorRef _coordinatorActor;
        private Dictionary<CounterType, IActorRef> _toggleActors = new Dictionary<CounterType, IActorRef>();
        private int time_span = 250;
        private bool isAdmin = false;
        private string actor_path = "";
        private int number;
        private Login parent;
        public Main(bool isAdmin, int number, int time_span=250)
        {
            this.isAdmin = isAdmin;
            this.number = number;
            this.time_span = time_span;
            //this.parent = parent;
            InitializeComponent();
        }

        #region Initialization

        IActorRef GetOrCreate(string childName)
        {
            IActorRef child;
            if (this.actor_path == "")
            {
                Console.WriteLine("actor created");
                
                child = Program.ChartActors.ActorOf(Props.Create(() => new ChartingActor(sysChart, btnPauseResume)), childName);
                this.actor_path = child.Path.ToString();

            }
            else {
                Console.WriteLine("start wait");
                child = Program.ChartActors.ActorSelection(this.actor_path).ResolveOne(TimeSpan.FromSeconds(2)).Result;
                Console.WriteLine("after wait");
                
            }
            return child;
        }
        private void Main_Load(object sender, EventArgs e)
        {
            //_chartActor = GetOrCreate("charting");
            _chartActor = Program.ChartActors.ActorOf(Props.Create(() => new ChartingActor(sysChart, btnPauseResume, this)), "charting" + number);
            _chartActor.Tell(new ChartingActor.InitializeChart(null));

            _coordinatorActor =
                Program.ChartActors.ActorOf(Props.Create(() => new PerformanceCounterCoordinatorActor(_chartActor, time_span)),//1111
                    "counters" + number);

            _toggleActors[CounterType.Cpu] =
                Program.ChartActors.ActorOf(
                    Props.Create(() => new ButtonToggleActor(_coordinatorActor, btnCpu, CounterType.Cpu, false))
                        .WithDispatcher("akka.actor.synchronized-dispatcher"));

            _toggleActors[CounterType.Memory] =
                Program.ChartActors.ActorOf(
                    Props.Create(() => new ButtonToggleActor(_coordinatorActor, btnMemory, CounterType.Memory, false))
                        .WithDispatcher("akka.actor.synchronized-dispatcher"));

            _toggleActors[CounterType.Disk] =
                Program.ChartActors.ActorOf(
                    Props.Create(() => new ButtonToggleActor(_coordinatorActor, btnDisk, CounterType.Disk, false))
                        .WithDispatcher("akka.actor.synchronized-dispatcher"));

            _toggleActors[CounterType.Network ] =
                Program.ChartActors.ActorOf(
                    Props.Create(() => new ButtonToggleActor(_coordinatorActor, btnNet, CounterType.Network, false))
                        .WithDispatcher("akka.actor.synchronized-dispatcher"));

            _toggleActors[CounterType.Cpu].Tell(new ButtonToggleActor.Toggle());
        }

        #endregion

        private CounterType[] getToggledButtons(CounterType ct_to_reverse, bool reverse=false) {
            int[] types = {0,0,0,0};
            CounterType[] cts = new CounterType[4];
            int index = 0;
            if (btnCpu.Text.Contains("ON")) {
                types[(int)CounterType.Cpu] = 1;  
            }
            if (btnMemory.Text.Contains("ON"))
            {
                types[(int)CounterType.Memory] = 1;
            }
            if (btnDisk.Text.Contains("ON"))
            {
                types[(int)CounterType.Disk] = 1;
            }
            if (btnNet.Text.Contains("ON"))
            {
                types[(int)CounterType.Network] = 1;
            }
            if(reverse)
                types[(int)ct_to_reverse] = 1 - types[(int)ct_to_reverse];
            foreach(int i in types)
                if (i == 1)
                    index++;
            if (index == 0)
                return null;
            CounterType[] res = new CounterType[index];
            int j = 0;
            for (int i = 0; i < 4; i++)
                if (types[i] == 1)
                    res[j++] = (CounterType)i;
            return res;
        }

        private void TB1update(CounterType ct_to_reverse) {
            CounterType[] cts = getToggledButtons(ct_to_reverse, true);
            string str = "";
            if(cts != null)
                foreach (var ct in cts) {
                    if (ct == CounterType.Cpu)
                        str += ct.ToString() + ": " +getCPU() + System.Environment.NewLine;
                    if (ct == CounterType.Memory)
                        str += ct.ToString() + ": " + getMemory() + System.Environment.NewLine;
                    if (ct == CounterType.Disk)
                        str += ct.ToString() + ": " + getHardDisk() + System.Environment.NewLine;
                    if (ct == CounterType.Network)
                        str += ct.ToString() + ": " + getNetwork() + System.Environment.NewLine;
                }
            textBox1.Text = str;
        }

        private double[] cachedData = new double[4];
        public void TB2update(String Series, double value) {

            CounterType[] cts = getToggledButtons(0, false); 
            string str = "";
            if(cts != null)
                foreach (var ct in cts)
                {
                    if (ct == CounterType.Cpu)
                    {
                        if (ct.ToString() == Series)
                            cachedData[(int)ct] = value;
                        str += "CPU" + ": " + string.Format("{0:0.00}", cachedData[(int)ct]) +"%"+ System.Environment.NewLine;
                    }
                    if (ct == CounterType.Memory)
                    {
                        if (ct.ToString() == Series)
                            cachedData[(int)ct] = value;
                        str += "内存" + ": " + string.Format("{0:0.00}", cachedData[(int)ct]) + "%" + System.Environment.NewLine;
                    }
                    if (ct == CounterType.Disk)
                    {
                        if (ct.ToString() == Series)
                            cachedData[(int)ct] = value;
                        str += "硬盘" + ": " + string.Format("{0:0.00}", cachedData[(int)ct]) + "%" + System.Environment.NewLine;
                    }
                    if (ct == CounterType.Network)
                    {
                        if (ct.ToString() == Series)
                            cachedData[(int)ct] = value;
                        str += "网络" + ": " + string.Format("{0:0.00}", cachedData[(int)(ct)]/1024) + "KB/s" + System.Environment.NewLine;
                    }
                }
            textBox2.Text = str;
        }

        private void btnCpu_Click(object sender, EventArgs e)
        {
            _toggleActors[CounterType.Cpu].Tell(new ButtonToggleActor.Toggle());//消息机制，发送消息
            TB1update(CounterType.Cpu);
            //textBox1.Text = "CPU：" + System.Environment.NewLine + getCPU();
        }

        private void btnNet_Click(object sender, EventArgs e)
        {
            _toggleActors[CounterType.Network].Tell(new ButtonToggleActor.Toggle());

            TB1update(CounterType.Network);
            //textBox1.Text = "网卡：" + System.Environment.NewLine + getNetworkAll();
        }

        private void btnMemory_Click(object sender, EventArgs e)
        {
            _toggleActors[CounterType.Memory].Tell(new ButtonToggleActor.Toggle());
            TB1update(CounterType.Memory);
            //textBox1.Text = "内存：" + System.Environment.NewLine + getMemory();
        }

        private void btnDisk_Click(object sender, EventArgs e)
        {
            _toggleActors[CounterType.Disk].Tell(new ButtonToggleActor.Toggle());
            TB1update(CounterType.Disk);
            //textBox1.Text = "硬盘：" + System.Environment.NewLine + getHardDisk();
        }

        private void btnPauseResume_Click(object sender, EventArgs e)
        {
            _chartActor.Tell(new ChartingActor.TogglePause());
        }

        private void toolStripMenuItem7_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {

            textBox1.Text = "计算机系统性能信息如下：" + System.Environment.NewLine
                + "系统：\t" + getSystem() + System.Environment.NewLine
                + "CPU：\t" + getCPU() + System.Environment.NewLine
                + "硬盘：\t" + getDriveAll()+ System.Environment.NewLine
                + "内存：\t" + getMemory() + System.Environment.NewLine
                + "网卡：\t" + getNetworkAll() + System.Environment.NewLine
                ;

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

     

        private void 保存ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string str = "C:\\Users\\28237\\Desktop";
            var dialog = new FolderBrowserDialog();
            var currentPath = "C:\\Users\\28237\\Desktop";
            dialog.SelectedPath = currentPath;
            var result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                str = dialog.SelectedPath;
                //string str = Interaction.InputBox("设置默认保存路径",  "保存数据", "C:\\Users\\28237\\Desktop", 100, 100);
                DirectoryInfo directoryInfo = new DirectoryInfo(str);
                _chartActor.Tell(new ChartingActor.ClickSave(directoryInfo));
                MessageBox.Show("保存到" + str + "\\test.txt成功！");
            }
            
            
        }

        private void 修改ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int timespan = 250;
            //MessageBox.Show("请设置新的实时监控时刷新的时间间隔！");
            string str = Interaction.InputBox("请设置新的实时监控时刷新的时间间隔", "修改时间间隔", "", 100, 100);
            try
            {
                timespan = Convert.ToInt32(str);    //字符转换为整型
                Console.WriteLine("time span is " + timespan);//写入后台
                this.time_span = timespan;//设置新的的刷新时间间隔
                var ss = new Main(this.isAdmin, this.number + 1, time_span);//弹出修改后的新的展示窗口
                ss.Show();
                this.Close();
                this.Hide();
                //parent.Hide();
                //((Login)Owner).Close();
            }
            catch (Exception ex) {
                time_span = 250;
            }
            
            

            // string Interaction.InputBox（string Prompt,string title,string Defaultresponce,int Xpos,int Ypose）

        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {
            
        }

        private void menuStrip2_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void sysChart_Click(object sender, EventArgs e)
        {

        }

        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
