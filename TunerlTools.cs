using MathWorks.MATLAB.NET.Arrays;
using NeuroBase;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
//using TunerlTools.CPlusDll;  // before name
using TunerlTools.Dobot;
using System.Runtime.InteropServices;


namespace TunerlTools
{
    public class Server
    {
        //Class Server provides basic tcp server functions, here is how it works:
        //Server server=new server( LocalPort);// LocalPort(int) is the local port of the server
        //server.SetupLocalServer(address);  //address(IPAddress) is the local address of the server,is omitted, it will be start on an random local address;
        //server.SendMsg( Msg);  //Msg(byte[]) is the message to send to clients;
        //server.StopServer();  // this would dispose any resources about the server;
        //server.protocal+= ProtocalFunctions; //ProtocalFunctions(Socket socket) is your function that you wish to be evoked after the server connect setted up, 
        //noted that this function should have one only input parameter, the setted socket; 

        public int LocalPort;
        IPAddress localIP;
        Socket Listensocket;//listen,waiting for connection
        Socket acceptSock;//connected socket,for recieve and send
        Thread thListener;
        Thread threadMsg;
        public delegate void Protocal(Socket socket);
        public Protocal protocal;
        public bool connected = false;

        public Server(int port)
        {
            LocalPort = port;                      
        }
        public void SendMsg(byte[] Msg)
        {
            if (acceptSock != null)
            {
                acceptSock.Send(Msg);
            }
        }
        public void SetupLocalServer(IPAddress address)
        {
            localIP = address;
            IPEndPoint EPHeartbeat = new IPEndPoint(localIP, LocalPort);
            Listensocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); 
            Listensocket.Bind(EPHeartbeat);
            Listensocket.Listen(10);
            thListener = new Thread(new ThreadStart(SmartListener)) { IsBackground = true }; 
            thListener.Start();
        }
        public void SetupLocalServer()
        {
            localIP = IPAddress.Any;
            IPEndPoint EPHeartbeat = new IPEndPoint(localIP, LocalPort);
            Listensocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); 
            Listensocket.Bind(EPHeartbeat);
            Listensocket.Listen(10);
            thListener = new Thread(new ThreadStart(SmartListener)) { IsBackground = true }; 
            thListener.Start();
        }


        private void SmartListener()
        {
            //wait for connect
            acceptSock = Listensocket.Accept();
            connected = true;

            threadMsg = new Thread(ProtocalProcces) { Name = "TunerlTool-ProtocalProcces", IsBackground = true };
            threadMsg.Start();
        }
        void ProtocalProcces()
        {
            if (protocal != null)
            {
                protocal(acceptSock);
            }
        }
        
        public void StopServer()
        {
            if (threadMsg != null)
                this.threadMsg.Abort();
            if (acceptSock != null)
            {
                acceptSock.Close();
                acceptSock = null;
            }
            Listensocket.Close();
            thListener.Abort();

        }
        

    }
    
    public class Data
    {
        int TimingCount = 0;        
        
        int[] SSVEPChannelList = {0, 1, 2, 3, 4, 5, 6, 7 };
        MWArray result;
        MWArray srate = 1000;
        Thread Machine;
        List<SoundPlayer> Player = new List<SoundPlayer>(8);        
        int TargetServo = 0;
        int[] ServoList = { 1,1,2,2,3,3,4,4};
        int DataLength=2000;
        int StartTime = 1000;
        double[,] DataToClass;

        int ChannelNumber;
        public static int BufferLength = 40000;
        int CountPack = 0;

        public delegate void OnlineMachineDelegate();
        public OnlineMachineDelegate OnlineMachine;

        public class Event
        {
            List<int> EventType = new List<int>();
            List<int> EventTiming = new List<int>();
            public int LastType;
            public int LastTiming;
            public List<int> EventTypeGet{get{ return EventType; }}
            public List<int> EventTimingGet { get { return EventTiming; } }
            
            public void AddEvent(int type,int Timing)
            {
                EventType.Add(type);
                EventTiming.Add(Timing);
                LastType = type;
                LastTiming = Timing;
            }
        }
        public Event Events = new Event();
        public int CountPackGet{get{return CountPack;}}
       
        List<float[]> DataBuffL = new List<float[]>();
        List<double[]> DataBuffFiltedL = new List<double[]>();
        float[] CurrentData;

        public bool SaveOn = false;

        public bool OnlineSSVEPOn = false;        
        
        public UdpBroadcaster Feedbacker;        

        double CurrentEvent;
        public double CurrentEventGet
        {
            get
            {
                return CurrentEvent;
            }
        }

        string[] DefaultHeaders = new string[19] { "Fz", "Cz", "T5", "P3", "Pz", "P4", "T6", "Oz", "Closed", "Closed", "Closed", "Closed", "Closed", "Closed", "Closed", "Closed", "LABEL", "TimeStamp", "voltage" };
        //string[] Headers2 = new string[36] { "F3", "FZ", "F4", "FCZ", "C3", "Cz", "C4", "P3", "Pz", "P4", "PO7", "POz", "PO8", "O1", "Oz", "O2", "LABEL","TimeStamp" , "F3Filted", "FZFilted", "F4Filted", "FCZFilted", "C3Filted", "CzFilted", "C4Filted", "P3Filted", "PzFilted", "P4Filted", "PO7Filted", "POzFilted", "PO8Filted", "O1Filted", "OzFilted", "O2Filted", "LABEL", "TimeStamp" };
        TextWriter file;

        List<Filter> Filters;

        int LengthCalcR = 5000;
        int Fs = 1000;
        double[] Cos7p8Hz;
        double[] Sin7p8Hz;        

        public double[] CurrentDataFiltedGet
        {
            get
            {
                double[][] CurrentDataFilted = GetDataFilted(1);
                return CurrentDataFilted[0];
            }
        }

        public Data(int Num)
        {
            ChannelNumber = Num;
            CurrentData = new float[ChannelNumber + 3];
            for (int iBuffInitial = 0; iBuffInitial < BufferLength; iBuffInitial++)
            {
                DataBuffL.Add(new float[ChannelNumber + 3]);
                DataBuffFiltedL.Add(new double[ChannelNumber + 3]);
            }
            Filters = new List<Filter>();
            Cos7p8Hz = new double[LengthCalcR];
            Sin7p8Hz = new double[LengthCalcR];
            for (int i = 0; i < LengthCalcR; i++)
            {
                Cos7p8Hz[i] = Math.Cos(2 * Math.PI * 7.8 * i / Fs);
                Sin7p8Hz[i] = Math.Sin(2 * Math.PI * 7.8 * i / Fs);
            }

            //result = TrcaMachine.TRCALoadPara(srate);
            //result = cca.CCALoadPara(srate);            
            //for (int iPlayer = 0; iPlayer < 8; iPlayer++)
            //{
            //    SoundPlayer NewPlayer = new SoundPlayer();
            //    NewPlayer.SoundLocation = System.Environment.CurrentDirectory + "\\Sounds\\" + (iPlayer + 1).ToString() + ".wav";
            //    Player.Add(NewPlayer);
            //    Player[iPlayer].Load();
            //}
            
            

        }
        public void AddFilter(double[] b, double[] a)
        {
            Filters.Add(new Filter(a, b));
        }
        public void AddFilter(double[] b, double[] a, int Num)
        {
            Filters.Add(new Filter(a, b, Num));
        }
        public void FilterSwitcher(int FilterId, bool OnOff)
        {
            Filters[FilterId].FilterOn = OnOff;
        }
        public void FilterSwitcher(bool[] OnOff)
        {
            if (OnOff.Length == Filters.Count)
            {
                for (int iFilter = 0; iFilter < Filters.Count; iFilter++)
                {
                    Filters[iFilter].FilterOn = OnOff[iFilter];
                }
            }

        }
        public bool[] FilterSwitcher()
        {
            bool[] OnOff = new bool[Filters.Count];
            for (int iFilter = 0; iFilter < Filters.Count; iFilter++)
            {
                OnOff[iFilter] = Filters[iFilter].FilterOn;
            }
            return OnOff;
        }
        public void WriteHeader(string Path)
        {

            file = new StreamWriter(Path, false);
            foreach (string x in DefaultHeaders)
            {
                file.Write(x + ",");
            }
            //file.Close();          

        }
        public void WriteHeader(string Path, string[] Headers)
        {

            file = new StreamWriter(Path, false);
            foreach (string x in Headers)
            {
                file.Write(x + ",");
            }
            //file.Close();          

        }
        public void CloseSaveFile()
        {
            if (SaveOn == true)
            {
                SaveOn = false;
                file.Close();
            }
        }

        public void PushData(float[] floatdata)
        {
            CountPack++;
            CurrentData = floatdata;
            DataBuffL.Add(CurrentData);
            DataBuffL.RemoveAt(0);
            CurrentEvent = CurrentData[ChannelNumber];
            //Thread Machine;

            //filt
            double[] DataFilted = new double[ChannelNumber];
            for (int iChannel = 0; iChannel < CurrentData.Length - 3; iChannel++)
                DataFilted[iChannel] = CurrentData[iChannel];

            for (int iFilter = 0; iFilter < Filters.Count; iFilter++)
            {
                if (Filters[iFilter].FilterOn)
                {
                    DataFilted = Filters[iFilter].Filt(DataFilted);
                }
            }

            double[] CurrentDataFilted = new double[ChannelNumber + 3];
            for (int iChannel = 0; iChannel < CurrentData.Length - 3; iChannel++)
            { CurrentDataFilted[iChannel] = DataFilted[iChannel]; }
            CurrentDataFilted[ChannelNumber] = floatdata[ChannelNumber];
            CurrentDataFilted[ChannelNumber + 1] = floatdata[ChannelNumber + 1];
            CurrentDataFilted[ChannelNumber + 2] = floatdata[ChannelNumber + 2];


            DataBuffFiltedL.Add(CurrentDataFilted);
            DataBuffFiltedL.RemoveAt(0);

            if (CurrentData[ChannelNumber] != 0)            {
                
                Events.AddEvent((int)CurrentData[ChannelNumber], CountPack);
            }
            
            // record
            if (SaveOn)
            {
                file.WriteLine();
                for (int index = 0; index < ChannelNumber + 3; index++)
                {
                    file.Write(floatdata[index] + ",");
                }
            }
            //if(Machine!=null)
            //{
            //    Machine();
            //}

            //online machine  
            //next steps by Yukun: take this part out of TunerlTools into MainForm; realize online machine runs each time an event received, NO MATER IF NEXT EVENT HAPPEN BEFORE THAT;
            //OnlineSSVEP()
            OnlineMachine?.Invoke();
        }
        
        void OnlineSSVEP()
        {
            if (CurrentEvent < 50 & CurrentEvent > 30)
            {
                //Mode = (int)floatdata[16] % 10;
                //Phase = (int)floatdata[16] / 10;                
                TimingCount = CountPack;
                //
            }
            if (TimingCount != 0)
            {
                if (CountPack == TimingCount + DataLength - 1)
                {
                    float[][] DataTem = GetDataRaw(StartTime + DataLength);
                    DataToClass = new double[StartTime + DataLength, SSVEPChannelList.Length];
                    for (int i = 0; i < StartTime + DataLength; i++)
                    {
                        for (int iChannel = 0; iChannel < SSVEPChannelList.Length; iChannel++)
                            DataToClass[i, iChannel] = DataTem[i][SSVEPChannelList[iChannel]];
                    }

                    Machine = new Thread(CCAControl) { IsBackground = true };
                    Machine.Start();
                }
            }

        }
        void CCAControl()
        {
            MWArray FreqNum = (MWNumericArray)new int[]{ 1,2,3,4,5,6,7,8};
            MWNumericArray Onlinedata = new MWNumericArray();
            Onlinedata = (MWNumericArray)DataToClass;
            MWArray BandUsed = (MWNumericArray)new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            MWArray length = DataLength / 1000f;
            //result = cca.CCAMachine(Onlinedata, length,StartTime,srate, FreqNum,BandUsed);
            
            //double[,] Xresult = (double[,])result.ToArray();
            //int XIresult = (int)Xresult[0, 0];
            ////XIresult = LastTimingMsg - 32;//for test
            //Player[XIresult - 1].Play();
                    
            
        }
        
        

        
        public float[][] GetDataRaw(int Length)
        {
            float[][] Data = new float[Length][];
            DataBuffL.CopyTo(BufferLength - Length, Data, 0, Length);
            return Data;
        }
        public double[][] GetDataFilted(int Length)
        {
            double[][] Data = new double[Length][];
            DataBuffFiltedL.CopyTo(BufferLength - Length, Data, 0, Length);
            return Data;
        }
        public double[] CalculateResistance()
        {
            double[] Sum1 = new double[ChannelNumber];
            double[] Sum2 = new double[ChannelNumber];
            double[] Sum = new double[ChannelNumber];
            double[] SumAll = new double[ChannelNumber];
            double[] Z = new double[ChannelNumber];
            for (int iChannel = 0; iChannel < ChannelNumber; iChannel++)
            {
                for (int i = 0; i < LengthCalcR; i++)
                {
                    Sum[iChannel] += DataBuffFiltedL[(-i + BufferLength - 1)][iChannel];
                }
                for (int i = 0; i < LengthCalcR; i++)
                {
                    Sum1[iChannel] += (DataBuffFiltedL[(-i + BufferLength - 1)][iChannel] - Sum[iChannel] / LengthCalcR) * Cos7p8Hz[i] * 2 / LengthCalcR;
                    Sum2[iChannel] += (DataBuffFiltedL[(-i + BufferLength - 1)][iChannel] - Sum[iChannel] / LengthCalcR) * Sin7p8Hz[i] * 2 / LengthCalcR;
                }
                SumAll[iChannel] = Math.Sqrt(Math.Pow(Sum1[iChannel], 2) + Math.Pow(Sum2[iChannel], 2)) / (2 / Math.PI);
                Z[iChannel] = (24 * SumAll[iChannel] / 6 - 4400) / 2;

            }
            return Z;
        }
        public double GetEvent()
        {
            double e = CurrentEvent;
            return e;
        }
    }

    public class Filter
    {
        //filter = new Filter(a, b, ChannelNumber)
        //filter.Filt(double[ChannelNumber] data)
        //filter will save a,b,x,y. for a signal, sequencially use filter.Filt for each data point in the signal gives result.
        //Yukun

        double[][] FiltBuffX;
        double[][] FiltBuffY;
        double[] A;
        double[] B;
        int ChannelQuantity;
        public bool FilterOn;
        public Filter(double[] a, double[] b)
        {
            ChannelQuantity = 1;
            A = a; B = b;
            FiltBuffX = new double[ChannelQuantity][];
            FiltBuffY = new double[ChannelQuantity][];
            for (int iChannel = 0; iChannel < ChannelQuantity; iChannel++)
            {
                FiltBuffX[iChannel] = new double[Math.Max(a.Length, b.Length)];
                FiltBuffY[iChannel] = new double[Math.Max(a.Length, b.Length)];
            }
            FilterOn = true;
        }
        public Filter(double[] a, double[] b, int ChannelQuantityInput)
        {
            ChannelQuantity = ChannelQuantityInput;
            A = a; B = b;
            FiltBuffX = new double[ChannelQuantity][];
            FiltBuffY = new double[ChannelQuantity][];
            for (int iChannel = 0; iChannel < ChannelQuantity; iChannel++)
            {
                FiltBuffX[iChannel] = new double[Math.Max(a.Length, b.Length)];
                FiltBuffY[iChannel] = new double[Math.Max(a.Length, b.Length)];
            }
            FilterOn = true;
        }
        public double[] Filt(double[] x)
        {

            double[] CurrentDataFilted = new double[ChannelQuantity];
            for (int iChannel = 0; iChannel < ChannelQuantity; iChannel++)
            {
                BuffPusher(ref FiltBuffX[iChannel], x[iChannel]);
                for (int n = 0; n < B.Length; n++)
                {
                    CurrentDataFilted[iChannel] += FiltBuffX[iChannel][FiltBuffX[iChannel].Length - 1 - n] * B[n];
                }
                for (int n = 1; n < B.Length; n++)
                {
                    CurrentDataFilted[iChannel] -= FiltBuffY[iChannel][FiltBuffY[iChannel].Length - n] * A[n];
                }
                CurrentDataFilted[iChannel] /= A[0];
                BuffPusher(ref FiltBuffY[iChannel], CurrentDataFilted[iChannel]);
            }
            double[] aa = CurrentDataFilted;
            return CurrentDataFilted;
        }
        double[] BuffPusher(ref double[] Buff, double NewData)
        {
            List<double> BuffList = Buff.ToList();
            BuffList.Add(NewData);
            BuffList.RemoveAt(0);
            Buff = BuffList.ToArray();
            return Buff;
        }

    }

    
    public class RoboticArmControler
    {
        //this class fade with the fall of last generate robotic arm
        //but it may still offer an example of bluetooth communication
        string[] ServoActuatorList = { "0", "16", "17", "18", "1", "6" };// info from the control board of the robotic arm: ID of each working machine
        SerialPort SerialOut;
        int[] Pos;
        int[] OriginPos = { 2000, 1500, 1500, 1500, 2500, 1700 };
        int[] PosUpBound = {2100,1750,2000,2500,2500,2500 };
        int[] PosSubBound = { 1700, 1250, 1000, 500, 500, 500 };

        public RoboticArmControler()
        {
            Pos = OriginPos;
            string[] ports = SerialPort.GetPortNames();
            SerialOut = new SerialPort(ports[1]);
            SerialOut.Open();

        }
        public RoboticArmControler(string Port)
        {
            Pos = OriginPos;            
            SerialOut = new SerialPort(Port);
            SerialOut.Open();

        }
        public void Move(int IDServo, bool ClockWise, int speed)
        {
            IDServo--;
            Pos[IDServo] += ClockWise ? speed : -speed;            

            for (int iServo = 0; iServo < OriginPos.Length; iServo++)
            {
                Pos[IDServo] = Pos[IDServo] > PosUpBound[IDServo] ? PosUpBound[IDServo] : Pos[IDServo];
                Pos[IDServo] = Pos[IDServo] < PosSubBound[IDServo] ? PosSubBound[IDServo] : Pos[IDServo];
            }

            //SerialOut.WriteLine("#"+ServoActuatorList[IDServo]+" P2100 S200" + "\r\n");
            //for (int iServo = 0; iServo < OriginPos.Length; iServo++)
            //{
            //    SerialOut.WriteLine("#" + ServoActuatorList[iServo] + " P" + Pos[iServo] + "S400" + "\r\n");               
            //}
            SerialOut.WriteLine("#" + ServoActuatorList[IDServo] + " P" + Pos[IDServo] + "S400" + "\r\n");
        }
        public void Close()
        {
            SerialOut.Close();
        }
        public void Reset()
        {
            for (int iServo = 0; iServo < OriginPos.Length; iServo++)
                SerialOut.WriteLine("#" + ServoActuatorList[iServo] + " P" + OriginPos[iServo].ToString() + "S500" + "\r\n");
        }
    }

    class PlotMaster
    {
        Form mainform;
        double TimeOnPlot = 0;
        double TimeLengthOnPlot = 10000;
        double RefreshSparing = 40;//5*1000/60;
        int BeginPackCount = 0;
        public int SampleRate = 1000;

        double[] BaseLine;
        public int[] ChannelOn= { 0,1,2,3,4,5,6,7};
        int LastCountPack;
        int PointSparing = 1;
        double PixPerUv = 0.125;
        bool AllReplot = false;
        public double[] FinalZ;
        int ChannelNumber;
        System.Timers.Timer timer;
        private int inTimer = 0;
        //Drawings
        Pen PenPool = new Pen(Color.Gray, 1);
        SolidBrush BrushPool = new SolidBrush(Color.White);
        SolidBrush BrushBlack = new SolidBrush(Color.Black);
        Font FChannel = new Font("隶书", 14, FontStyle.Bold);
        Font FBaseLine = new Font("隶书", 10, FontStyle.Bold);
        HatchBrush BChannel = new HatchBrush(HatchStyle.Percent05, Color.Blue, Color.White);
        StringFormat SFChannel = new StringFormat();
        StringFormat ImpFromat = new StringFormat();
        //Pool related
        Pen PenPoolLine = new Pen(Color.Gray);
        int PoolTop;
        int PoolBottom;
        public int PoolLeft;
        int PoolRight;
        int PoolHeight;
        int PoolWidth;
        public int[] Space= { 30,30,30,30};//top left bottom right
        Rectangle Pool;
        Rectangle PoolChannel;
        public string[] Channels = {"TP8"," P7"," F7"," M1"," FP1"," F3 ","C3 ","T7"," CZ ","FZ"," PZ"," OZ"," O1",
            " FT7 ","TP7"," CP3"," C4"," P4 ","F4"," FC4 ","O2 ","CP4"," FP2 ","CPZ"," M2 ","T8"," FT8 ","P3"," P8"," FCZ"," FC3"," F8"};
        Data data;
        public int Phase=0;
        public PlotMaster(Form form,int num,Data datain)
        {
            mainform = form;
            ChannelNumber = num;
            data = datain;
            BaseLine = new double[ChannelNumber];
            //Pool related
            PreparePool();
            ImpFromat.Alignment = StringAlignment.Center;
            
            timer = new System.Timers.Timer(RefreshSparing);
            timer.Elapsed += new System.Timers.ElapsedEventHandler(Timing); //到达时间的时候执行事件；   
            timer.AutoReset = true;   //设置是执行一次（false）还是一直执行(true)；     
        }
        
        void PreparePool()
        {
            
            PoolTop = Space[0];
            PoolBottom = mainform.Height - Space[2]; ;
            PoolLeft = Space[1];
            PoolRight = mainform.Width - Space[3]; ;
            PoolHeight = PoolBottom - PoolTop;
            PoolWidth = PoolRight - PoolLeft;
            PoolChannel = new Rectangle(Space[1] - 50, Space[0], 50, PoolHeight);
            Pool = new Rectangle(PoolLeft, PoolTop, PoolWidth, PoolHeight);
        }
        public void TimerSwitcher(bool On)
        {
            timer.Enabled = On;
        }
        public void Reset()
        {
            TimeOnPlot = 0;
            BaseLine = new double[ChannelNumber];
        }
        public void ReplotAll()
        {
            AllReplot = true;
            PreparePool();
            mainform.Invalidate(Pool);
            mainform.Invalidate(PoolChannel);
        }

        public void InitializeDataAcquire()
        {
            BaseLine = data.CurrentDataFiltedGet;
            LastCountPack = data.CountPackGet;
            BeginPackCount = data.CountPackGet;
        }
        public void PixPerVtg(float times)
        {
            PixPerUv = PixPerUv * times;
        }
        public void PlotLength(bool add)
        {
            if (add)
            {
                if (TimeLengthOnPlot < 30000)
                {
                    double LastTimeLengthOnPlot = TimeLengthOnPlot;
                    //PointSparing = 5;
                    if (TimeLengthOnPlot < 20000)
                        TimeLengthOnPlot = TimeLengthOnPlot * 2;
                    else
                        TimeLengthOnPlot = 30000;


                    TimeOnPlot = (data.CountPackGet - BeginPackCount) % TimeLengthOnPlot;

                    //Ploter.AllReplot = true;
                    ReplotAll();
                    //Invalidate();
                }
            }
            else
            {
                if (TimeLengthOnPlot > 5000)
                {
                    double LastTimeLengthOnPlot = TimeLengthOnPlot;
                    //PointSparing = 5;
                    if (TimeLengthOnPlot == 30000)
                        TimeLengthOnPlot = 20000;
                    else if (TimeLengthOnPlot < 30000)
                        TimeLengthOnPlot = TimeLengthOnPlot / 2;


                    TimeOnPlot = (data.CountPackGet - BeginPackCount) % TimeLengthOnPlot;

                    //Ploter.AllReplot = true;
                    ReplotAll();
                    //Invalidate();
                }
            }

        }
        public void RefreshBaseLine()
        {
            BaseLine = data.CurrentDataFiltedGet;
        }
        public void PlotPool(PaintEventArgs e)
        {
            PreparePool();
            Graphics Drawer = e.Graphics;

            Drawer.FillRectangle(BrushPool, Pool);
            Drawer.SmoothingMode = SmoothingMode.AntiAlias;
            SFChannel.Alignment = StringAlignment.Near;            
            for (int i = 0; i < ChannelOn.Count(); i++)
            {
                Drawer.DrawString(Channels[ChannelOn[i]], FChannel,
                    new SolidBrush
                    (
                    Color.FromArgb(
                        (int)(2 * Math.Abs((float)i / (float)ChannelOn.Count() - 1.0 / 2.0) * 255),
                        (int)(2 * Math.Abs(0.5 - 0.5 * Math.Sign((float)i / (float)ChannelOn.Count() - 1.0 / 3.0) + (float)i / (float)ChannelOn.Count() - 5.0 / 6.0) * 255),
                        (int)(2 * Math.Abs(-0.5 + 0.5 * Math.Sign(-(float)i / (float)ChannelOn.Count() + 2.0 / 3.0) + (float)i / (float)ChannelOn.Count() - 1.0 / 6.0) * 255)
                        )
                    ),
                    PoolLeft - 50, PoolTop + (PoolHeight) / (ChannelOn.Count()) * (i), SFChannel);

                //Drawer.DrawString(BaseLine[ChannelOn[i]].ToString(), FBaseLine, BChannel, PoolLeft-80, PoolTop + (PoolHeight) / (ChannelOn.Count()) * (i) + 16, SFChannel);
                Drawer.DrawLine(PenPoolLine, PoolLeft, PoolTop + (PoolHeight) / (ChannelOn.Count()) * (i), PoolRight, PoolTop + (PoolHeight) / (ChannelOn.Count()) * (i));
            }
            //Drawer.DrawString(mainform.data.CountPack.ToString(), FChannel, BChannel, mainform.Width * 12 / 20, PoolTop + PoolHeight + 20, SFChannel);                
            Drawer.DrawString((PoolHeight / ChannelOn.Count() / PixPerUv).ToString(), FChannel, BChannel, mainform.Width * 16 / 20, PoolTop + PoolHeight + 20, SFChannel);

            Drawer.DrawRectangle(PenPool, Pool);
        }
        public void PlotEEG(PaintEventArgs e)
        {
            Graphics Waver = e.Graphics;

            //TimeOnPlot = mainform.TimeOnPlot;
            //TimeLengthOnPlot = mainform.TimeLengthOnPlot;
            //BaseLine = mainform.BaseLine;
            //BeginPackCount = mainform.BeginPackCount;


            Waver.SmoothingMode = SmoothingMode.AntiAlias;
            Font FChannel = new Font("隶书", 14, FontStyle.Bold);
            Font FLabel = new Font("隶书", 12, FontStyle.Bold);
            Font FBaseLine = new Font("隶书", 10, FontStyle.Bold);
            HatchBrush BChannel = new HatchBrush(HatchStyle.Percent05, Color.Blue, Color.Black);
            SolidBrush BrushTimeStamp = new SolidBrush(Color.Black);
            StringFormat SFChannel = new StringFormat();
            SFChannel.Alignment = StringAlignment.Near;


            Pen PEegWave = new Pen(Color.Blue, 1);
            Point[][] PointEegWave = new Point[ChannelNumber][];//(int)TimeLengthOnPlot

            Pen PTimeLine = new Pen(Color.Black, 1);
            Point[] PointTimeLine = new Point[2];
            PointTimeLine[0].X = (int)(((TimeOnPlot + RefreshSparing) / TimeLengthOnPlot) * PoolWidth + PoolLeft);
            PointTimeLine[0].Y = PoolTop;
            PointTimeLine[1].X = PointTimeLine[0].X;
            PointTimeLine[1].Y = PoolBottom;
            int z = (int)((TimeOnPlot / TimeLengthOnPlot));
            Waver.DrawLines(PTimeLine, PointTimeLine);
            //double[] Event = { mainform.data.CurrentDataFiltedGet, TimeOnPlot };
            //if(Event[0]!=0)
            //Waver.DrawString(Event[0].ToString(), FChannel, BrushPool, (int)(Event[1] / TimeLengthOnPlot * PoolWidth + PoolLeft), PoolTop + PoolHeight + 2);



            //PlotLength = (int)(AllReplot == false ?RefreshSparing*4:Data.BufferLength);
            if (AllReplot == false)
            {
                lock (this)
                {
                    for (int iChannel = 0; iChannel < ChannelOn.Count(); iChannel++)
                        PointEegWave[iChannel] = new Point[(int)RefreshSparing * 6];
                    for (int iChannel = 0; iChannel < ChannelOn.Count(); iChannel++)
                    {
                        double[][] Data = data.GetDataFilted((int)RefreshSparing * 6);
                        for (int iTimeOnPlot = 0; iTimeOnPlot < RefreshSparing * 6; iTimeOnPlot += PointSparing)
                        {
                            int yCenter = (int)(PoolTop + (PoolHeight) / (ChannelOn.Count()) * (iChannel + 0.5f));
                            int x = (int)Math.Floor(((data.CountPackGet - BeginPackCount) % TimeLengthOnPlot + RefreshSparing - iTimeOnPlot) / TimeLengthOnPlot * PoolWidth + PoolLeft);
                            int y = (int)Math.Floor((Data[(int)RefreshSparing * 6 - iTimeOnPlot - 1][ChannelOn[iChannel]] - BaseLine[ChannelOn[iChannel]]) * PixPerUv + yCenter);
                            PointEegWave[iChannel][iTimeOnPlot / PointSparing].X = x > PoolLeft ? x : PoolLeft;
                            PointEegWave[iChannel][iTimeOnPlot / PointSparing].Y = x > PoolLeft ? y : yCenter;



                            if (Data[(int)RefreshSparing * 6 - iTimeOnPlot - 1][ChannelNumber] != 0)
                                Waver.DrawString(Data[(int)RefreshSparing * 6 - iTimeOnPlot - 1][ChannelNumber].ToString(), FLabel, BrushBlack, PointEegWave[iChannel][iTimeOnPlot / PointSparing].X + 2, PoolTop);
                            //Waver.DrawString(Data[(int)RefreshSparing * 6 - iTimeOnPlot - 1][ChannelNumber].ToString(), FLabel, BrushBlack, PoolLeft+10, PoolTop+100);

                        }
                    }
                }
            }
            else
            {
                PreparePool();

                lock (this)
                {
                    for (int iChannel = 0; iChannel < ChannelOn.Count(); iChannel++)
                        PointEegWave[iChannel] = new Point[(int)TimeLengthOnPlot];
                    for (int iChannel = 0; iChannel < ChannelOn.Count(); iChannel++)
                    {
                        double[][] Data = data.GetDataFilted((int)TimeLengthOnPlot);
                        for (int iTimeOnPlot = 0; iTimeOnPlot < TimeLengthOnPlot; iTimeOnPlot += PointSparing)
                        {
                            int yCenter = (int)(PoolTop + (PoolHeight) / (ChannelOn.Count()) * (iChannel + 0.5f));
                            int x = (int)Math.Floor(((data.CountPackGet - BeginPackCount) % TimeLengthOnPlot + RefreshSparing - iTimeOnPlot) / TimeLengthOnPlot * PoolWidth + PoolLeft);
                            int y = (int)Math.Floor((Data[(int)TimeLengthOnPlot - iTimeOnPlot - 1][ChannelOn[iChannel]] - BaseLine[ChannelOn[iChannel]]) * PixPerUv + yCenter);
                            PointEegWave[iChannel][iTimeOnPlot / PointSparing].X = x > PoolLeft ? x : PoolLeft;
                            PointEegWave[iChannel][iTimeOnPlot / PointSparing].Y = x > PoolLeft ? y : yCenter;

                            //PointEegWave[iChannel][iTimeOnPlot / PointSparing].X =
                            //    (int)Math.Floor((TimeOnPlot + RefreshSparing - iTimeOnPlot) / TimeLengthOnPlot * PoolWidth + PoolLeft);
                            ////PointEegWave[iChannel][iTimeOnPlot].X = (int)((TimeOnPlot + RefreshSparing) / TimeLengthOnPlot * Width)+25-iTimeOnPlot;
                            //PointEegWave[iChannel][iTimeOnPlot / PointSparing].Y =
                            //    (int)Math.Floor((Data[(int)TimeLengthOnPlot - iTimeOnPlot - 1][ChannelOn[iChannel]] - BaseLine[ChannelOn[iChannel]]) * PixPerUv + PoolTop + (PoolHeight) / (ChannelOn.Count()) * (iChannel + 0.5));

                        }
                    }
                }
                AllReplot = false;
            }

            for (int iChannel = 0; iChannel < ChannelOn.Count(); iChannel++)
            {
                Waver.DrawLines(
                    new Pen
                    (
                        Color.FromArgb(
                    (int)(2 * Math.Abs((float)iChannel / (float)ChannelOn.Count() - 1.0 / 2.0) * 255),
                    (int)(2 * Math.Abs(0.5 - 0.5 * Math.Sign((float)iChannel / (float)ChannelOn.Count() - 1.0 / 3.0) + (float)iChannel / (float)ChannelOn.Count() - 5.0 / 6.0) * 255),
                    (int)(2 * Math.Abs(-0.5 + 0.5 * Math.Sign(-(float)iChannel / (float)ChannelOn.Count() + 2.0 / 3.0) + (float)iChannel / (float)ChannelOn.Count() - 1.0 / 6.0) * 255)
                    )
                        , 1),
                    PointEegWave[iChannel]);
            }

            for (int i = 0; i < TimeLengthOnPlot / SampleRate; i++)
            {
                int TimeStamp = ((int)((data.CountPackGet - BeginPackCount) / SampleRate / (TimeLengthOnPlot / SampleRate)) * (int)(TimeLengthOnPlot / SampleRate) + i);
                Waver.DrawString(TimeStamp.ToString(), FBaseLine, BrushTimeStamp, (int)(PoolWidth / TimeLengthOnPlot * SampleRate * i + PoolLeft), PoolHeight + PoolTop - 13, SFChannel);

            }
        }
        public void PlotResistance(PaintEventArgs e)
        {
            Graphics Waver = e.Graphics;
            Font FChannel = new Font("隶书", (mainform.Height) / 19 - 2, FontStyle.Bold);
            Font FBaseLine = new Font("隶书", (mainform.Height) / 38, FontStyle.Bold);
            HatchBrush BChannel = new HatchBrush(HatchStyle.Percent05, Color.Blue, Color.Black);
            StringFormat SFChannel = new StringFormat();
            SFChannel.Alignment = StringAlignment.Near;
            double[] Z = new double[ChannelNumber];
            Z = data.CalculateResistance();
            for (int i = 0; i < ChannelOn.Count(); i++)
            {
                Waver.DrawString(Z[ChannelOn[i]].ToString("f0"), FBaseLine, BChannel, PoolLeft + 10, (int)(PoolTop + (PoolHeight) / (ChannelOn.Count()) * (i - 0.3)), SFChannel);
            }
        }
        public void PlotResistanceIri(PaintEventArgs e, int row, int column, int[] PosList)
        {
            Graphics Painter = e.Graphics;
            int[] GridPosX = new int[row * column];
            int[] GridPosY = new int[row * column];
            int ImpRectWidth = (int)(PoolWidth / (column + 1) * 0.6);
            int ImpRectHeight = (int)(PoolHeight / (row + 1) * 0.6);
            double[] Z = new double[ChannelNumber];
            Z = data.CalculateResistance();
            FinalZ = Z;
            int[] StepValue = { 100000, 50000, 20000, 5000 };
            for (int iPos = 0; iPos < row * column; iPos++)
            {
                GridPosX[iPos] = PoolWidth / (column + 1) * (iPos % column + 1) + PoolLeft;
                GridPosY[iPos] = PoolHeight / (row + 1) * (iPos / column + 1) + PoolTop;
            }
            for (int iChannel = 0; iChannel < PosList.Length; iChannel++)
            {
                if (PosList[iChannel] >= 0)
                {
                    Painter.FillRectangle(
                        new SolidBrush(Z[PosList[iChannel]] > StepValue[0] ? Color.Red : Z[PosList[iChannel]] > StepValue[1] ? Color.Yellow : Z[PosList[iChannel]] > StepValue[2] ? Color.Green : Z[PosList[iChannel]] > StepValue[3] ? Color.Blue : Color.Black),
                        GridPosX[iChannel] - ImpRectWidth * 0.5f, GridPosY[iChannel] - ImpRectHeight * 0.5f, ImpRectWidth, ImpRectHeight
                        );
                    Painter.DrawString(Channels[PosList[iChannel]], FChannel, new SolidBrush(Z[PosList[iChannel]] > StepValue[3] ? Color.Black : Color.White), GridPosX[iChannel], GridPosY[iChannel] - FChannel.Height * 0.5f, ImpFromat);
                }
            }
        }
        void Timing(object source, System.Timers.ElapsedEventArgs e)
        {
            //AllReplot = true;
            //TimeOnPlot = (server.CountPack - BeginPackCount) % TimeLengthOnPlot;
            //if(LastCountRound< (int)((server.CountPack - BeginPackCount) / TimeLengthOnPlot))
            //{
            //    LastCountRound =(int)((server.CountPack - BeginPackCount) / TimeLengthOnPlot);
            //    BaseLine = server.CurrentData;
            //}
            TimeOnPlot += (data.CountPackGet - LastCountPack);
            if (TimeOnPlot / TimeLengthOnPlot >= 1)
            {
                BaseLine = data.CurrentDataFiltedGet;
            }
            TimeOnPlot = TimeOnPlot % TimeLengthOnPlot;
            LastCountPack = data.CountPackGet;

            //TimeOnPlot = data.TimeOnPlot;
            //TimeLengthOnPlot = data.TimeLengthOnPlot;
            if (inTimer == 0)
            {
                inTimer = 1;
                //Invalidate();
                int FreshLeftEdge = (int)((TimeOnPlot) / TimeLengthOnPlot * PoolWidth + PoolLeft) - (int)((double)PoolWidth / TimeLengthOnPlot * RefreshSparing * 2);
                mainform.Invalidate(
                    new Rectangle(
                        FreshLeftEdge < PoolLeft ? PoolLeft : FreshLeftEdge,
                        PoolTop,
                        (int)((double)PoolWidth / TimeLengthOnPlot * RefreshSparing * 4),
                        PoolHeight)
                        );

                //mainform.Invalidate(new Rectangle((int)((TimeOnPlot) / TimeLengthOnPlot * PoolWidth + PoolLeft) + 5, PoolTop + PoolHeight, 20, 20));
                mainform.Invalidate(new Rectangle(PoolRight, 0, 50, mainform.Height));
                //mainform.Invalidate(new Rectangle(PoolLeft-50, 0, 50, mainform.Height));
                //Invalidate(new Rectangle(PoolRight,0,Width-PoolRight,PoolHeight));
                //Invalidate(new Rectangle(0, 0, Width, PoolTop));
                //Invalidate(new Rectangle(0, PoolTop + PoolHeight, Width, Height - (PoolTop + PoolHeight)));
                //mainform.Invalidate(new Rectangle(mainform.Width * 12 / 20, PoolTop + PoolHeight + 20, 40, 20));

                if (Phase == 2)
                    //{ mainform.Invalidate(new Rectangle(PoolLeft, PoolTop - 5, PoolWidth, PoolHeight)); }
                    mainform.Invalidate();

                inTimer = 0;
            }
        }
    }

    public class TunerlAmplifierProtocal
    {
        //TunerlAmplifierProtocal(int Num,Data datain) bulids a Protocal object, 'Num'=channel number, 'data' is a data collect object, which provide pushdata
        // Problems: this Protocal now include data object in, which is not convenient. 
        //By Yukun
        int PackLength;
        Data data;
        
        int ChannelNum;
        public TunerlAmplifierProtocal(int Num,Data datain)
        {
            ChannelNum = Num;
            PackLength = (8 + 3 * ChannelNum);
            data = datain;            
            
        }
        public void WatchMsg(Socket acceptSock)
        {
            byte[] DataTemp = new byte[PackLength];
            byte[] arrMsg = new byte[PackLength * 1000];
            while (true)
            {
                if (acceptSock != null)
                {
                    //开始等待接受消息,开辟缓存
                    arrMsg.Initialize();
                    //返回真实数据长度
                    int inLen = acceptSock.Receive(arrMsg);
                    //假如客户端并没有发消息或缺少数据包  则忽略跳过继续接收
                    if (inLen == 0 || inLen < PackLength) { MessageBox.Show("数据包丢失"); }

                    //将实际客户端发来的字节数复制到一个新的数组，否则缓存过大后面将产产生过多的00000；缓存过小则对某些数据比如图片流 不能一次性接收及时显示，还需要根据不同的图片格式进行图片头和尾的切割。
                    byte[] NewByte = new byte[inLen];
                    Array.Copy(arrMsg, 0, NewByte, 0, inLen);

                    //拆包
                    List<byte[]> ListByte = new List<byte[]> { };
                    try
                    {
                        ListByte = UnpackMsg(NewByte);
                    }
                    catch
                    {
                        MessageBox.Show("UnpackMsg出错");
                    }


                    if (ListByte == null) //非指定需要的数据，返回null
                    {
                        continue;
                    }
                    else
                    {
                        for (int i = 0; i < ListByte.Count; i++)
                        {
                            DataTemp = ListByte[i];
                            DataAnalysis(DataTemp);
                            //try { DataAnalysis(DataTemp); }
                            //catch { MessageBox.Show("DataAnalysis出错"); }
                        }
                    }

                }
                else
                {
                    return;
                }
            }

        }
        private List<byte[]> UnpackMsg(byte[] buffer)//recongnize packs
        {
            //这里很重要，此传输协议我以"0xA0"作为包头，“0xC0” 作为包尾做例子
            List<byte[]> LstTemp = new List<byte[]>();
            int HeadFlag = 0;
            int TailFlag = 0;
            //byte[] bufferTemp = new byte[buffer.Length];
            for (HeadFlag = 0; HeadFlag < buffer.Length; HeadFlag += PackLength)//拆包：逐个自己搜索，检测到包头后同时检测对应包尾是否正确
            {
                TailFlag = HeadFlag + PackLength - 1;
                if (buffer[HeadFlag] == 160 && buffer[TailFlag] == 192)
                {
                    byte[] bufferTemp = new byte[PackLength];
                    Array.ConstrainedCopy(buffer, HeadFlag, bufferTemp, 0, PackLength);
                    LstTemp.Add(bufferTemp);

                }
                //if (TailFlag >= buffer.Length - 1) break;
            }
            return LstTemp;//返回该集合
        }
        private void DataAnalysis(byte[] packsource)//数据解析
        {
            //截取数据段
            byte[] datasource = new byte[ChannelNum * 3];
            for (int n = 0; n < ChannelNum * 3; n++)
            {
                datasource[n] = packsource[n + 6];
            }
            //十进制 转 十六进制 同时拼接 加正负号判断
            int[] inttemp = new int[ChannelNum];
            for (int i = 0; i < ChannelNum; i++)
            {
                inttemp[i] = ((0xFF & datasource[3 * i]) << 16) | ((0xFF & datasource[3 * i + 1]) << 8) | (0xFF & datasource[3 * i + 2]);//三字符拼接 //July25！
                if ((inttemp[i] & 0x00800000) > 0)
                { inttemp[i] |= unchecked((int)0xFF000000); }//不检查是否溢出
                else
                { inttemp[i] &= 0x00FFFFFF; }
            }
            //换算为电位
            //float[] floatdata = new float[17];//16通道+标签
            float[] floatdata = new float[ChannelNum + 3];//16通道+1标签+1时间戳+1power
            for (int m = 0; m < ChannelNum; m++)
            {
                floatdata[m] = ((inttemp[m] * (float)4.5) / 8388607) * 1000000 / 24;//(2的23次方-1) 该到此
            }
            //添加标签位
            floatdata[ChannelNum] = (float)packsource[PackLength - 2];
            //添加时间戳
            int timetemp = 0;
            timetemp = ((0xFF & packsource[2]) << 24) | ((0xFF & packsource[3]) << ChannelNum) | ((0xFF & packsource[4]) << 8) | ((0xFF & packsource[5]));//三字符拼接
            floatdata[ChannelNum + 1] = timetemp;
            floatdata[ChannelNum + 2] = (float)packsource[1];

            data.PushData(floatdata);

        }
    }

    class UDP
    {
        //Simple UDP example
        //By Yukun
        Socket server;
        String LocalAddress = "192.168.1.105";  // "192.168.1.105"
        int LocalPort = 7040;  // 7040
        String RemoteAddress;
        int RemotePort;
        byte[] buffer = new byte[1024];
        public UDP()
        {
            server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            server.Bind(new IPEndPoint(IPAddress.Parse(LocalAddress), LocalPort));//绑定端口号和IP            
            Thread t = new Thread(ReciveMsg);//开启接收消息线程
            t.Start();
        }
        
        /// 接收发送给本机ip对应端口号的数据报
        /// </summary>
        public void ReciveMsg()
        {
            while (true)
            {
                EndPoint point = new IPEndPoint(IPAddress.Any, 0);//用来保存发送方的ip和端口号                
                int length = server.ReceiveFrom(buffer, ref point);//接收数据报                


            }
        }
        public void sendMsg()
        {
            EndPoint point = new IPEndPoint(IPAddress.Parse(RemoteAddress), RemotePort);
            while (true)
            {
                string msg = Console.ReadLine();
                server.SendTo(Encoding.UTF8.GetBytes(msg), point);
            }


        }
        public void Close()
        {
            server.Close();
        }

    }

    
    class TCPClient
    {

        Socket clientSocket;
        public bool Connected { get { return clientSocket.Connected; } }
        public TCPClient()
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }
        public void Bind(string LocalHost, int port)
        {
            IPEndPoint LocalEndpoint = new IPEndPoint(IPAddress.Parse(LocalHost), port);
            clientSocket.Bind(LocalEndpoint);
            Console.WriteLine("Bind成功");
        }
        public void Connect(IPEndPoint RemoteEndPoint)
        {
            //IPEndPoint RemoteEndPoint = new IPEndPoint(IPAddress.Parse(LocalHost), port);
            clientSocket.Connect(RemoteEndPoint);
            Console.WriteLine("连接成功");
        }
        public void Send(string msg)
        {
            byte[] sendBytes = Encoding.ASCII.GetBytes(msg);
            clientSocket.Send(sendBytes);
        }


        public string Receive()
        {
            //receive message
            string recStr = "";
            byte[] recBytes = new byte[4096];
            int bytes = clientSocket.Receive(recBytes, recBytes.Length, 0);
            recStr += Encoding.ASCII.GetString(recBytes, 0, bytes);
            return recStr;
        }
        public void Close()
        {
            clientSocket.Dispose();
            clientSocket.Close();
        }

    }
    class ArmPiControl
    {
        TCPClient Client;
        int port = 8947;
        string host = "10.0.0.1";//服务器端ip地址
        string LocalHost = "10.0.0.183";
        //string host = "127.0.0.1";//服务器端ip地址
        //string LocalHost = "127.0.0.22";
        int LocalPort = 0;
        int[] SetPos = { 800, 500, 500, 1500, 1500, 1500 };
        int[] PosUpLim = { 1200, 500, 500, 2000, 2000, 2500 };
        int[] PosBottomLim = { 800, 500, 500, 1000, 1250, 500 };
        int[] Pos;
        int[] ServoList = { 0, 3, 4, 5 };
        public bool Connected { get
            {
                if (Client != null)
                    return Client.Connected;
                else
                    return false;
            } }
        public ArmPiControl()
        {


        }
        public bool Connect()
        {
            Client = new TCPClient();
            Client.Bind(LocalHost, LocalPort);
            IPEndPoint RemoteEndPoint = new IPEndPoint(IPAddress.Parse(host), port);
            Client.Connect(RemoteEndPoint);
            Thread.CurrentThread.Join(100);
            if (Client.Connected) { Reset(); }
            return Client.Connected;
        }
        public void Reset()
        {
            Client.Send("I001-500-6-1-800-2-500-3-500-4-1500-5-1500-6-1500\r\n");
            Pos = SetPos;
        }
        public void MoveByCommand(int Command)
        {

        }
        public void Move(int servo, bool Direction)
        {
            switch (servo)
            {
                case 1:
                    Pos[0] = Direction ? PosUpLim[0] : PosBottomLim[0];   // 1200 : 800
                    break;
                case 2:
                    Pos[3] = Pos[3] + (Direction ? -500 : +500);
                    Pos[ServoList[servo - 1]] = Pos[ServoList[servo - 1]] > PosUpLim[ServoList[servo - 1]] ? PosUpLim[ServoList[servo - 1]] : Pos[ServoList[servo - 1]];
                    Pos[ServoList[servo - 1]] = Pos[ServoList[servo - 1]] < PosBottomLim[ServoList[servo - 1]] ? PosBottomLim[ServoList[servo - 1]] : Pos[ServoList[servo - 1]];
                    break;
                case 3:
                    Pos[4] = Pos[4] + (Direction ? +250 : -250);
                    Pos[ServoList[servo - 1]] = Pos[ServoList[servo - 1]] > PosUpLim[ServoList[servo - 1]] ? PosUpLim[ServoList[servo - 1]] : Pos[ServoList[servo - 1]];
                    Pos[ServoList[servo - 1]] = Pos[ServoList[servo - 1]] < PosBottomLim[ServoList[servo - 1]] ? PosBottomLim[ServoList[servo - 1]] : Pos[ServoList[servo - 1]];
                    break;
                case 4:
                    Pos[5] = Pos[5] + (Direction ? +250 : -250);
                    Pos[ServoList[servo - 1]] = Pos[ServoList[servo - 1]] > PosUpLim[ServoList[servo - 1]] ? PosUpLim[ServoList[servo - 1]] : Pos[ServoList[servo - 1]];
                    Pos[ServoList[servo - 1]] = Pos[ServoList[servo - 1]] < PosBottomLim[ServoList[servo - 1]] ? PosBottomLim[ServoList[servo - 1]] : Pos[ServoList[servo - 1]];
                    break;
            }
            string Msg = "I001 - 500 - 6";
            for (int iServo = 0; iServo < Pos.Length; iServo++)
            {
                Msg += "-" + (iServo + 1).ToString() + "-" + Pos[iServo].ToString();
            }
            Msg += "\r\n";
            Client.Send(Msg);
        }
        public void Close()
        {
            Client.Close();
        }
    }
    public class UdpBroadcaster
    {
        IPAddress AvailableLocalAddress;
        Socket sock;//for broadcast
        IPEndPoint RemoteEndPoint;
        Byte[] MsgIn;
        public UdpBroadcaster(IPAddress RemoteHost, int RemotePort)
        {
            RemoteEndPoint = new IPEndPoint(RemoteHost, RemotePort);  // 将address和port绑在一起
            MsgIn = new byte[10];
        }
        public void BroadcasterSetup(IPAddress address, int LocalPort)
        {
            // if wish auto set, use IPAddress.Any as address and 0 as port

            sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sock.Bind(new IPEndPoint(address, LocalPort));
            sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
        }
        public void SendMsg(byte[] msg)
        {
            //byte[] data = Encoding.ASCII.GetBytes(msg);            
            sock.SendTo(msg, RemoteEndPoint);

        }
        public void Rec()
        {
            sock.Receive(MsgIn);
        }
        public void CloseBroadcaster()
        {
            sock.Close();
        }
        /**
         * 对所有获取到的IP地址进行广播
         **/
        public IPAddress UDPSearch(int LocalPort)
        {

            //RemoteEndPoint = new IPEndPoint(IPAddress.Broadcast, RemotePort);            
            List<IPAddress> Ipv4List = GetIpv4List();//get ipv4 list; function defined below;
            Console.WriteLine("搜索到的IP地址数量" + Ipv4List.Count);
            for (int iLocalAdd = 0; iLocalAdd < Ipv4List.Count; iLocalAdd++)
            {
                //udp settings
                Console.WriteLine("IP：" + Ipv4List[iLocalAdd].ToString());
                sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sock.Bind(new IPEndPoint(Ipv4List[iLocalAdd], LocalPort));
                sock.ReceiveTimeout = 100;
                // Send Local ip through different local network
                // and watch if there's any response
                byte[] data = Encoding.ASCII.GetBytes(Ipv4List[iLocalAdd].ToString());
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                sock.SendTo(data, RemoteEndPoint);
                byte[] RecieveData = new byte[4];
                try
                {
                    int len = sock.Receive(RecieveData);
                    if (len == 4)
                    {
                        AvailableLocalAddress = Ipv4List[iLocalAdd];//if responesed , return the corresponding local address;
                        sock.Close();
                        break;

                    }
                }
                catch
                {
                    sock.Close();
                }

            }
            return AvailableLocalAddress;
        }

        public List<IPAddress> GetIpv4List()
        {
            List<IPAddress> Ipv4List = new List<IPAddress>();
            string hostname = Dns.GetHostName();
            //IPHostEntry localhost = Dns.GetHostByName(hostname);    //方法已过期，可以获取IPv4的地址
            IPHostEntry localhost = Dns.GetHostEntry(hostname);   //获取IPv6地址
            for (int iAddress = 0; iAddress < localhost.AddressList.Length; iAddress++)
            {
                if (localhost.AddressList[iAddress].AddressFamily == AddressFamily.InterNetwork)
                {
                    Ipv4List.Add(localhost.AddressList[iAddress]);
                }
            }
            return Ipv4List;
            //IPAddress localaddr = localhost.AddressList[0];
            //string LocalIp = localaddr.ToString();
        }
    }

    public class AirCC
    {
        // Air control center
        UdpBroadcaster AirUdp;
        int PortVehicle = 8889;
        IPAddress HostVehicle = IPAddress.Parse("192.168.10.1");
        IPAddress Host;
        List<IPAddress> LocalHostList;
        public AirCC()
        {
            AirUdp = new UdpBroadcaster(HostVehicle, PortVehicle);
            LocalHostList = AirUdp.GetIpv4List();
            foreach (IPAddress host in LocalHostList)
            {
                if (host.ToString().Split('.')[2] == "10")
                {
                    Host = host;
                }
            }

            AirUdp.BroadcasterSetup(Host, 0);
            EnterCommandMod();
        }
        public void Close()
        {
            AirUdp.CloseBroadcaster();
        }

        void SendCmd(string Cmd)
        {
            byte[] Msg = Encoding.Default.GetBytes(Cmd);
            AirUdp.SendMsg(Msg);
        }
        public void EnterCommandMod()
        {
            SendCmd("command");
        }
        public void TakeOff()
        {
            SendCmd("takeoff");
        }
        public void Land()
        {
            SendCmd("land");
        }
        public void Left(int distance)
        {
            SendCmd("left " + distance.ToString());
        }
        public void Right(int distance)
        {
            SendCmd("right " + distance.ToString());
        }
        public void Forward(int distance)
        {
            SendCmd("forward " + distance.ToString());
        }
        public void Back(int distance)
        {
            SendCmd("back " + distance.ToString());
        }
    }

    class DobotPiControl
    {
        // By Pengxiao
        public bool isConnectted = false;
        public JogCmd currentCmd;
        public byte isJoint = (byte)1;    //  0: 笛卡尔坐标系; 1: 关节坐标系 
        System.Timers.Timer PiStopTimer;
        int testNum = 0;

        // 定时器初始化应该放在哪儿比较好
        private void InitTimer()
        {
            int interval = 500;
            PiStopTimer = new System.Timers.Timer(interval);
            PiStopTimer.AutoReset = false;
            PiStopTimer.Enabled = true;
            PiStopTimer.Elapsed += new System.Timers.ElapsedEventHandler(PiStop);
        }

        private void PiStop(object sender, System.Timers.ElapsedEventArgs e)
        {
            Move(0);
            //testNum++;
        }

        public void StartDobot()   //test privat and public
        {
            StringBuilder fwType = new StringBuilder(60);
            StringBuilder version = new StringBuilder(60);
            int ret = DobotDll.ConnectDobot("", 115200, fwType, version);

            if (ret != (int)DobotConnect.DobotConnect_NoError)
            {
                // 错误链接
            }
            isConnectted = true;
            DobotDll.SetCmdTimeout(3000);

            // Get name
            string deviceName = "Dobot Magician";
            DobotDll.SetDeviceName(deviceName);

            StringBuilder deviceSN = new StringBuilder(64);
            DobotDll.GetDeviceName(deviceSN, 64);

            SetParam();
            //InitTimer();   // 在这初始化 会使运动时长不定

            // 没有增加警报测试
        }

        public void Disconnect()
        {
            DobotDll.DisconnectDobot();
            isConnectted = false;
        }

        private void SetParam()
        {
            UInt64 cmdIndex = 0;
            JOGJointParams jsParam;
            jsParam.velocity = new float[] { 100, 100, 100, 100 };
            jsParam.acceleration = new float[] { 200, 200, 200, 200 };
            DobotDll.SetJOGJointParams(ref jsParam, false, ref cmdIndex);

            JOGCommonParams jdParam;
            jdParam.velocityRatio = 30;
            jdParam.accelerationRatio = 100;
            DobotDll.SetJOGCommonParams(ref jdParam, false, ref cmdIndex);

            //暂时只有JOG运动模式

            //EndTypeParams endType;
            //endType.xBias = 59.7f;
            //endType.yBias = 0f;
            //endType.zBias = 0f;
            //DobotDll.SetEndEffectorParams(ref endType);
        }

        public void Move(int servo)
        {
            if (!isConnectted)
                return;

            UInt64 cmdIndex = 0;
            
            switch (servo)
            {
                case 0:
                    currentCmd.isJoint = isJoint;
                    currentCmd.cmd = (byte)JogCmdType.JogIdle;
                    DobotDll.SetJOGCmd(ref currentCmd, false, ref cmdIndex);
                    break;
                case 1:
                    DobotDll.SetEndEffectorSuctionCup(true, false, false, ref cmdIndex);
                    break;
                case 2:
                    DobotDll.SetEndEffectorSuctionCup(true, true, false, ref cmdIndex);
                    break;
                case 3:
                    currentCmd.isJoint = isJoint;
                    currentCmd.cmd = (byte)JogCmdType.JogCNPressed;   
                    DobotDll.SetJOGCmd(ref currentCmd, false, ref cmdIndex);
                    InitTimer();
                    break;
                case 4:
                    currentCmd.isJoint = isJoint;
                    currentCmd.cmd = (byte)JogCmdType.JogCPPressed;
                    DobotDll.SetJOGCmd(ref currentCmd, false, ref cmdIndex);
                    InitTimer();
                    break;
                case 5:
                    currentCmd.isJoint = isJoint;
                    currentCmd.cmd = (byte)JogCmdType.JogBNPressed;
                    DobotDll.SetJOGCmd(ref currentCmd, false, ref cmdIndex);
                    InitTimer();
                    break;
                case 6:
                    currentCmd.isJoint = isJoint;
                    currentCmd.cmd = (byte)JogCmdType.JogBPPressed;
                    DobotDll.SetJOGCmd(ref currentCmd, false, ref cmdIndex);
                    InitTimer();
                    break;
                case 7:
                    currentCmd.isJoint = isJoint;
                    currentCmd.cmd = (byte)JogCmdType.JogANPressed;
                    DobotDll.SetJOGCmd(ref currentCmd, false, ref cmdIndex);
                    InitTimer();
                    break;
                case 8:
                    currentCmd.isJoint = isJoint;
                    currentCmd.cmd = (byte)JogCmdType.JogAPPressed;
                    DobotDll.SetJOGCmd(ref currentCmd, false, ref cmdIndex);
                    InitTimer();
                    break;
            }
        }

    }


}
