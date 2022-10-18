using System;
using System.Collections;
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
    public partial class SmartMainForm : Form
    {
        private bool moving = false;
        private Point oldMousePosition;
        private PumpID m_LocalPid = PumpID.GrasebyC6;//默认显示的是C6
        private int m_SampleInterval = 500;//采样频率：毫秒
        //private List<List<SampleData>> m_SampleDataList = new List<List<SampleData>>();//存放双道泵上传的数据，等第二道泵结束后，一起存在一张表中

        private Hashtable hashSampleData = new Hashtable();//[Key=通道编号（0，1）, Value=List<PressureCalibrationParameter>]



        public static int RangeMinP = 170;
        public static int RangeMaxP = 210;
        public static int PressureCalibrationMax = 418;
        public static int SerialNumberCount = 28;               //在指定时间内连续输入字符数量不低于28个时方可认为是由条码枪输入
        //public static List<double> SamplingPoints1 = new List<double>();//采样点大概5个，当工装读数在某个值时，自动停止，等待5秒，再读三次工装和P值，比较是否稳定。不稳定，再读
        //public static List<double> SamplingPoints2 = new List<double>();//采样点大概5个，当工装读数在某个值时，自动停止，等待5秒，再读三次工装和P值，比较是否稳定。不稳定，再读

        //public static List<double> SingleSamplingPoints1 = new List<double>();//采样点大概5个，当工装读数在某个值时，自动停止，等待5秒，再读三次工装和P值，比较是否稳定。不稳定，再读
      
        public static List<float> SamplingPoints = new List<float>();//采样点大概5个,当工装读数在某个值时，自动停止，等待5秒，再读三次工装和P值，比较是否稳定。不稳定，再读
        public static double m_StandardError = 0.05; 
        public static double m_SamplingError = 0.1; //采样误差范围


        private const int INPUTSPEED = 50;//条码枪输入字符速率小于50毫秒
        

        private DateTime m_CharInputTimestamp = DateTime.Now;  //定义一个成员函数用于保存每次的时间点
        private DateTime m_FirstCharInputTimestamp = DateTime.Now;  //定义一个成员函数用于保存每次的时间点
        private DateTime m_SecondCharInputTimestamp = DateTime.Now;  //定义一个成员函数用于保存每次的时间点
        private int m_PressCount = 0;

        public SmartMainForm()
        {
            InitializeComponent();
            InitUI();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x1EE1)
            {
                ClearPumpNoWhenCompleteTest();
            }
            base.WndProc(ref m);
        }


        private void PressureForm_Load(object sender, EventArgs e)
        {
         
            LoadSettings();
            LoadConfig();
        }

        /// <summary>
        /// 加载配置文件中的参数
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                string strInterval = ConfigurationManager.AppSettings.Get("SampleInterval");
                if (!int.TryParse(strInterval, out m_SampleInterval))
                    m_SampleInterval = 500;
                chart1.SampleInterval = m_SampleInterval;
              
                string strTool1 = ConfigurationManager.AppSettings.Get("Tool1");
                string strTool2 = ConfigurationManager.AppSettings.Get("Tool2");
             
                RangeMinP = Int32.Parse(ConfigurationManager.AppSettings.Get("RangeMinP"));
                RangeMaxP = Int32.Parse(ConfigurationManager.AppSettings.Get("RangeMaxP"));
                PressureCalibrationMax = Int32.Parse(ConfigurationManager.AppSettings.Get("PressureCalibrationMax"));
                SerialNumberCount = Int32.Parse(ConfigurationManager.AppSettings.Get("SerialNumberCount"));
                var samplingPoint = ConfigurationManager.AppSettings.Get("SamplingPoint");

                //0~3.5kg区间
                string[] strSamplingPoints = samplingPoint.Trim().Split(',');
                SamplingPoints.Clear();
                foreach (string s in strSamplingPoints)
                {
                    SamplingPoints.Add(float.Parse(s));
                }

                var standardError = ConfigurationManager.AppSettings.Get("StandardError");
                m_StandardError = double.Parse(standardError);

                var samplingError = ConfigurationManager.AppSettings.Get("SamplingError");
                m_SamplingError = double.Parse(samplingError);
            }
            catch (Exception ex)
            {
                MessageBox.Show("PTool.config文件参数配置错误，请先检查该文件后再重新启动程序!" + ex.Message);
            }
        }

        private void LoadSettings()
        {
            string currentPath = Assembly.GetExecutingAssembly().Location;
            currentPath = currentPath.Substring(0, currentPath.LastIndexOf('\\'));  //删除文件名
            string iniPath = currentPath + "\\ptool.ini";
            IniReader reader = new IniReader(iniPath);
            reader.ReadSettings();
        }

        private void SaveLastToolingNo()
        {
            Configuration cfa = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            cfa.Save();
        }

        private void InitUI()
        {
            chart1.SamplingStartOrStop += OnSamplingStartOrStop;
            chart1.OnSamplingComplete += OnChartSamplingComplete;
            hashSampleData.Clear();
        }

        /// <summary>
        /// 双道泵测量数据统一放进m_SampleDataList中，第一道数据索引为0，第二道为1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnChartSamplingComplete(object sender, DoublePumpDataArgs e)
        {
            Chart chart = sender as Chart;
            if (e.SampleDataList != null)
            {
                if (chart.Name == "chart1")
                {
                    if (hashSampleData.ContainsKey(1))
                        hashSampleData[1] = e.SampleDataList;
                    else
                        hashSampleData.Add(1, e.SampleDataList);
                }
                else
                {
                    if (hashSampleData.ContainsKey(2))
                        hashSampleData[2] = e.SampleDataList;
                    else
                        hashSampleData.Add(2, e.SampleDataList);
                }
            }
            if(hashSampleData.Count>=2)
            {
                //写入excel,调用chart类中函数
                string path = Path.GetDirectoryName(Assembly.GetAssembly(typeof(SmartMainForm)).Location) + "\\数据导出";
                PumpID pid = PumpID.None;
                switch (m_LocalPid)
                {
                    case PumpID.GrasebyF6_2:
                        pid = PumpID.GrasebyF6;
                        break;
                    case PumpID.WZS50F6_2:
                        pid = PumpID.WZS50F6;
                        break;
                    default:
                        pid = m_LocalPid;
                        break;
                }
                string fileName = string.Format("{0}_{1}_{2}", pid.ToString(), tbPumpNo.Text, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss"));
                if (!System.IO.Directory.Exists(path))
                    System.IO.Directory.CreateDirectory(path);
                string saveFileName = path + "\\" + fileName + ".xlsx";


                string path2 = Path.GetDirectoryName(Assembly.GetAssembly(typeof(SmartMainForm)).Location) + "\\数据导出备份";
                string fileName2 = string.Format("{0}_{1}_{2}", pid.ToString(), tbPumpNo.Text, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss"));
                if (!System.IO.Directory.Exists(path2))
                    System.IO.Directory.CreateDirectory(path2);
                string saveFileName2 = path2 + "\\" + fileName2 + ".xlsx";

                List<List<PressureCalibrationParameter>> sampleDataList = new List<List<PressureCalibrationParameter>>();
                if(hashSampleData.ContainsKey(1))
                    sampleDataList.Add(hashSampleData[1] as List<PressureCalibrationParameter>);
                if (hashSampleData.ContainsKey(2))
                    sampleDataList.Add(hashSampleData[2] as List<PressureCalibrationParameter>);
               
                Thread.Sleep(2000);
                hashSampleData.Clear();
            }
        }

        private void OnSamplingStartOrStop(object sender, EventArgs e)
        {
            StartOrStopArgs args = e as StartOrStopArgs;
            chart1.PumpNo = tbPumpNo.Text;
        }


        private void tlpTitle_MouseDown(object sender, MouseEventArgs e)
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                return;
            }
            oldMousePosition = e.Location;
            moving = true; 
        }

        private void tlpTitle_MouseUp(object sender, MouseEventArgs e)
        {
            moving = false;
        }

        private void tlpTitle_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && moving)
            {
                Point newPosition = new Point(e.Location.X - oldMousePosition.X, e.Location.Y - oldMousePosition.Y);
                this.Location += new Size(newPosition);
            }
        }

        private void picCloseWindow_Click(object sender, EventArgs e)
        {
            chart1.SamplingStartOrStop -= OnSamplingStartOrStop;
            chart1.OnSamplingComplete -= OnChartSamplingComplete;
            SaveLastToolingNo();
            this.Close();
        }

        /// <summary>
        /// 采样结束，清空产品序号 20180820
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearPumpNoWhenCompleteTest()
        {
            tbPumpNo.Clear();
        }

        private void tbPumpNo_KeyPress(object sender, KeyPressEventArgs e)
        {
            TimeSpan ts;
            m_SecondCharInputTimestamp = DateTime.Now;
            ts = m_SecondCharInputTimestamp.Subtract(m_FirstCharInputTimestamp);     //获取时间间隔
            if (ts.Milliseconds < INPUTSPEED)
                m_PressCount++;
            else
            {
                m_PressCount = 0;
            }

            if (m_PressCount == SerialNumberCount)
            {
                if (tbPumpNo.Text.Length >= SerialNumberCount)
                {
                    if (tbPumpNo.SelectionStart < tbPumpNo.Text.Length)
                        tbPumpNo.Text = tbPumpNo.Text.Remove(tbPumpNo.SelectionStart);
                    try
                    {
                        tbPumpNo.Text = tbPumpNo.Text.Substring(tbPumpNo.Text.Length - SerialNumberCount, SerialNumberCount);
                        tbPumpNo.SelectionStart = tbPumpNo.Text.Length;
                    }
                    catch
                    {
                        tbPumpNo.Text = "";
                    }
                }
                m_PressCount = 0;
            }
            m_FirstCharInputTimestamp = m_SecondCharInputTimestamp;
        }
    }

    public class SampleData
    {
        public DateTime m_SampleTime = DateTime.Now;
        public float m_ApValue;
        public float m_ACDValue;

        public SampleData()
        {
        }

        public SampleData(DateTime sampleTime, float Ap, float ACD)
        {
            m_SampleTime = sampleTime;
            m_ApValue = Ap;
            m_ACDValue = ACD;
        }

        public virtual void Copy(SampleData other)
        {
            this.m_SampleTime = other.m_SampleTime;
            this.m_ApValue = other.m_ApValue;
            this.m_ACDValue = other.m_ACDValue;
        }
    }
}
