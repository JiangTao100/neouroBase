using MathWorks.MATLAB.NET.Arrays;
using MathWorks.MATLAB.NET.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Media;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using TunerlTools;
using FBCCATool;
using System.Runtime.InteropServices;
using xTRCA;
using xTRCA_Eye_merge;
using System.Net.Sockets;
using System.Text;

namespace NeuroBase
{
    public partial class MainForm : Form
    {
        ////varibles below
        //server for data comunication; udp for search and initialize amplifier; data for temporarily save, filt and record;
        Server server;
        TunerlAmplifierProtocal TunerlProtocal;
        UdpBroadcaster IpBroadcaster;
        TCPClient Curry =new TCPClient();


        IPAddress LocalAddress;
        Data data;
        PlotMaster Ploter;
        int Phase;//function FromPhase related varible
        public int PhaseGet
        {
            get
            {
                return Phase;
            }
        }
        //String[] Channels = new string[16] { "F3","Fz","F4","FCz","C3","Cz","C4","P3","Pz","P4","PO7","POz","PO8","O1","Oz","O2"};
        //PO5   PO3   POZ   PO4   PO6   O1   OZ   O2
        string[] Channels = new string[] { "PO5", "PO3", "POz", "PO4", "PO6", "O1", "Oz", "O2","Closed", "Closed", "Closed", "Closed", "Closed", "Closed", "Closed", "Closed" };
        public static int ChannelNumber = 16;

        //string[] Channels = new string[16] { "POz", "Oz", "O1", "O2", "PO3", "PO4", "PO5", "PO6", "Closed", "Closed", "Closed", "Closed", "Closed", "Closed", "Closed", "Closed" };
        //public static int ChannelNumber = 16;
        int[] PosList;
        //filter setting varibles below  1000hz
        double[] FilterBandFa = new double[] { 1, -4.88485919493613, 9.97463009788807, -10.9349617103779, 6.80710555281658, -2.28354164058238, 0.321626922721497 };
        double[] FilterBandFb = new double[] { 0.0134277334215775, 0, -0.0402832002647325, 0, 0.0402832002647325, 0, -0.0134277334215775 };
        double[] Filter50NotchFa = new double[] { 1, -5.68254876635053, 13.7387485793903, -18.0664564757185, 13.6241305276252, -5.58812899963934, 0.975180295515611 };
        double[] Filter50NotchFb = new double[] { 0.987512174868383, -5.63519056681354, 13.6815175263792, -18.0667531080454, 13.6815175263792, -5.63519056681354, 0.987512174868383 };
        double[] Filter100NotchA = new double[] { 1, -4.83386470159597, 10.7636849982447, -13.7703229188621, 10.6738871408885, -4.75354645071867, 0.975180295515609 };
        double[] Filter100NotchB = new double[] { 0.987512174869645, -4.79357941088386, 10.7188640424554, -13.7705752494103, 10.7188640424554, -4.79357941088386, 0.987512174869645 };
        double[] Filter150NotchA = new double[] { 1, -3.51200828033341, 7.08630417456484, -8.56970106422093, 7.02718576471289, -3.45365365529606, 0.975180295515611 };
        double[] Filter150NotchB = new double[] { 0.987512174869591, -3.48273930337782, 7.05682294252708, -8.56988439309477, 7.05682294252708, -3.48273930337782, 0.987512174869591 };
        Color[] FilterColor = { Color.Gray, Color.Green, Color.Blue };

        //plot related varibles below 
        int[] ChannelOn;
        public int[] ChannelOnGet
        {
            get
            {
                return ChannelOn;
            }
        }

        public MWArray FreqNum1 { get => FreqNum; set => FreqNum = value; }

        //online machine related

        int DataLength = 2000;
        int StartTime = 1000;
        double[,] DataToClass;
        int[] SSVEPChannelList = {0, 1, 2, 3, 4, 5, 6, 7};
        List<SoundPlayer> Player = new List<SoundPlayer>(8);
        MWArray FreqNum = (MWNumericArray)new int[] {0, 1, 2, 3, 4, 5, 6, 7,};
        MWArray BandUsed = (MWNumericArray)new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        MWArray srate = 1000;
        int phase=0;
        float sRate = 1000;        
        FBCCA fbcca = new FBCCA();
        //Class1 xtrca = new Class1();  // 返回xTRCA分类结果
        MergexTRCA xtrca = new MergexTRCA();  // 返回xTRCA相关系数
        int FPS=60;


        /******************机械臂相关************************/

        ArmPiControl ArmMaster = new ArmPiControl();
        DobotPiControl DobotMaster = new DobotPiControl();
        bool ArmOn=false;// for button status
        bool AirOn = false;//
        public delegate void OutPuts(int result);
        public OutPuts OutPut;
        //airs
        AirCC AirAgent;
        // Eye
        float EyeData;

        //// functions below

        void ContinuousSSVEP()
        {
            //delegate, run each time pushdata called
            //Console.WriteLine("准备识别");
            if (data.CurrentEventGet>=100 && data.CurrentEventGet <= 160)
            {
                float[][] DataTem = data.GetDataRaw(StartTime + DataLength);
                DataToClass = new double[StartTime + DataLength, SSVEPChannelList.Length];
                for (int i = 0; i < StartTime + DataLength; i++)
                {
                    for (int iChannel = 0; iChannel < SSVEPChannelList.Length; iChannel++)
                        DataToClass[i, iChannel] = DataTem[i][SSVEPChannelList[iChannel]];
                }
                double RelativePhase = ((data.CurrentEventGet-100) * sRate / FPS - DataLength) % sRate;
                phase = (int)(RelativePhase < 0 ? RelativePhase + 1000 : RelativePhase);


                //Thread Machine = new Thread(CCAControl) { IsBackground = true };
                Thread Machine = new Thread(xTRCAControl) { IsBackground = true };
                Machine.Start();
            }
        }
        void CCAControl()
        {            
            MWNumericArray Onlinedata = new MWNumericArray();
            Onlinedata = (MWNumericArray)DataToClass;
            MWArray length = DataLength;
            MWArray StartTimeM = StartTime;
            MWArray phaseM = 0;
            MWArray result = fbcca.FBCCARawMachineC(Onlinedata, StartTimeM, length, phaseM, BandUsed);

            double[,] Xresult = (double[,])result.ToArray();
            int XIresult = (int)Xresult[0, 0];            
            Player[XIresult - 1].Play();
            OutPut?.Invoke(XIresult);
        }

        // ----- xTRCA ------ // 
        // LLLpx
        // modify by 2019.12.23
        void xTRCAControl()
        {
            MWNumericArray Onlinedata = new MWNumericArray();
            Onlinedata = (MWNumericArray)DataToClass;
            MWArray length = DataLength;
            MWArray StartTimeM = StartTime;
            MWArray phaseM = 0;
            MWArray result = xtrca.xTRCATempleCorr(Onlinedata, StartTime, length, phaseM);

            double[,] Xresult = (double[,])result.ToArray();
            Console.WriteLine(Xresult);  // 查看xTRCA输出的相关系数
            Console.WriteLine("接到眼动数据：" + EyeData);  // 查看接收的眼动数据

            int XIresult = (int)Xresult[0, 0];
            if (XIresult != (int)EyeData)
            {
                XIresult = (int)EyeData;
                Console.WriteLine("使用眼动结果");
            }
            Player[XIresult - 1].Play();
            OutPut?.Invoke(XIresult);
        }

        void ArmPiFunc(int result)
        {                      
            if (ArmMaster.Connected)
            {
                ArmMaster.Move((result + 1) / 2, result % 2 == 0 ? true : false);
                //ArmMaster.Move(3,true);
            }
        }

        void DobotControl(int result)
        {
            DobotMaster.Move(result);
        }

        void Birds(int result)
        {
            int distance = 20;
            if (AirOn)
            {
                switch (result)
                {
                    case 1:
                        AirAgent.Forward(distance);
                        break;
                    case 2:
                        AirAgent.Left(distance);
                        break;
                    case 3:
                        AirAgent.Back(distance);
                        break;
                    case 4:
                        AirAgent.Right(distance);
                        break;
                }
            }
        }
               

        public MainForm()
        {
            ChannelOn = new int[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            data = new Data(ChannelNumber);
            Ploter = new PlotMaster(this, ChannelNumber, data);
            Ploter.Space =new int[]{ 40,60,80,30};
            Ploter.Channels = Channels;
            InitializeComponent();
            
            //Filter Setting          
            data.AddFilter(FilterBandFb, FilterBandFa, ChannelNumber);
            data.AddFilter(FilterBandFb, FilterBandFa, ChannelNumber);
            data.AddFilter(Filter50NotchFb, Filter50NotchFa, ChannelNumber);
            data.AddFilter(Filter50NotchFb, Filter50NotchFa, ChannelNumber);
            data.AddFilter(Filter100NotchB, Filter100NotchA, ChannelNumber);
            data.AddFilter(Filter100NotchB, Filter100NotchA, ChannelNumber);
            data.AddFilter(Filter150NotchB, Filter150NotchA, ChannelNumber);
            data.AddFilter(Filter150NotchB, Filter150NotchA, ChannelNumber);
            data.FilterSwitcher(new bool[] { true, false, true, false, false, false, false, false });

            //Filter button initialize
            bool[] FilterStatus = data.FilterSwitcher();
            int[] FilterStatusInt = new int[FilterStatus.Length];
            for(int iFilter=0;iFilter<FilterStatus.Length;iFilter++)
            {
                FilterStatusInt[iFilter] = FilterStatus[iFilter] ? 1 : 0;
            }
            toolStripButton14.BackColor = FilterColor[FilterStatusInt[0] + FilterStatusInt[1]];
            toolStripButton15.BackColor = FilterColor[FilterStatusInt[2] + FilterStatusInt[3]];
            toolStripButton15.BackColor = FilterColor[FilterStatusInt[2] + FilterStatusInt[3]];
            toolStripButton16.BackColor = FilterColor[FilterStatusInt[4] + FilterStatusInt[5]];
            toolStripButton17.BackColor = FilterColor[FilterStatusInt[6] + FilterStatusInt[7]];
            //Form Phase setting
            FormPhase(1);
            
            PosList =new int[]{ 6,4,0,5,7,-1,2,1,3,-1};
            //plot settings
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true); // 禁止擦除背景.
            SetStyle(ControlStyles.DoubleBuffer, true); // 双缓冲


            // online machine            
            //data.Machine += TRCA.TRCAProcces;
            TunerlProtocal = new TunerlAmplifierProtocal(ChannelNumber,data);
            //for (int iPlayer = 0; iPlayer < 8; iPlayer++)
            //{
            //    SoundPlayer NewPlayer = new SoundPlayer();
            //    NewPlayer.SoundLocation = System.Environment.CurrentDirectory + "\\Sounds\\" + (iPlayer + 1).ToString() + ".wav";
            //    Player.Add(NewPlayer);
            //    Player[iPlayer].Load();
            //}

            //   Load
            //cca.CCALoadPara(srate);
            fbcca.FBCCARawLoadPara();
            fbcca.FBCCARawLoadModule();

            // xTRCA      此处应该是自己封装一个TRCA 
            // LLLpx
            // 2019.12.23
            xtrca.xTRCARawLoadFilterPara();
            xtrca.xTRCARawLoadModule();
            Console.WriteLine("loadTemple");
            

            //进行TRCA识别
            data.OnlineMachine += ContinuousSSVEP;//返回到ContinuousSSVEP，


            OutPut += ArmPiFunc;
            OutPut += Birds;
            OutPut += DobotControl;   
        }
        void FormPhase(int p)   
        {
            Phase = p;
            switch (p)
            {
                case 1:
                    StartServerButton.Enabled = true;
                    toolStripButton2.Enabled = false;
                    toolStripButton3.Enabled = false;
                    toolStripButton11.Enabled = false;
                    toolStripButton4.Enabled = false;
                    toolStripButton13.Enabled = false;
                    toolStripStatusLabel2.Text = "未连接";
                    //DobotStatus.Text = "未连接";
                    break;
                case 2:
                    StartServerButton.Enabled = false;
                    toolStripButton2.Enabled = false;
                    toolStripButton3.Enabled = false;
                    toolStripButton11.Enabled = false;
                    toolStripButton4.Enabled = false;
                    toolStripButton13.Enabled = false;
                    toolStripStatusLabel2.Text = "正在连接";
                    break;
                case 3:
                    StartServerButton.Enabled = false;
                    toolStripButton2.Enabled = true;
                    toolStripButton3.Enabled = false;
                    toolStripButton11.Enabled = false;
                    toolStripButton4.Enabled = true;
                    toolStripButton13.Enabled = true;
                    toolStripStatusLabel2.Text = "已连接";
                    break;
                case 4:
                    Ploter.Phase = 1;
                    StartServerButton.Enabled = false;
                    toolStripButton2.Enabled = false;
                    toolStripButton3.Enabled = true;
                    toolStripButton11.Enabled = true;
                    toolStripButton4.Enabled = true;
                    toolStripButton13.Enabled = false;
                    toolStripStatusLabel2.Text = "EEG";
                    break;
                case 5:
                    Ploter.Phase = 2;
                    StartServerButton.Enabled = false;
                    toolStripButton2.Enabled = false;
                    toolStripButton3.Enabled = false;
                    toolStripButton11.Enabled = false;
                    toolStripButton4.Enabled = true;
                    toolStripButton13.Enabled = false;
                    toolStripStatusLabel2.Text = "IMP";
                    break;
                case 6:
                    StartServerButton.Enabled = false;
                    toolStripButton2.Enabled = true;
                    toolStripButton3.Enabled = false;
                    toolStripButton11.Enabled = false;
                    toolStripButton4.Enabled = true;
                    toolStripButton13.Enabled = false;
                    toolStripStatusLabel2.Text = "暂停";
                    break;
                case 7:
                    Ploter.Phase = 1;
                    StartServerButton.Enabled = false;
                    toolStripButton2.Enabled = false;
                    toolStripButton3.Enabled = false;
                    toolStripButton11.Enabled = false;
                    toolStripButton4.Enabled = true;
                    toolStripButton13.Enabled = false;
                    toolStripStatusLabel2.Text = "Recording";
                    break;

            }
            //phase 1 : unconnected
            //phase 2 : connecting
            //phase 3 : connected
            //phase 4 : eeg
            //phase 5 : imp
            //phase 6 : eeg pause
            //phase 7 : eeg recording
        }

        void DobotStatusDisp(int s)
        {
            // Add DobotStatus Display
            // By Pengxiao
            switch(s)
            {
                case 1:
                    DobotStatus.Text = "已连接";
                    break;
                case 2:
                    DobotStatus.Text = "未连接";
                    break;

            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (IpBroadcaster != null)
            {
                //IpBroadcaster.Stop();
                //UdpBroadcaster.StopFlag = true;
                IpBroadcaster = null;
            }
            if(data.Feedbacker!=null)
                data.Feedbacker.CloseBroadcaster();
            if ((server!=null) )
            {
                if(server.connected == true)
                {
                    if (Phase == 3)
                    {
                        byte[] SendByteStart = System.Text.Encoding.Default.GetBytes("starteeg");
                        server.SendMsg(SendByteStart);
                        Thread.CurrentThread.Join(200);
                    }
                    byte[] SendByte = System.Text.Encoding.Default.GetBytes("stop");
                    server.SendMsg(SendByte);
                }
                
            }
        }
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {            
            //IpBroadcaster = null;
            if (server.connected == true)
            {                
                byte[] SendByte = System.Text.Encoding.Default.GetBytes("stop");
                server.SendMsg(SendByte);
            }
        }              
        protected override void OnPaint(PaintEventArgs e)
        {
            
            Ploter.PlotPool(e);

            switch (Phase)
            {
                case 4:
                    Ploter.PlotEEG(e);                   
                    break;
                case 5:
                    Ploter.PlotResistanceIri(e,2,5,PosList);
                    break;
                case 7:
                    Ploter.PlotEEG(e);
                    break;
            }
           

        }
        
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Ploter.ReplotAll();
        }       


        private void toolStripButton1_Click(object sender, EventArgs e)
        {

        }
        
        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            if (Phase == 3)
            {
                if (server.connected == true)
                {
                    byte[] SendByte = System.Text.Encoding.Default.GetBytes("starteeg");
                    server.SendMsg(SendByte);
                    Ploter.InitializeDataAcquire();
                    FormPhase(4);
                    Ploter.TimerSwitcher(true);

                    
                }
            }
            if(Phase==6)
            {
                FormPhase(4);
                Ploter.TimerSwitcher(true);                
            }
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            byte[] SendByte;
            if (Phase==3)
            {
                SendByte = System.Text.Encoding.Default.GetBytes("starteeg");
                server.SendMsg(SendByte);
                Thread.CurrentThread.Join(200);
            }
            SendByte = System.Text.Encoding.Default.GetBytes("stop");
            server.SendMsg(SendByte);
            data.CloseSaveFile();
            FormPhase(1);            
            server.StopServer();
            data.Feedbacker.CloseBroadcaster();            
            Ploter.Reset();           
            Ploter.TimerSwitcher(false);
            Ploter.ReplotAll();            
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {            
            FormPhase(6);            
            Ploter.TimerSwitcher(false);
        }

        private void toolStripButton5_Click(object sender, EventArgs e)
        {
           Ploter.PixPerVtg(2);
            Ploter.ReplotAll();
        }

        private void toolStripButton6_Click(object sender, EventArgs e)
        {
            Ploter.PixPerVtg((float)0.5);
            Ploter.ReplotAll();
        }

        private void toolStripButton7_Click(object sender, EventArgs e)
        {
            Ploter.PlotLength(false);
        }
       
        private void toolStripButton8_Click_1(object sender, EventArgs e)
        {
            Ploter.PlotLength(true);
        }

        private void toolStripButton9_Click(object sender, EventArgs e)
        {
            VisualSetting vsetting=new VisualSetting(ChannelOn);
            vsetting.ShowDialog();
            ChannelOn = vsetting._ChannelOn;
            Ploter.ChannelOn = ChannelOn;            
            Ploter.ReplotAll();
        }

        private void toolStripButton10_Click(object sender, EventArgs e)
        {
            Ploter.RefreshBaseLine();
            Ploter.ReplotAll();
        }

        public void toolStripButton11_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*";
            saveFileDialog1.FilterIndex = 1;
            saveFileDialog1.RestoreDirectory = true;
            
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                data.WriteHeader(saveFileDialog1.FileName);                
                data.SaveOn = true;
                FormPhase(7);
            }
            
        }

        // LLLpx
        Socket EyeServer;

        private void StartServerButton_Click(object sender, EventArgs e)
        {
            FormPhase(2);

            Console.WriteLine("开始绑定端口");
            //Curry.Bind("192.168.232.1", 4455);
            IPEndPoint RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.168.232.1"), 4455);
            Curry.Connect(RemoteEndPoint);
           String aaa= Curry.Receive();
            //IpBroadcaster = new UdpBroadcaster(IPAddress.Broadcast, 4455);  // LLLpx 7001 首先用广播模式绑定向远程端口7001来接收广播
            //Console.WriteLine("1");
            //LocalAddress =IpBroadcaster.UDPSearch(4455);  // LLLpx 7010  用本地端口7010发送广播来搜索并返回本地可用IP
            Console.WriteLine("得到可用本地IP：" + LocalAddress);
            if (LocalAddress != null)
            {
                Console.WriteLine(aaa);

                Console.WriteLine("绑定本地端口4455");
                server = new Server(4455);  // LLLpx 7010  本地监听端口
                Console.WriteLine("开启监听");
                server.protocal += TunerlProtocal.WatchMsg;
                Console.WriteLine("设置监听");
                server.SetupLocalServer(LocalAddress);  // LLLpx 设置监听
                Console.WriteLine("通过智能监听判断是否连接：" + server.connected);
                //Thread.CurrentThread.Join(500);
                while (server.connected != true)
                {
                    //Console.WriteLine("循环");
                }
                data.Feedbacker = new UdpBroadcaster(IPAddress.Broadcast,7050);  // LLLpx 远程端口7050  做广播好像是发给无人机的吧
                Console.WriteLine("第二次广播：" + data.Feedbacker.ToString());
                data.Feedbacker.BroadcasterSetup(LocalAddress, 7011);         // LLLpx 本地端口7011
                FormPhase(3);
                toolStripStatusLabel4.Text = LocalAddress.ToString();





                // 加入接收眼动数据 创建服务器
                EyeServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                // 创建IP端口数据
                IPEndPoint eyeEndPoint = new IPEndPoint(LocalAddress, 8000);
                // 绑定端口和IP
                EyeServer.Bind(eyeEndPoint);
                // 开启接收消息线程
                Thread eyeReceiveThread = new Thread(ReceiveEyeMsg);
                eyeReceiveThread.IsBackground = true;
                eyeReceiveThread.Start();

            }
            else
            {
                FormPhase(1);
                Console.Write("6");
            }           
        }

        void ReceiveEyeMsg()
        {
            while(true)
            {
                EndPoint point = new IPEndPoint(IPAddress.Any, 8010); // 客户端的IP和端口号
                byte[] buffer = new byte[1024 * 1024];
                int length = EyeServer.ReceiveFrom(buffer, ref point);
                // string message = Encoding.UTF8.GetString(buffer, 0, length);
                // Console.WriteLine("接到眼动数据：" + (float)buffer[0]);
                // Console.WriteLine("接到眼动数据：" + message);
                EyeData = (float)buffer[0];
            }
        }

        private void toolStripButton13_Click(object sender, EventArgs e)
        {
            if (server.connected == true)
            {
                byte[] SendByte = System.Text.Encoding.Default.GetBytes("imp");
                server.SendMsg(SendByte);
                FormPhase(5);
                Ploter.InitializeDataAcquire();
                Ploter.TimerSwitcher(true);
            }
        }

        private void toolStripButton14_Click(object sender, EventArgs e)
        {
            
            bool[] FilterStatus = data.FilterSwitcher();           
            if (!FilterStatus[0])
            {
                data.FilterSwitcher(0, true);
                toolStripButton14.BackColor = FilterColor[1];
            }
            else if (!FilterStatus[1])
            { data.FilterSwitcher(1, true);
                toolStripButton14.BackColor = FilterColor[2];
            }
            else
            {
                data.FilterSwitcher(0, false);
                data.FilterSwitcher(1, false);
                toolStripButton14.BackColor = FilterColor[0];
            }
        }

        private void toolStripButton15_Click(object sender, EventArgs e)
        {
            bool[] FilterStatus = data.FilterSwitcher();
            if (!FilterStatus[2])
            {
                data.FilterSwitcher(2, true);
                toolStripButton15.BackColor = FilterColor[1];
            }
            else if (!FilterStatus[3])
            {
                data.FilterSwitcher(3, true);
                toolStripButton15.BackColor = FilterColor[2];
            }
            else
            {
                data.FilterSwitcher(2, false);
                data.FilterSwitcher(3, false);
                toolStripButton15.BackColor = FilterColor[0];
            }
        }

        private void toolStripButton16_Click(object sender, EventArgs e)
        {
            bool[] FilterStatus = data.FilterSwitcher();
            if (!FilterStatus[4])
            {
                data.FilterSwitcher(4, true);
                toolStripButton16.BackColor = FilterColor[1];
            }
            else if (!FilterStatus[5])
            {
                data.FilterSwitcher(5, true);
                toolStripButton16.BackColor = FilterColor[2];
            }
            else
            {
                data.FilterSwitcher(4, false);
                data.FilterSwitcher(5, false);
                toolStripButton16.BackColor = FilterColor[0];
            }
        }

        private void toolStripButton17_Click(object sender, EventArgs e)
        {
            bool[] FilterStatus = data.FilterSwitcher();
            if (!FilterStatus[6])
            {
                data.FilterSwitcher(6, true);
                toolStripButton17.BackColor = FilterColor[1];
            }
            else if (!FilterStatus[7])
            {
                data.FilterSwitcher(7, true);
                toolStripButton17.BackColor = FilterColor[2];
            }
            else
            {
                data.FilterSwitcher(6, false);
                data.FilterSwitcher(7, false);
                toolStripButton17.BackColor = FilterColor[0];
            }
        }

        private void ArmSwitcherButton_Click(object sender, EventArgs e)
        {
            if (ArmOn)
            {
                ArmMaster.Close();
                ArmSwitcherButton.BackColor = Color.White;
                ArmOn = false;
            }
            else
            {
                
                ArmOn = ArmMaster.Connect();
                if (ArmOn)
                {
                    ArmMaster.Reset();
                    ArmSwitcherButton.BackColor = Color.LightGreen;
                }
                
            }
            
        }

        private void AirButton_Click(object sender, EventArgs e)
        {
            if (AirOn)
            {
                AirAgent.Land();
                //AirAgent.Close();
                AirButton.BackColor = Color.White;
                AirOn = false;
            }
            else
            {

                AirAgent = new AirCC();                
                AirButton.BackColor = Color.LightGreen;
                AirAgent.TakeOff();
                AirOn = true;

            }
        }


        // -------- DobotInit ----------//
        // By Pengxiao
        private void DobotOn_Click(object sender, EventArgs e)
        {
            DobotMaster.StartDobot();
            if (DobotMaster.isConnectted)
                DobotStatusDisp(1);
        }

        private void DobotOff_Click(object sender, EventArgs e)
        {
            DobotMaster.Disconnect();
            if (!DobotMaster.isConnectted)
                DobotStatusDisp(2);
        }

        private void MOVE1_Click(object sender, EventArgs e)
        {
            DobotMaster.Move(1);
        }

        private void Move2_Click(object sender, EventArgs e)
        {
            DobotMaster.Move(7);
        }

        private void Move3_Click(object sender, EventArgs e)
        {
            DobotMaster.Move(8);
        }

    }

    
 
}

