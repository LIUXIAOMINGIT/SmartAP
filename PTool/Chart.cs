using System;
using System.Collections.Generic;
using System.Configuration;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Data;
using System.Drawing;
using System.Threading;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using ClosedXML.Excel;
using CCWin;
using ApplicationClient;
using Misc = ComunicationProtocol.Misc;
using SerialDevice;


namespace PTool
{
    public partial class Chart : UserControl
    {
        private readonly int BAUDRATE = 9600;
        private const string VOL = "电压值";
        private const int LEFTBORDEROFFSET = 30;
        private const int RIGHTBORDEROFFSET = 20;
        private const int BOTTOMBORDEROFFSET = 30;     //X坐标与下边距，一般是绘图区域的一半高度
        private const int TOPBOTTOMFFSET = 5;      //坐标上下边距
        private const int CIRCLEDIAMETER = 5;      //曲线图上的圆点直径0
        private const int TRYCOUNTSAMPLINGTIMEOUT = 5;      //采样超时次数为5.超时5次就停止 
        private Graphics m_gh = null;
        private System.Drawing.Rectangle m_Rect;
        private Pen m_WaveLinePen = new Pen(Color.FromArgb(19, 113, 185));
        private SolidBrush m_WaveLineBrush = new SolidBrush(Color.Black);
        private float m_XCoordinateMaxValue = 2f;
        private int m_YCoordinateMaxValue = 100;
        private int m_XSectionCount = 4;
        private int m_YSectionCount = 5;
        private float m_CoordinateIntervalX = 0;  //X轴上的区间实际长度，单位为像素
        private float m_CoordinateIntervalY = 0;  //Y轴上的区间实际长度，单位为像素
        private float m_ValueInervalX = 0;  //X轴上的坐标值，根据实际放大倍数和量程决定
        private float m_ValueInervalY = 0;
        private List<SampleData> m_Ch1SampleDataList = new List<SampleData>();

        protected GlobalResponse m_ConnResponse = null;
        private MeterACD201 m_PTool = null;
        private MeterACD201 m_DetectPTool = null;
        private SmartAP m_SmartAP = null;               //用于正式通信
        private SmartAP m_DetectSmartAP = null;         //用于测试连接
        private System.Timers.Timer m_Ch1Timer = new System.Timers.Timer();
        private bool m_bStopClick = false;//是否人工点击停止
        private int m_SampleInterval = 400;//采样频率：毫秒

        private string m_PumpNo = string.Empty;//产品序号
        private string m_ToolingNo = string.Empty;//工装编号

        public delegate void DelegateSetWeightValue(float weight, bool isDetect);
        public delegate void DelegateSetPValue(float p);
        public delegate void DelegateEnableContols(bool bEnabled);
        public delegate void DelegateAlertMessageWhenComplete(string msg);
        public delegate void DelegateInputOpratorNumber(string number);

        /// <summary>
        /// 当启动或停止时通知主界面
        /// </summary>
        public event EventHandler<EventArgs> SamplingStartOrStop;

        /// <summary>
        /// 当双道泵，测量结束后通知主界面，把数据传入
        /// </summary>
        public event EventHandler<DoublePumpDataArgs> OnSamplingComplete;
        public event EventHandler<OpratorNumberArgs> OpratorNumberInput;

        private int mCurrentSamplingIndex = 0;//当前采样点

        private System.Timers.Timer mSamplingPointTimer = new System.Timers.Timer();
        private bool mSamplingPointStart = false;//是否开始采样了
        private int mSamplingCount = 0;//静止状态下采样了几次，三次即可
        private List<SampleData> mSamplingPointList = new List<SampleData>();//要采样9个点的数据

        

        /// <summary>
        /// 采样间隔
        /// </summary>
        public int SampleInterval
        {
            get { return m_SampleInterval; }
            set { m_SampleInterval = value; }
        }

      

        /// <summary>
        /// 产品序号
        /// </summary>
        public string PumpNo
        {
            get { return m_PumpNo; }
            set { m_PumpNo = value; }
        }

        /// <summary>
        /// 工装编号
        /// </summary>
        public string ToolingNo
        {
            get { return m_ToolingNo; }
            set { m_ToolingNo = value; }
        }

        public Chart()
        {
            InitializeComponent();
            m_gh = WavelinePanel.CreateGraphics();
            m_Rect = WavelinePanel.ClientRectangle;
            m_PTool = new MeterACD201();
            m_PTool.DeviceDataReceived += OnACD201DataReceived;
            m_DetectPTool = new MeterACD201();
            m_DetectPTool.DeviceDataReceived += OnACD201DetectPortNumberRecerived;
            m_SmartAP = new SmartAP();
            m_SmartAP.DeviceDataReceived += OnSmartReceived;
        }

        private void Chart_Load(object sender, EventArgs e)
        {
            cbPumpPort.Items.AddRange(SerialPort.GetPortNames());
            cbToolingPort.Items.AddRange(SerialPort.GetPortNames());
            m_Ch1Timer.Interval = m_SampleInterval;
            m_Ch1Timer.Elapsed += OnCh1Timer_Elapsed;

            mSamplingPointTimer.Interval = 500;
            mSamplingPointTimer.Elapsed += OnSamplingPointTimer_Elapsed; 
        }


      
        #region 时钟

        /// <summary>
        /// 采样时钟，画图数据
        /// </summary>
        private void StartCh1Timer()
        {
            StopCh1Timer();
            m_Ch1Timer.Start();
        }

        private void StopCh1Timer()
        {
            m_Ch1Timer.Stop();
        }

        /// <summary>
        /// 采样画图时钟
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCh1Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (m_bStopClick==false && m_SmartAP != null && m_SmartAP.IsOpen())
            {
                m_SmartAP.SendCmdGetVoltage();
            }
        }

        /// <summary>
        /// 泵停止后，起一个新时钟。去读静止状态的值
        /// </summary>
        private void StartSamplingPointTimer()
        {
            //时钟开启后5秒钟才能
            StopSamplingPointTimer();
            mSamplingPointStart = true;
            mSamplingPointTimer.Start();
        }

        private void StopSamplingPointTimer()
        {
            mSamplingCount = 0;
            mSamplingPointStart = false;
            mSamplingPointTimer.Stop();
        }
        private void OnSamplingPointTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if(mSamplingCount >= 3)
            {
                StopSamplingPointTimer();
                StartCh1Timer();
                m_SmartAP.SendCmdStartAP();
                return;
            }

            if(mSamplingPointStart)
            {
                m_SmartAP.SendCmdGetVoltage();
                mSamplingCount++;
            }
        }

        #endregion


        /// <summary>
        ///仅检测串口使用
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnACD201DetectPortNumberRecerived(object sender, EventArgs e)
        {
            PressureMeterArgs args = e as PressureMeterArgs;
            SetWeightValue(args.PressureValue, true);
        }


        /// <summary>
        /// Smart AP检测串口和接收数据使用
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSmartReceived(object sender, EventArgs e)
        {
            if (e is CmdGetVoltage)
            {
                CmdGetVoltage args = e as CmdGetVoltage;
                SetPValue(args.GetVoltage());

                //采样数据，输出
                if (mSamplingPointStart)
                {
                    //System.Diagnostics.Debug.Write("泵的P值：");
                    //System.Diagnostics.Debug.WriteLine((paras.pressureVoltage * 100).ToString("F0"));
                    //静止后采最后一次数据
                    if (mSamplingCount == 2)
                    {
                        lock (mSamplingPointList)
                        {
                            mSamplingPointList.Add(new SampleData(DateTime.Now, args.GetVoltage(), -1000f));
                        }
                    }
                }

                //是否开始采样了,开始按钮不可点击
                if (picStart.Enabled == false)
                {
                    lock (m_Ch1SampleDataList)
                    {
                        m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, args.GetVoltage(), -1000f));
                    }
                    //去读压力表的值
                    m_PTool.Get();
                }
                return;
            }

            //开始启动泵或停止泵命令返回
            if(e is CmdStartStopAP)
            {
                CmdStartStopAP args = e as CmdStartStopAP;
                m_SmartAP.SendCmdGetStatus();
                return;
            }

            //开始启动泵或停止泵命令返回
            if (e is CmdGetStatus)
            {
                CmdGetStatus args = e as CmdGetStatus;
                if(args.GetStatus() == 1)
                {
                    StartCh1Timer();
                }
                else
                {
                    if (mSamplingPointList.Count < SmartMainForm.SamplingPoints.Count)
                    {
                        StopCh1Timer();
                        //开始读静止状态下的压力表个泵的值
                        StartSamplingPointTimer();
                    }
                    else
                    {
                        //采样完成，所有设备工装都要停止
                    }
                }
                return;
            }

        }

        /// <summary>
        /// 正式的压力表值
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnACD201DataReceived(object sender, EventArgs e)
        {
            PressureMeterArgs args = e as PressureMeterArgs;

            //采样数据，输出
            if (mSamplingPointStart)
            {
                //先取最后一次采样数据
                if (mSamplingCount == 2)
                {
                    lock (mSamplingPointList)
                    {
                        if (mSamplingPointList.Count > 0)
                        {
                            SampleData sp = mSamplingPointList[mSamplingPointList.Count - 1];
                            sp.m_ACDValue = args.PressureValue;
                        }
                    }
                }
            }

            lock (m_Ch1SampleDataList)
            {
                if (m_Ch1SampleDataList.Count > 0)
                {
                    SampleData sp = m_Ch1SampleDataList[m_Ch1SampleDataList.Count - 1];
                    sp.m_ACDValue = args.PressureValue;
                }
            }

            if (mCurrentSamplingIndex < SmartMainForm.SamplingPoints.Count && mCurrentSamplingIndex >= 0)
            {
                if (args.PressureValue >= SmartMainForm.SamplingPoints[mCurrentSamplingIndex]
                    || Math.Abs(args.PressureValue - SmartMainForm.SamplingPoints[mCurrentSamplingIndex])<= SmartMainForm.m_SamplingError
                    )
                {
                    StopCh1Timer();
                    m_SmartAP.SendCmdStopAP();
                    mCurrentSamplingIndex++;
                }
            }

            SetWeightValue(args.PressureValue, false);
            DrawSingleAccuracyMap();
            
            //采集的压力表数据是负数
            var min = 0f;
            int pointsCount = SmartMainForm.SamplingPoints.Count;
            if (pointsCount > 0)
                min = SmartMainForm.SamplingPoints[pointsCount - 1];
            if (min > args.PressureValue)
            {
                Complete();
                AlertMessageWhenComplete(string.Format("压力值已超出最大范围{0},调试结束!", min));
            }
        }

        private void AlertMessageWhenComplete(string msg)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new DelegateAlertMessageWhenComplete(AlertMessageWhenComplete), new object[] { msg });
                return;
            }
            MessageBox.Show(msg);
        }

        private void SetPValue(float p)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new DelegateSetPValue(SetPValue), new object[] { p });
                return;
            }
            lbPValue.Text = p.ToString("F0");
            m_SmartAP.Close();
        }

        private void SetWeightValue(float weight, bool isDetect = false)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new DelegateSetWeightValue(SetWeightValue), new object[] { weight, isDetect });
            }
            lbWeight.Text = weight.ToString("F3");
            if (isDetect)
                m_DetectPTool.Close();
        }

        /// <summary>
        /// 调试结束，保存相关数据，停止泵，停止时钟
        /// </summary>
        private void Complete()
        {
            StopSamplingPointTimer();
            StopCh1Timer();
            if (m_PTool != null && m_PTool.IsOpen())
                m_PTool.Close();

            if (m_SmartAP != null)
            {
                if (m_SmartAP.IsOpen())
                {
                    m_SmartAP.SendCmdStopAP();
                    Thread.Sleep(500);
                    CalcuatePolyParameters();
                    m_SmartAP.Close();
                }
            }
           

            //string fileName = m_PumpNo;
            //if (m_LocalPid == PumpID.GrasebyF6 || m_LocalPid == PumpID.WZS50F6)
            //    fileName = string.Format("{0}{1}道{2}{3}", m_LocalPid.ToString(), m_Channel, m_PumpNo, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss"));
            //else
            //    fileName = string.Format("{0}{1}{2}", m_LocalPid.ToString(), m_PumpNo, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss"));
            //string path = Path.GetDirectoryName(Assembly.GetAssembly(typeof(SmartMainForm)).Location) + "\\压力调试数据";
            //if (!System.IO.Directory.Exists(path))
            //    System.IO.Directory.CreateDirectory(path);
            //string saveFileName = path + "\\" + fileName + ".xlsx";
            //Export(m_Channel, saveFileName);
            EnableContols(true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xSectionCount">X轴的坐标数量</param>
        /// <param name="ySectionCount">Y轴坐标数量</param>
        private void DrawSingleAccuracyMap(int xSectionCount = 4, int ySectionCount = 5)
        {
            if (m_Ch1SampleDataList.Count <= 1)
                return;
            Rectangle rect = m_Rect;
            Font xValuefont = new Font("宋体", 7);
            Font fontTitle = new Font("宋体", 8);
            //X轴原点
            PointF xOriginalPoint = new PointF((float)rect.Left + LEFTBORDEROFFSET, rect.Bottom - BOTTOMBORDEROFFSET);
            //X轴终点
            PointF xEndPoint = new PointF((float)rect.Right - RIGHTBORDEROFFSET, rect.Bottom - BOTTOMBORDEROFFSET);
            //Y轴最下面的点位置
            PointF yOriginalPoint = xOriginalPoint;
            //Y轴终点（由下向上）
            PointF yEndPoint = new PointF((float)rect.Left + LEFTBORDEROFFSET, (float)rect.Top + TOPBOTTOMFFSET);
            float y0 = 0, y1 = 0, x0 = 0, x1 = 0;
            int i = m_Ch1SampleDataList.Count - 1;
            //ACD 201压力表作为Y轴
            y0 = xOriginalPoint.Y - ((yOriginalPoint.Y - yEndPoint.Y) / ySectionCount * (-(m_Ch1SampleDataList[i - 1].m_ACDValue / -m_ValueInervalY)));
            y1 = xOriginalPoint.Y - ((yOriginalPoint.Y - yEndPoint.Y) / ySectionCount * (-(m_Ch1SampleDataList[i].m_ACDValue / -m_ValueInervalY)));
          
            //Smart Ap 作为X轴
            x0 = (xEndPoint.X - xOriginalPoint.X) / xSectionCount * m_Ch1SampleDataList[i - 1].m_ApValue + xOriginalPoint.X;
            x1 = (xEndPoint.X - xOriginalPoint.X) / xSectionCount * m_Ch1SampleDataList[i].m_ApValue + xOriginalPoint.X;
            m_gh.DrawLine(m_WaveLinePen, new PointF(x0, y0), new PointF(x1, y1));
        }

        private void WavelinePanel_Paint(object sender, PaintEventArgs e)
        {
            DrawCoordinate(m_XCoordinateMaxValue, m_XSectionCount, m_YCoordinateMaxValue, m_YSectionCount);
        }

        /// <summary>
        /// 画坐标轴
        /// </summary>
        /// <param name="xMax">X坐标最大值</param>
        /// <param name="xSectionCount">X坐标分成几段</param>
        /// <param name="yMax">Y坐标最大值</param>
        /// <param name="ySectionCount">Y坐标分成几段</param>
        private void DrawCoordinate(float xMax, int xSectionCount, int yMax, int ySectionCount)
        {
            try
            {
                Rectangle rect = m_Rect;
                Font xValuefont = new Font("宋体", 7);
                Font fontTitle = new Font("宋体", 8);
                Font fontChartDes = new Font("Noto Sans CJK SC Bold", 12);
                //画X轴
                PointF originalpoint = new PointF((float)rect.Left + LEFTBORDEROFFSET, rect.Bottom - BOTTOMBORDEROFFSET);
                PointF xEndPoint = new PointF((float)rect.Right - RIGHTBORDEROFFSET, rect.Bottom - BOTTOMBORDEROFFSET);
                m_gh.DrawLine(m_WaveLinePen, originalpoint, xEndPoint);
                //画X坐标箭头
                PointF arrowpointUp = new PointF(xEndPoint.X - 12, xEndPoint.Y - 6);
                PointF arrowpointDwon = new PointF(xEndPoint.X - 12, xEndPoint.Y + 6);
                m_gh.DrawLine(m_WaveLinePen, arrowpointUp, xEndPoint);
                m_gh.DrawLine(m_WaveLinePen, arrowpointDwon, xEndPoint);

                //画X轴坐标,SECTIONCOUNT个点
                float intervalX = (xEndPoint.X - originalpoint.X) / xSectionCount;
                m_CoordinateIntervalX = intervalX;
                float lineSegmentHeight = 8f;
                for (int i = 1; i <= xSectionCount - 1; i++)
                {
                    m_gh.DrawLine(m_WaveLinePen, new PointF(originalpoint.X + intervalX * i, originalpoint.Y), new PointF(originalpoint.X + intervalX * i, originalpoint.Y - lineSegmentHeight));
                }
                //画X坐标值
                float xValueInerval = xMax / xSectionCount;
                m_ValueInervalX = xValueInerval;
                for (int i = 0; i <= xSectionCount; i++)
                {
                    if (i == 0)
                    {
                        // m_gh.DrawString("0", xValuefont, Brushes.Black, new PointF(originalpoint.X - 5, originalpoint.Y + 5));
                    }
                    else
                        m_gh.DrawString((i * xValueInerval).ToString(), xValuefont, Brushes.Black, new PointF(originalpoint.X + intervalX * i - 8, originalpoint.Y + 5));
                }
                //画Y轴
                PointF yEndPoint = new PointF((float)rect.Left + LEFTBORDEROFFSET, (float)rect.Top + TOPBOTTOMFFSET);
                //写图形描述字符
                m_gh.DrawString("传感电压vs气压关系图", fontChartDes, m_WaveLineBrush, new PointF(yEndPoint.X + 285, yEndPoint.Y));
                //y轴的起始点，从底部往上
                PointF yOriginalPoint = originalpoint;//new PointF((float)rect.Left + LEFTBORDEROFFSET, rect.Bottom - TOPBOTTOMFFSET);
                m_gh.DrawLine(m_WaveLinePen, yOriginalPoint, yEndPoint);
                //画Y坐标箭头
                PointF arrowpointLeft = new PointF(yEndPoint.X - 6, yEndPoint.Y + 12);
                PointF arrowpointRight = new PointF(yEndPoint.X + 6, yEndPoint.Y + 12);
                m_gh.DrawLine(m_WaveLinePen, arrowpointLeft, yEndPoint);
                m_gh.DrawLine(m_WaveLinePen, arrowpointRight, yEndPoint);
                //画Y坐标文字
                //m_gh.DrawString("压力值(V)", fontTitle, Brushes.Black, new PointF(yEndPoint.X + 10, yEndPoint.Y));
                //画Y轴坐标,每个区间的实际坐标长度
                float intervalY = Math.Abs(yEndPoint.Y - yOriginalPoint.Y) / ySectionCount;
                m_CoordinateIntervalY = intervalY;
                for (int i = 0; i < ySectionCount; i++)
                {
                    m_gh.DrawLine(m_WaveLinePen, new PointF(yOriginalPoint.X, yOriginalPoint.Y - intervalY * i), new PointF(yOriginalPoint.X + lineSegmentHeight, yOriginalPoint.Y - intervalY * i));
                }
                float yValueInerval = (float)yMax / ySectionCount;
                m_ValueInervalY = yValueInerval;//Y轴上的坐标值，根据实际放大倍数和量程决定
                for (int i = 0; i <= ySectionCount; i++)
                {
                    m_gh.DrawString((-i* yValueInerval).ToString(), xValuefont, Brushes.Black, new PointF(yOriginalPoint.X - 24, yOriginalPoint.Y - intervalY * i - 6));
                }
                //画legend
                m_gh.DrawString(VOL, fontTitle, m_WaveLineBrush, new PointF(xEndPoint.X - 80, 10));
                SizeF fontSize = m_gh.MeasureString(VOL, fontTitle);

                m_gh.DrawLine(m_WaveLinePen, new PointF(xEndPoint.X - 100, 10 + fontSize.Height / 2), new PointF(xEndPoint.X - 80, 10 + fontSize.Height / 2));
            }
            catch (Exception e)
            {
                MessageBox.Show("DrawHomeostasisMap Error:" + e.Message);
            }
        }

        /// <summary>
        /// 界面移动或变化时需要重绘所有点
        /// </summary>
        /// <param name="xSectionCount"></param>
        /// <param name="ySectionCount"></param>
        private void DrawAccuracyMap(int xSectionCount = 4, int ySectionCount = 5)
        {
            if (m_Ch1SampleDataList.Count <= 1)
                return;
            Rectangle rect = m_Rect;
            Font xValuefont = new Font("宋体", 7);
            Font fontTitle = new Font("宋体", 8);
            //画X轴
            //X轴原点
            PointF xOriginalPoint = new PointF((float)rect.Left + LEFTBORDEROFFSET, rect.Bottom - BOTTOMBORDEROFFSET);
            //X轴终点
            PointF xEndPoint = new PointF((float)rect.Right - RIGHTBORDEROFFSET, rect.Bottom - BOTTOMBORDEROFFSET);
            //Y轴最下面的点位置
            PointF yOriginalPoint = xOriginalPoint;
            //Y轴终点（由下向上）
            PointF yEndPoint = new PointF((float)rect.Left + LEFTBORDEROFFSET, (float)rect.Top + TOPBOTTOMFFSET);
            string strMsg = string.Empty;
            float y0 = 0, y1 = 0, x0 = 0, x1 = 0;
            int count = m_Ch1SampleDataList.Count;
            for (int iLoop = 1; iLoop < count; iLoop++)
            {
                y0 = xOriginalPoint.Y - ((yOriginalPoint.Y - yEndPoint.Y) / ySectionCount * ((m_Ch1SampleDataList[iLoop - 1].m_ACDValue / -m_ValueInervalY)));
                y1 = xOriginalPoint.Y - ((yOriginalPoint.Y - yEndPoint.Y) / ySectionCount * ((m_Ch1SampleDataList[iLoop].m_ACDValue / -m_ValueInervalY)));
                x0 = (xEndPoint.X - xOriginalPoint.X) / xSectionCount * m_Ch1SampleDataList[iLoop - 1].m_ApValue/ m_ValueInervalX + xOriginalPoint.X;
                x1 = (xEndPoint.X - xOriginalPoint.X) / xSectionCount * m_Ch1SampleDataList[iLoop].m_ApValue/ m_ValueInervalX + xOriginalPoint.X;
                m_gh.DrawLine(m_WaveLinePen, new PointF(x0, y0), new PointF(x1, y1));
            }
        }

        /// <summary>
        /// 当不可用时，将按钮图标变灰
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Chart_EnabledChanged(object sender, EventArgs e)
        {
            //if (this.Enabled)
            //{
            //    picStart.Image = global::PTool.Properties.Resources.icon_start_Blue;
            //    picStop.Image = global::PTool.Properties.Resources.icon_stop_blue;
            //    picDetail.Image = global::PTool.Properties.Resources.icon_tablelist_blue;
            //    if (m_Channel == 2)
            //    {
            //        picChannel.Image = global::PTool.Properties.Resources.icon_2_blue;
            //    }
            //}
            //else
            //{
            //    picStart.Image = global::PTool.Properties.Resources.icon_start_gray;
            //    picStop.Image = global::PTool.Properties.Resources.icon_stop_gray;
            //    picDetail.Image = global::PTool.Properties.Resources.icon_tablelist_gray;
            //    if (m_Channel == 2)
            //    {
            //        picChannel.Image = global::PTool.Properties.Resources.icon_2_gray;
            //    }
            //}
        }

        /// <summary>
        /// 串口选择时发送命令
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbToolingPort_SelectedIndexChanged(object sender, EventArgs e)
        {
            lbWeight.Text = "-----";
            if (m_DetectPTool == null)
                m_DetectPTool = new MeterACD201();
            if (m_DetectPTool.IsOpen())
                m_DetectPTool.Close();
            m_DetectPTool.Init(cbToolingPort.Items[cbToolingPort.SelectedIndex].ToString());
            m_DetectPTool.Open();
            Thread.Sleep(500);
            m_DetectPTool.Get();
        }

        private void cbPumpPort_SelectedIndexChanged(object sender, EventArgs e)
        {
            lbPValue.Text = "-----";
            if (m_SmartAP == null)
                m_SmartAP = new SmartAP();
            if (m_SmartAP.IsOpen())
                m_SmartAP.Close();
            m_SmartAP.Init(cbPumpPort.Items[cbPumpPort.SelectedIndex].ToString());
            m_SmartAP.Open();
            m_SmartAP.Get();
        }

        /// <summary>
        /// 速率不能输入非法值
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRateKeyPress(object sender, KeyPressEventArgs e)
        {
            TextBox txt = sender as TextBox;
            if (char.IsNumber(e.KeyChar) || e.KeyChar == (char)Keys.Back)
            {
                e.Handled = false;                         //让操作生效
                if (txt.Text.Length == 0)
                {
                    if (e.KeyChar == '0')
                        e.Handled = true;                  //让操作失效，第一个字符不能输入0
                }
                else if (txt.Text.Length >= 3)
                {

                    if (e.KeyChar == (char)Keys.Back)
                        e.Handled = false;             //让操作生效
                    else
                        e.Handled = true;              //让操作失效，如果第一个字符是2以上，不能输入其他字符
                }
                else
                {
                    e.Handled = false;                 //让操作生效
                }
            }
            else
            {
                e.Handled = true;
            }
        }

        private void EnableContols(bool bEnabled = true)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new DelegateEnableContols(EnableContols), new object[] { bEnabled });
                return;
            }
            cbToolingPort.Enabled = bEnabled;
            cbPumpPort.Enabled = bEnabled;
            tbToolNo.Enabled = bEnabled;
            tbToolNo.Enabled = bEnabled;
            picStart.Enabled = bEnabled;
            picStop.Enabled = !bEnabled;

            if (!bEnabled)
            {
                picStart.Image = global::PTool.Properties.Resources.icon_start_gray;
                picStop.Image = global::PTool.Properties.Resources.icon_stop_blue;
            }
            else
            {
                picStart.Image = global::PTool.Properties.Resources.icon_start_Blue;
                picStop.Image = global::PTool.Properties.Resources.icon_stop_gray;
            }
            if (SamplingStartOrStop != null)
            {
                SamplingStartOrStop(this, new StartOrStopArgs(bEnabled));
            }
        }

        /// <summary>
        /// 生成第三方公司需要的表格
        /// </summary>
        /// <param name="name"></param>
        /// <param name="caliParameters">已经生成好的数据，直接写到表格中</param>
        private void GenReport(string name, List<PressureCalibrationParameter> caliParameters, string nameBackup="")
        {
            //if (caliParameters == null || caliParameters.Count == 0)
            //    return;
            //string title = string.Empty;
            //if (m_LocalPid == PumpID.GrasebyF6 || m_LocalPid == PumpID.WZS50F6)
            //{
            //    title = string.Format("泵型号:{0}{1}道 产品序号:{2} 工装编号:{3}", m_LocalPid.ToString(), m_Channel, m_PumpNo, m_ToolingNo);
            //}
            //else
            //{
            //    title = string.Format("泵型号：{0} 产品序号:{1} 工装编号:{2}", m_LocalPid.ToString(), m_PumpNo, m_ToolingNo);
            //}
            //var wb = new XLWorkbook();
            //var ws = wb.Worksheets.Add("压力调试数据");
            //int columnIndex = 0;
            //ws.Cell(1, ++columnIndex).Value = "机器编号";
            //ws.Cell(1, ++columnIndex).Value = "机器型号";
            //ws.Cell(1, ++columnIndex).Value = "道数";
            //ws.Cell(1, ++columnIndex).Value = "工装编号";
            //ws.Cell(1, ++columnIndex).Value = "P0值";
            //ws.Cell(1, ++columnIndex).Value = "10mlL预设值";
            //ws.Cell(1, ++columnIndex).Value = "10mlC预设值";
            //ws.Cell(1, ++columnIndex).Value = "10mlH预设值";
            //ws.Cell(1, ++columnIndex).Value = "20mlL预设值";
            //ws.Cell(1, ++columnIndex).Value = "20mlC预设值";
            //ws.Cell(1, ++columnIndex).Value = "20mlH预设值";
            //ws.Cell(1, ++columnIndex).Value = "30mlL预设值";
            //ws.Cell(1, ++columnIndex).Value = "30mlC预设值";
            //ws.Cell(1, ++columnIndex).Value = "30mlH预设值";
            //ws.Cell(1, ++columnIndex).Value = "50mlL预设值";
            //ws.Cell(1, ++columnIndex).Value = "50mlC预设值";
            //ws.Cell(1, ++columnIndex).Value = "50mlH预设值";
            //ws.Cell(1, ++columnIndex).Value = "10ml低压";
            //ws.Cell(1, ++columnIndex).Value = "10ml中压";
            //ws.Cell(1, ++columnIndex).Value = "10ml高压";
            //ws.Cell(1, ++columnIndex).Value = "20ml低压";
            //ws.Cell(1, ++columnIndex).Value = "20ml中压";
            //ws.Cell(1, ++columnIndex).Value = "20ml高压";
            //ws.Cell(1, ++columnIndex).Value = "30ml低压";
            //ws.Cell(1, ++columnIndex).Value = "30ml中压";
            //ws.Cell(1, ++columnIndex).Value = "30ml高压";
            //ws.Cell(1, ++columnIndex).Value = "50ml低压";
            //ws.Cell(1, ++columnIndex).Value = "50ml中压";
            //ws.Cell(1, ++columnIndex).Value = "50ml高压";
            //ws.Cell(1, ++columnIndex).Value = "操作员";

            //columnIndex = 0;
            //ws.Cell(2, ++columnIndex).Value = m_PumpNo;
            //ws.Cell(2, ++columnIndex).Value = m_LocalPid.ToString();
            //ws.Cell(2, ++columnIndex).Value = m_Channel;
            //ws.Cell(2, ++columnIndex).Value = m_ToolingNo;
            //ws.Cell(2, ++columnIndex).Value = m_Ch1SampleDataList.Min(x => x.m_ApValue) * 100;
            //float mid = PressureManager.Instance().GetMidBySizeLevel(m_LocalPid, 10, Misc.OcclusionLevel.L);
            //ws.Cell(2, ++columnIndex).Value = mid == 0 ? "" : (mid).ToString("F2");
            //mid = PressureManager.Instance().GetMidBySizeLevel(m_LocalPid, 10, Misc.OcclusionLevel.C);
            //ws.Cell(2, ++columnIndex).Value = mid == 0 ? "" : (mid).ToString("F2");
            //mid = PressureManager.Instance().GetMidBySizeLevel(m_LocalPid, 10, Misc.OcclusionLevel.H);
            //ws.Cell(2, ++columnIndex).Value = mid == 0 ? "" : (mid).ToString("F2");
            //ws.Cell(2, ++columnIndex).Value = PressureManager.Instance().GetMidBySizeLevel(m_LocalPid, 20, Misc.OcclusionLevel.L);
            //ws.Cell(2, ++columnIndex).Value = PressureManager.Instance().GetMidBySizeLevel(m_LocalPid, 20, Misc.OcclusionLevel.C);
            //ws.Cell(2, ++columnIndex).Value = PressureManager.Instance().GetMidBySizeLevel(m_LocalPid, 20, Misc.OcclusionLevel.H);
            //ws.Cell(2, ++columnIndex).Value = PressureManager.Instance().GetMidBySizeLevel(m_LocalPid, 30, Misc.OcclusionLevel.L);
            //ws.Cell(2, ++columnIndex).Value = PressureManager.Instance().GetMidBySizeLevel(m_LocalPid, 30, Misc.OcclusionLevel.C);
            //ws.Cell(2, ++columnIndex).Value = PressureManager.Instance().GetMidBySizeLevel(m_LocalPid, 30, Misc.OcclusionLevel.H);
            //ws.Cell(2, ++columnIndex).Value = PressureManager.Instance().GetMidBySizeLevel(m_LocalPid, 50, Misc.OcclusionLevel.L);
            //ws.Cell(2, ++columnIndex).Value = PressureManager.Instance().GetMidBySizeLevel(m_LocalPid, 50, Misc.OcclusionLevel.C);
            //ws.Cell(2, ++columnIndex).Value = PressureManager.Instance().GetMidBySizeLevel(m_LocalPid, 50, Misc.OcclusionLevel.H);

            //PressureCalibrationParameter para = null;
            //para = caliParameters.Find((x) => { return x.m_SyringeSize == 10; });
            //if (para != null)
            //{
            //    columnIndex = 17;
            //    ws.Cell(2, ++columnIndex).Value = para.m_PressureL * 100;
            //    ws.Cell(2, ++columnIndex).Value = para.m_PressureC * 100;
            //    ws.Cell(2, ++columnIndex).Value = para.m_PressureH * 100;
            //}
            //para = caliParameters.Find((x) => { return x.m_SyringeSize == 20; });
            //if (para != null)
            //{
            //    columnIndex = 20;
            //    ws.Cell(2, ++columnIndex).Value = para.m_PressureL * 100;
            //    ws.Cell(2, ++columnIndex).Value = para.m_PressureC * 100;
            //    ws.Cell(2, ++columnIndex).Value = para.m_PressureH * 100;
            //}

            //para = caliParameters.Find((x) => { return x.m_SyringeSize == 30; });
            //if (para != null)
            //{
            //    columnIndex = 23;
            //    ws.Cell(2, ++columnIndex).Value = para.m_PressureL * 100;
            //    ws.Cell(2, ++columnIndex).Value = para.m_PressureC * 100;
            //    ws.Cell(2, ++columnIndex).Value = para.m_PressureH * 100;
            //}

            //para = caliParameters.Find((x) => { return x.m_SyringeSize == 50; });
            //if (para != null)
            //{
            //    columnIndex = 26;
            //    ws.Cell(2, ++columnIndex).Value = para.m_PressureL * 100;
            //    ws.Cell(2, ++columnIndex).Value = para.m_PressureC * 100;
            //    ws.Cell(2, ++columnIndex).Value = para.m_PressureH * 100;
            //}
            //ws.Cell(2, ++columnIndex).Value = tbToolNo.Text;

            //ws.Range(1, 1, 2, 1).SetDataType(XLCellValues.Text);
            //ws.Range(1, 4, 2, 4).SetDataType(XLCellValues.Text);
            //wb.SaveAs(name);
            //Thread.Sleep(1000);
            //File.Copy(name, nameBackup, true);
            //IntPtr handle = UserMessageHelper.FindWindow(null, "压力测试工具");
            //UserMessageHelper.SendMessage(handle, 0x1EE1, 0, 0);
        }

        private void CalcuatePressure(PumpID pid, List<SampleData> sampleDataList)
        {
//            if (sampleDataList == null || sampleDataList.Count == 0)
//                return;
          
//            List<PressureParameter> parameters = new List<PressureParameter>();
//            ProductPressure pp = PressureManager.Instance().GetPressureByProductID(pid);
//            if (pp == null)
//                return;
//            List<LevelPressure> lps = pp.GetLevelPressureList();
//            List<float> midWeights = new List<float>();
//            List<int> sizes = new List<int>();
//            if (pid == PumpID.WZS50F6 || pid == PumpID.GrasebyF6 || pid == PumpID.GrasebyF6_2 || pid == PumpID.WZS50F6_2)
//                sizes.Add(10);
//            sizes.Add(20);
//            sizes.Add(30);
//            sizes.Add(50);

//            foreach (var size in sizes)
//            {
//                foreach (Misc.OcclusionLevel level in Enum.GetValues(typeof(Misc.OcclusionLevel)))
//                {
//                    LevelPressure lp = lps.Find((x) => { return x.m_Level == level; });
//                    if (lp != null)
//                    {
//                        SizePressure sp = lp.Find(size);
//                        if (sp != null)
//                        {
//                            PressureParameter para = new PressureParameter(size, level, sp.m_Mid, 0);
//                            parameters.Add(para);
//                        }
//                    }
//                }
//            }
//            //找到相关的值后，需要写入到泵中
//            FindNearestPValue(ref parameters, sampleDataList);
//            //if (!IsValidEx(sampleDataList))
//            //{
//            //    sampleDataList.Clear();
//            //    MessageBox.Show("测量数据异常，请重试！");
//            //    return;
//            //}

//            float pValue = FindZeroPValue(sampleDataList);

//            if (pValue * 100 >= SmartMainForm.RangeMaxP || pValue * 100 <= SmartMainForm.RangeMinP)
//            {
//                Logger.Instance().ErrorFormat("P值超范围，请重试！P值={0},最小值={1},最大值={2}", pValue, SmartMainForm.RangeMinP, SmartMainForm.RangeMaxP);
//                sampleDataList.Clear();
//                MessageBox.Show("P值超范围，请重试！");
//                return;
//            }

//            WritePValue2Pump(pValue);
//            Logger.Instance().InfoFormat("测量结束，P值为{0}", pValue);

//            List<PressureCalibrationParameter> caliParameters = new List<PressureCalibrationParameter>();
//            foreach (var size in sizes)
//            {
//                PressureCalibrationParameter p = new PressureCalibrationParameter();
//                p.m_P0 = pValue;
//                p.m_SyringeSize = size;
//                List<PressureParameter> findobjs = parameters.FindAll((x) => { return x.m_SyringeSize == size; });
//                foreach (var obj in findobjs)
//                {
//                    switch (obj.m_Level)
//                    {
//                        case Misc.OcclusionLevel.L:
//                            p.m_PressureL = obj.m_Pressure;
//                            break;
//                        case Misc.OcclusionLevel.C:
//                            p.m_PressureC = obj.m_Pressure;
//                            break;
//                        case Misc.OcclusionLevel.H:
//                            p.m_PressureH = obj.m_Pressure;
//                            break;
//                        default: break;
//                    }
//                }
//                caliParameters.Add(p);
//            }
//#if DEBUG
//            //if (this.Channel==1)
//            //{
//            //    if (IsOutOfRange(caliParameters))
//            //    {
//            //        sampleDataList.Clear();
//            //        MessageBox.Show("P值变化大，请重试！");
//            //        return;
//            //    }
//            //}
//#else
//            if (IsOutOfRange(caliParameters))
//            {
//                sampleDataList.Clear();
//                MessageBox.Show("P值变化大，请重试！");
//                return;
//            }
//#endif
          

//            WritePressureCaliParameter2Pump(caliParameters);
//            detail.P0 = m_Ch1SampleDataList.Min(x => x.m_ApValue) * 100;
//            detail.CaliParameters = caliParameters;
//            //如果是单泵，则调用下列代码
//            if (m_LocalPid != PumpID.GrasebyF6_2 && m_LocalPid != PumpID.WZS50F6_2)
//            {
//                string path = Path.GetDirectoryName(Assembly.GetAssembly(typeof(SmartMainForm)).Location) + "\\数据导出";
//                string fileName = m_PumpNo;
//                if (m_LocalPid == PumpID.GrasebyF6 || m_LocalPid == PumpID.WZS50F6)
//                    fileName = string.Format("{0}{1}道{2}{3}", m_LocalPid.ToString(), m_Channel, m_PumpNo, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss"));
//                else
//                    fileName = string.Format("{0}{1}{2}", m_LocalPid.ToString(), m_PumpNo, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss"));
//                if (!System.IO.Directory.Exists(path))
//                    System.IO.Directory.CreateDirectory(path);
//                string saveFileName = path + "\\" + fileName + ".xlsx";

//                string path2 = Path.GetDirectoryName(Assembly.GetAssembly(typeof(SmartMainForm)).Location) + "\\数据导出备份";
//                string fileName2 = m_PumpNo;
//                if (m_LocalPid == PumpID.GrasebyF6 || m_LocalPid == PumpID.WZS50F6)
//                    fileName2 = string.Format("{0}{1}道{2}{3}", m_LocalPid.ToString(), m_Channel, m_PumpNo, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss"));
//                else
//                    fileName2 = string.Format("{0}{1}{2}", m_LocalPid.ToString(), m_PumpNo, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss"));
//                if (!System.IO.Directory.Exists(path2))
//                    System.IO.Directory.CreateDirectory(path2);
//                string saveFileName2 = path2 + "\\" + fileName2 + ".xlsx";

//                GenReport(saveFileName, caliParameters,saveFileName2);
//            }
        }

        /// <summary>
        /// 给一组标准重量值(kg)，从采样的结果中查找与它最相近的值所对应的压力值（V）
        /// </summary>
        /// <param name="weight"></param>
        /// <param name="sampleDataList"></param>
        /// <returns></returns>
        private void FindNearestPValue(ref List<PressureParameter> parameters, List<SampleData> sampleDataList)
        {
            if (sampleDataList == null || sampleDataList.Count == 0)
                return;
            List<float> absList = new List<float>();
            List<int> indexs = new List<int>();
            for (int i = 0; i < parameters.Count; i++)
            {
                absList.Add(10000f);
                indexs.Add(0);
                for (int iLoop = 0; iLoop < sampleDataList.Count; iLoop++)
                {
                    float abs = Math.Abs(parameters[i].m_MidWeight - sampleDataList[iLoop].m_ACDValue);
                    if (absList[i] > abs)
                    {
                        absList[i] = abs;
                        indexs[i] = iLoop;
                        parameters[i].SetPressure(sampleDataList[iLoop].m_ApValue);
                    }
                }
            }
        }

        /// <summary>
        /// 找到P值最小的
        /// </summary>
        /// <param name="sampleDataList"></param>
        private float FindZeroPValue(List<SampleData> sampleDataList)
        {
            if (sampleDataList == null || sampleDataList.Count == 0)
            {
                Logger.Instance().Error("测量数据为空，无法确定P值大小!");
                return 0;
            }
            float minP = m_Ch1SampleDataList.Min(x => x.m_ApValue);
            return minP;
        }

        /// <summary>
        /// 写入LCH压力档值
        /// </summary>
        /// <param name="caliParas"></param>
        private void WritePressureCaliParameter2Pump(List<PressureCalibrationParameter> caliParas)
        {
            if (caliParas == null || caliParas.Count == 0)
                return;
            for (int i = 0; i < caliParas.Count; i++)
            {
                Logger.Instance().InfoFormat("准备写入压力数据Size={0},L={1},C={2},H={3}", (byte)(caliParas[i].m_SyringeSize), caliParas[i].m_PressureL, caliParas[i].m_PressureC, caliParas[i].m_PressureH);
                m_ConnResponse.SetPressureCalibrationParameter((byte)(caliParas[i].m_SyringeSize), caliParas[i].m_PressureL, caliParas[i].m_PressureC, caliParas[i].m_PressureH);
                Logger.Instance().Info("写入压力数据完毕，等待泵回应......");
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// 在写入泵之前判断数据是否超范围
        /// </summary>
        /// <param name="caliParas"></param>
        /// <returns></returns>
        private bool IsOutOfRange(List<PressureCalibrationParameter> caliParas)
        {
            bool bRet = false;
            for (int i = 0; i < caliParas.Count; i++)
            {
                if (caliParas[i].m_PressureL * 100 >= SmartMainForm.PressureCalibrationMax)
                {
                    Logger.Instance().ErrorFormat("P值变化大，请重试！L ={0}", caliParas[i].m_PressureL);
                    bRet = true;
                    break;
                }
                if (caliParas[i].m_PressureC * 100 >= SmartMainForm.PressureCalibrationMax)
                {
                    Logger.Instance().ErrorFormat("P值变化大，请重试！C ={0}", caliParas[i].m_PressureC);
                    bRet = true;
                    break;
                }
                if (caliParas[i].m_PressureH * 100 >= SmartMainForm.PressureCalibrationMax)
                {
                    Logger.Instance().ErrorFormat("P值变化大，请重试！H ={0}", caliParas[i].m_PressureH);
                    bRet = true;
                    break;
                }

                if (caliParas[i].m_PressureH <= caliParas[i].m_PressureC || caliParas[i].m_PressureC <= caliParas[i].m_PressureL || caliParas[i].m_PressureH <= caliParas[i].m_PressureL)
                {
                    Logger.Instance().ErrorFormat("P值异常高档位值小于低档位，请重试！{0}，{1}，{2}", caliParas[i].m_PressureL, caliParas[i].m_PressureC, caliParas[i].m_PressureH);
                    bRet = true;
                    break;
                }
            }
            return bRet;
        }

        /// <summary>
        /// 写入P值
        /// </summary>
        /// <param name="caliParas"></param>
        private void WritePValue2Pump(float p)
        {
            Logger.Instance().InfoFormat("准备写入P值 {0}", p);
            m_ConnResponse.SetPressureCalibrationPValue(p);
            Logger.Instance().Info("写入P值完毕，等待泵回应......");
            Thread.Sleep(1000);
        }

        /// <summary>
        /// 计算得出的数据是否合法，此函数弃用
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private bool IsValid(List<PressureParameter> parameters)
        {
            bool bRet = true;
            foreach (var p in parameters)
            {
                if (p.m_Pressure >= p.m_MidWeight * 2.5)
                {
                    bRet = false;
                    break;
                }
            }
            return bRet;
        }

        private bool IsValidEx(List<SampleData> sampleDataList)
        {
            bool bRet = true;
            int iLength = sampleDataList.Count;
            if (iLength > 0 && sampleDataList[0].m_ApValue > 2.5f)
            {
                bRet = false;
                return bRet;
            }
            //当采集到的重量大于配置参数时，可以停止采集，并计算相关数据写入到泵中
            //PumpID pid = PumpID.None;
            //switch (m_LocalPid)
            //{
            //    case PumpID.GrasebyF6_2:
            //        pid = PumpID.GrasebyF6;
            //        break;
            //    case PumpID.WZS50F6_2:
            //        pid = PumpID.WZS50F6;
            //        break;
            //    default:
            //        pid = m_LocalPid;
            //        break;
            //}
            //float max = PressureManager.Instance().GetMaxBySizeLevel(pid, 50, Misc.OcclusionLevel.H);
            //if (iLength > 0 && sampleDataList[iLength - 1].m_ACDValue < max)
            //{
            //    bRet = false;
            //}
            return bRet;
        }

        private void picStart_Click(object sender, EventArgs e)
        {
            m_bStopClick = false;
            mCurrentSamplingIndex = 0;
            m_Ch1SampleDataList.Clear();
            mSamplingPointList.Clear();
            WavelinePanel.Invalidate();

            #region 参数输入检查

            if (SamplingStartOrStop != null)
            {
                SamplingStartOrStop(this, new StartOrStopArgs(true));
            }

            if (string.IsNullOrEmpty(tbToolNo.Text))
            {
                MessageBox.Show("请输入操作员工号");
                return;
            }
            
            if (string.IsNullOrEmpty(PumpNo))
            {
                MessageBox.Show("请输入产品型号");
                return;
            }

            if (PumpNo.Length != SmartMainForm.SerialNumberCount)
            {
                string message = string.Format("产品序号长度不等于{0}位", SmartMainForm.SerialNumberCount);
                MessageBox.Show(message);
                return;
            }

            float weight = 0;
            float rate = 0;

            if (cbToolingPort.SelectedIndex < 0)
            {
                MessageBox.Show("请选择工装串口");
                return;
            }
            if (!float.TryParse(lbWeight.Text, out weight))
            {
                MessageBox.Show("工装串口连接错误，请正确选择串口！");
                return;
            }

            if (cbPumpPort.SelectedIndex < 0)
            {
                MessageBox.Show("请选择泵串口");
                return;
            }
            if (!float.TryParse(lbPValue.Text, out weight))
            {
                MessageBox.Show("泵串口连接错误，请正确选择串口！");
                return;
            }
            if (string.IsNullOrEmpty(tbToolNo.Text))
            {
                MessageBox.Show("请输入工装编号");
                return;
            }
            #endregion

            if (m_SmartAP != null && m_SmartAP.IsOpen())
            {
                m_SmartAP.Close();
            }
            m_SmartAP = new SmartAP();
            m_SmartAP.Init(cbPumpPort.Items[cbPumpPort.SelectedIndex].ToString());

            if (m_PTool != null)
            {
                if (m_PTool.IsOpen())
                { }
                else
                {
                    m_PTool.Init(cbToolingPort.Items[cbToolingPort.SelectedIndex].ToString());
                    m_PTool.Open();
                }
            }
            else
            {
                m_PTool = new MeterACD201();
                m_PTool.Init(cbToolingPort.Items[cbToolingPort.SelectedIndex].ToString());
                m_PTool.Open();
            }
            m_SmartAP.SendCmdStartAP();
            EnableContols(false);
        }

        private void picStop_Click(object sender, EventArgs e)
        {
            m_bStopClick = true;//人工停止了
            mCurrentSamplingIndex = 0;
            Complete();
            EnableContols(true);
        }

        private void picDetail_Click(object sender, EventArgs e)
        {
            //this.detail.Show();
            //测试数据

            m_Ch1SampleDataList.Clear();

            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 1.2424f, -5.51f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 1.3117f, -11.93f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 1.3176f, -12.04f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 1.3959f, -10.6f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 1.2893f, -7.22f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 1.0915f, -17.65f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 1.0809f, -26.85f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.8502f, -36.64f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.8475f, -49.26f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.849f, -49.28f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.8471f, -49.28f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.8146f, -50.3f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.788f, -51.94f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.7845f, -54.19f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.7525f, -54.34f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.6528f, -56.84f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.6109f, -65.3f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.5363f, -68.89f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.4735f, -75.71f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.451f, -78.63f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.4334f, -81.05f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.4315f, -82.74f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.4278f, -82.86f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.4056f, -83.66f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.3873f, -84.57f));
            m_Ch1SampleDataList.Add(new SampleData(DateTime.Now, 0.3793f, -86.11f));
            DrawAccuracyMap();
        }

        private void tbOprator_KeyPress(object sender, KeyPressEventArgs e)
        {
            TextBox txt = sender as TextBox;
            if (char.IsNumber(e.KeyChar) || e.KeyChar == (char)Keys.Back)
            {
                e.Handled = false;                         //让操作生效
                if (txt.Text.Length >= 8)
                {
                    if (e.KeyChar == (char)Keys.Back)
                        e.Handled = false;             //让操作生效
                    else
                        e.Handled = true;              //让操作失效，如果第一个字符是2以上，不能输入其他字符
                }
                else
                {
                    e.Handled = false;                 //让操作生效
                }
            }
            else
            {
                e.Handled = true;
            }
        }

        public void InputOpratorNumber(string number)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new DelegateInputOpratorNumber(InputOpratorNumber), new object[] { number });
            }
            else
            {
                this.tbToolNo.Text = number;
            }
        }

        private void tbOprator_TextChanged(object sender, EventArgs e)
        {
            if (OpratorNumberInput != null)
            {
                OpratorNumberInput(this, new OpratorNumberArgs(tbToolNo.Text));
            }
        }


        #region 一次方程计算压力P值

        /// <summary>
        /// 计算一次方程的a和b，需要两个点
        /// </summary>
        /// <param name="x1"></param>
        /// <param name="y1"></param>
        /// <param name="x2"></param>
        /// <param name="y2"></param>
        /// <returns></returns>
        private Tuple<double, double> CalculateSlope(double x1, double y1, double x2, double y2)
        {
            double a = (y2 - y1) / (x2 - x1);
            double b = y2 - (a * x2);
            Tuple<double, double> slope = new Tuple<double, double>(a ,b);
            return slope;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameters"></param>
        private void CalcuatePolyParameters()
        {
            int pointCount = mSamplingPointList.Count;
            if (pointCount < 8)
            {
                Logger.Instance().WarnFormat("采样点{0}个点，曲线精度可能受影响！",pointCount );
            }

            StringBuilder sb = new StringBuilder("采样的点[重量：压力]：");
            for (int i = 0; i < pointCount; i++)
            {
                sb.Append(mSamplingPointList[i].m_ACDValue.ToString());
                sb.Append(" : ");
                sb.Append(mSamplingPointList[i].m_ApValue.ToString());
                if (i != pointCount - 1)
                    sb.Append(",  ");
            }
            Logger.Instance().Info(sb.ToString());
            double[] paraPoly = CalculatePoly();
            double a = paraPoly[2];
            double b = paraPoly[1];
            double c = paraPoly[0];
            float apValue = mSamplingPointList[pointCount - 2].m_ApValue;
            float acdValue = mSamplingPointList[pointCount - 2].m_ACDValue;
            double temp = c + b * apValue + a * apValue * apValue;
            if (Math.Abs(temp - acdValue) >= SmartMainForm.m_StandardError)
            {
                Logger.Instance().InfoFormat("二次方程计算结果与实际采样有较大误差,采样值={0}，计算值={1}", acdValue, temp);
                MessageBox.Show("采样误差大，请重试！");
                return;
            }
            else
            {
                //结果正确，可以写入Smart AP泵中
                m_SmartAP.SendCmdWriteParameter((decimal)a, (decimal)b, (decimal)c);
            }

        }
        #endregion

        #region 2次方程计算压力P值,11采样点分2段进行

       

        /// <summary>
        ///y =  c + b*x + a*x*x
        /// </summary>
        /// <param name="arrX"></param>
        /// <param name="arrY"></param>
        /// <param name="length"></param>
        /// <param name="dimension"></param>
        /// <returns>c,b,a</returns>
        private double[] CalculatePoly(double[] arrX, double[] arrY, int dimension = 2)
        {
            return Polynomial.MultiLine(arrX, arrY, arrX.Length, dimension);
        }

        private double[] CalculatePoly()
        {
            if (mSamplingPointList.Count < SmartMainForm.SamplingPoints.Count)
            {
                MessageBox.Show("曲线采样点太少，无法计算拟合曲线！");
                return null;
            }
            double[] arrX = new double[SmartMainForm.SamplingPoints.Count];
            double[] arrY = new double[SmartMainForm.SamplingPoints.Count];
            for (int i=0;i< SmartMainForm.SamplingPoints.Count;i++)
            {
                arrX[i] = mSamplingPointList[i].m_ApValue;
                arrY[i] = mSamplingPointList[i].m_ACDValue;
            }
            return CalculatePoly(arrX, arrY,2);
        }


        /// <summary>
        /// 多项式拟合
        /// </summary>
        /// <param name="pid"></param>
        /// <param name="sampleDataList"></param>
        /// <returns></returns>
        private List<PressureCalibrationParameter> CalcuatePressureByPoly(List<SampleData> sampleDataList)
        {
            return null;
            //if (sampleDataList == null || sampleDataList.Count == 0)
            //    return null;
            //if (m_Channel == 1)
            //{
            //    switch (pid)
            //    {
            //        case PumpID.GrasebyF6_2:
            //            pid = PumpID.GrasebyF6;
            //            break;
            //        case PumpID.WZS50F6_2:
            //            pid = PumpID.WZS50F6;
            //            break;
            //        default:
            //            break;
            //    }
            //}
            //List<PressureParameter> parameters = new List<PressureParameter>();
            //ProductPressure pp = PressureManager.Instance().GetPressureByProductID(pid);
            //if (pp == null)
            //    return null;
            //List<LevelPressure> lps = pp.GetLevelPressureList();
            //List<float> midWeights = new List<float>();
            //List<int> sizes = new List<int>();
            //if (pid == PumpID.WZS50F6 || pid == PumpID.GrasebyF6 || pid == PumpID.GrasebyF6_2 || pid == PumpID.WZS50F6_2)
            //    sizes.Add(10);
            //sizes.Add(20);
            //sizes.Add(30);
            //sizes.Add(50);

            //foreach (var size in sizes)
            //{
            //    foreach (Misc.OcclusionLevel level in Enum.GetValues(typeof(Misc.OcclusionLevel)))
            //    {
            //        LevelPressure lp = lps.Find((x) => { return x.m_Level == level; });
            //        if (lp != null)
            //        {
            //            SizePressure sp = lp.Find(size);
            //            if (sp != null)
            //            {
            //                PressureParameter para = new PressureParameter(size, level, sp.m_Mid, 0);
            //                parameters.Add(para);
            //            }
            //        }
            //    }
            //}

            ////计算相关的值，一次方程
            //CalcuatePValue(ref parameters);

            //if (!IsValidEx(sampleDataList))
            //{
            //    sampleDataList.Clear();
            //    MessageBox.Show("测量数据异常，请重试！");
            //    return null;
            //}

            //float pValue = FindZeroPValue(sampleDataList);

            //if (pValue * 100 >= SmartMainForm.RangeMaxP || pValue * 100 <= SmartMainForm.RangeMinP)
            //{
            //    Logger.Instance().ErrorFormat("P值超范围，请重试！P值={0},最小值={1},最大值={2}", pValue, SmartMainForm.RangeMinP, SmartMainForm.RangeMaxP);
            //    sampleDataList.Clear();
            //    MessageBox.Show("P值超范围，请重试！");
            //    return null;
            //}

            //WritePValue2Pump(pValue);
            //Logger.Instance().InfoFormat("测量结束，P值为{0}", pValue);

            //List<PressureCalibrationParameter> caliParameters = new List<PressureCalibrationParameter>();
            //foreach (var size in sizes)
            //{
            //    PressureCalibrationParameter p = new PressureCalibrationParameter();
            //    p.m_P0 = pValue;
            //    p.m_SyringeSize = size;
            //    List<PressureParameter> findobjs = parameters.FindAll((x) => { return x.m_SyringeSize == size; });
            //    foreach (var obj in findobjs)
            //    {
            //        switch (obj.m_Level)
            //        {
            //            case Misc.OcclusionLevel.L:
            //                p.m_PressureL = obj.m_Pressure;
            //                break;
            //            case Misc.OcclusionLevel.C:
            //                p.m_PressureC = obj.m_Pressure;
            //                break;
            //            case Misc.OcclusionLevel.H:
            //                p.m_PressureH = obj.m_Pressure;
            //                break;
            //            default: break;
            //        }
            //    }
            //    caliParameters.Add(p);
            //}

            //if (IsOutOfRange(caliParameters))
            //{
            //    sampleDataList.Clear();
            //    MessageBox.Show("P值变化大，请重试！");
            //    return null;
            //}

            //WritePressureCaliParameter2Pump(caliParameters);
            //detail.P0 = m_Ch1SampleDataList.Min(x => x.m_ApValue) * 100;
            //detail.CaliParameters = caliParameters;
            ////如果是单泵，则调用下列代码
            //if (m_LocalPid != PumpID.GrasebyF6_2 && m_LocalPid != PumpID.WZS50F6_2)
            //{
            //    string path = Path.GetDirectoryName(Assembly.GetAssembly(typeof(SmartMainForm)).Location) + "\\数据导出";
            //    string fileName = m_PumpNo;
            //    if (m_LocalPid == PumpID.GrasebyF6 || m_LocalPid == PumpID.WZS50F6)
            //        fileName = string.Format("{0}{1}道{2}{3}", m_LocalPid.ToString(), m_Channel, m_PumpNo, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss"));
            //    else
            //        fileName = string.Format("{0}{1}{2}", m_LocalPid.ToString(), m_PumpNo, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss"));
            //    if (!System.IO.Directory.Exists(path))
            //        System.IO.Directory.CreateDirectory(path);
            //    string saveFileName = path + "\\" + fileName + ".xlsx";

            //    string path2 = Path.GetDirectoryName(Assembly.GetAssembly(typeof(SmartMainForm)).Location) + "\\数据导出备份";
            //    string fileName2 = m_PumpNo;
            //    if (m_LocalPid == PumpID.GrasebyF6 || m_LocalPid == PumpID.WZS50F6)
            //        fileName2 = string.Format("{0}{1}道{2}{3}", m_LocalPid.ToString(), m_Channel, m_PumpNo, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss"));
            //    else
            //        fileName2 = string.Format("{0}{1}{2}", m_LocalPid.ToString(), m_PumpNo, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss"));
            //    if (!System.IO.Directory.Exists(path2))
            //        System.IO.Directory.CreateDirectory(path2);
            //    string saveFileName2 = path2 + "\\" + fileName2 + ".xlsx";

            //    GenReport(saveFileName, caliParameters, saveFileName2);
            //}
            ////返回计算好的值
            //return caliParameters;
        }

        #endregion



    }
}
