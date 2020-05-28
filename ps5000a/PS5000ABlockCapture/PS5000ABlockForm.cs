

using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using PS5000AImports;
using PicoPinnedArray;
using PicoStatus;
using System.IO;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;

namespace PS5000A
{
    public partial class PS5000ABlockForm : Form
    {

        #region Поля
        private short _handle;
        public const int BUFFER_SIZE = 1024;
        public const int MAX_CHANNELS = 4;
        public const int QUAD_SCOPE = 4;
        public const int DUAL_SCOPE = 2;


        uint _timebase = 15;
        short _oversample = 1;
        bool _scaleVoltages = true;

        ushort[] inputRanges = { 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000 };
        bool _ready = false;
        short _trig = 0;
        uint _trigAt = 0;
        int _sampleCount = 0;
        uint _startIndex = 0;
        bool _autoStop;
        //private ChannelSettings[] _channelSettings;
        private int _channelCount;
        private Imports.Range _firstRange;
        private Imports.Range _lastRange;
        private int _digitalPorts;
        private Imports.ps5000aBlockReady _callbackDelegate;
        private string StreamFile = "stream.txt";
        private string BlockFile = "block.txt";
        double w0;
        double w1;
        int l;


        #endregion

        /// <summary>
        /// Конструктор
        /// </summary>
        public PS5000ABlockForm(double w0_, double w1_, int l_)
        {
            InitializeComponent();
            w0 = w0_;
            w1 = w1_;
            l = l_;

            comboRangeA.DataSource = System.Enum.GetValues(typeof(Imports.Range));


            progressBar1.Text = "Готов к работе";

            timer1.Interval = 300;
            timer1.Tick += new EventHandler(Timer1_Tick);
        }
        string[] filenames;
        private int all, save = 0;
        private void Timer1_Tick(object Sender, EventArgs e)
        {
            progressBar1.Value = (int)(((double)save) / all * progressBar1.Maximum);
            Refresh();

        }
        ////private void SetFiles()
        ////{
        ////    filenames = new string[4];
        ////    filenames[0] = $"f(w) from ({textBox1.Text} , {textBox2.Text}).txt";
        ////    filenames[1] = $"f(w) from ({textBox4.Text} , {textBox3.Text}).txt";
        ////    filenames[2] = $"f(w) from ({textBox6.Text} , {textBox5.Text}).txt";
        ////    filenames[3] = $"f(w) from ({textBox8.Text} , {textBox7.Text}).txt";
        ////}


        /// <summary>
        /// Обратная связб от осциллографа
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="status"></param>
        /// <param name="pVoid"></param>
        private void BlockCallback(short handle, short status, IntPtr pVoid)
        {
            // flag to say done reading data
            if (status != (short)StatusCodes.PICO_CANCELLED)
                _ready = true;
        }


        private uint SetTrigger(Imports.TriggerChannelProperties[] channelProperties,
            short nChannelProperties,
            Imports.TriggerConditions[] triggerConditions,
            short nTriggerConditions,
            Imports.ThresholdDirection[] directions,
            uint delay,
            short auxOutputEnabled,
            int autoTriggerMs)
        {
            uint status;

            status = Imports.SetTriggerChannelProperties(_handle, channelProperties, nChannelProperties, auxOutputEnabled,
                                                   autoTriggerMs);
            if (status != StatusCodes.PICO_OK)
            {
                return status;
            }

            status = Imports.SetTriggerChannelConditions(_handle, triggerConditions, nTriggerConditions);

            if (status != StatusCodes.PICO_OK)
            {
                return status;
            }

            if (directions == null)
            {
                directions = new Imports.ThresholdDirection[] { Imports.ThresholdDirection.None,
                                Imports.ThresholdDirection.None, Imports.ThresholdDirection.None, Imports.ThresholdDirection.None,
                                Imports.ThresholdDirection.None, Imports.ThresholdDirection.None};
            }

            status = Imports.SetTriggerChannelDirections(_handle,
                                                               directions[(int)Imports.Channel.ChannelA],
                                                               directions[(int)Imports.Channel.ChannelB],
                                                               directions[(int)Imports.Channel.ChannelC],
                                                               directions[(int)Imports.Channel.ChannelD],
                                                               directions[(int)Imports.Channel.External],
                                                               directions[(int)Imports.Channel.Aux]);
            if (status != StatusCodes.PICO_OK)
            {
                return status;
            }

            status = Imports.SetTriggerDelay(_handle, delay);

            if (status != StatusCodes.PICO_OK)
            {
                return status;
            }

            return status;
        }


        private void button1_Click(object sender, EventArgs e)
        {
            StringBuilder UnitInfo = new StringBuilder(80);

            short handle;

            string[] description = {
                           "Driver Version    ",
                           "USB Version       ",
                           "Hardware Version  ",
                           "Variant Info      ",
                           "Serial            ",
                           "Cal Date          ",
                           "Kernel Ver        ",
                           "Digital Hardware  ",
                           "Analogue Hardware "
                         };

            Imports.DeviceResolution resolution = Imports.DeviceResolution.PS5000A_DR_16BIT;
            

            if (_handle > 0)
            {
                Imports.CloseUnit(_handle);
                //textBoxUnitInfo.Text = "";
                _handle = 0;
                button1.Text = "Open";
            }
            else
            {
                uint status = Imports.OpenUnit(out handle, null, resolution);

                if (handle > 0)
                {
                    _handle = handle;

                    if (status == StatusCodes.PICO_POWER_SUPPLY_NOT_CONNECTED || status == StatusCodes.PICO_USB3_0_DEVICE_NON_USB3_0_PORT)
                    {
                        status = Imports.ChangePowerSource(_handle, status);
                    }
                    else if (status != StatusCodes.PICO_OK)
                    {
                        MessageBox.Show("Cannot open device error code: " + status.ToString(), "Error Opening Device", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.Close();
                    }
                    else
                    {
                        // Do nothing - power supply connected
                    }

                    //textBoxUnitInfo.Text = "Handle            " + _handle.ToString() + "\r\n";

                    //for (int i = 0; i < 9; i++)
                    //{
                    //    short requiredSize;
                    //    Imports.GetUnitInfo(_handle, UnitInfo, 80, out requiredSize, (uint)i);
                    //    textBoxUnitInfo.AppendText(description[i] + UnitInfo + "\r\n");
                    //}
                    button1.Text = "Закрыть";
                }
            }
        }
        

        void start(uint sampleCountAfter = 50000, uint sampleCountBefore = 50000, int write_every = 100)
        {
            uint all_ = sampleCountAfter + sampleCountBefore;
            uint status;
            int ms;
            status = Imports.MemorySegments(_handle, 1, out ms);
            Imports.Range IR = (Imports.Range)comboRangeA.SelectedIndex;
              status = Imports.SetChannel(_handle, Imports.Channel.ChannelA, 1, Imports.Coupling.PS5000A_AC, IR, 0);
            // Voltage_Range = 200;
            //  status = Imports.SetChannel(_handle, Imports.Channel.ChannelA, 1, Imports.Coupling.PS5000A_AC, Imports.Range.Range_200mV, 0);
            //status = Imports.SetChannel(_handle, Imports.Channel.ChannelA, 1, Imports.Coupling.PS5000A_DC, Imports.Range.Range_200mV, 0);

            const short enable = 1;
            const uint delay = 0;
            const short threshold = 25000;
            const short auto = 22222;
            if (checkBox1.Checked)
            {
                status = Imports.SetBandwidthFilter(_handle, Imports.Channel.ChannelA, Imports.BandwidthLimiter.PS5000A_BW_20MHZ);
            }
            status = Imports.SetSimpleTrigger(_handle, enable, Imports.Channel.External, threshold, Imports.ThresholdDirection.Rising, delay, auto);
            _ready = false;
            _callbackDelegate = BlockCallback;
            _channelCount = 1;
            //string data;
            int x;


            bool retry;

            PinnedArray<short>[] minPinned = new PinnedArray<short>[_channelCount];
            PinnedArray<short>[] maxPinned = new PinnedArray<short>[_channelCount];

            int timeIndisposed;
            short[] minBuffersA = new short[all_];
            short[] maxBuffersA = new short[all_];
            long[] masA   = new long[all_];
            minPinned[0] = new PinnedArray<short>(minBuffersA);
            maxPinned[0] = new PinnedArray<short>(maxBuffersA);
            status = Imports.SetDataBuffers(_handle, Imports.Channel.ChannelA, maxBuffersA, minBuffersA, (int)sampleCountAfter + (int)sampleCountBefore, 0, Imports.RatioMode.None);

            //int timeInterval;
            //int maxSamples;
            //while (Imports.GetTimebase(_handle, _timebase, (int)sampleCount, out timeInterval, out maxSamples, 0) != 0)
            //{
            //    _timebase++;
            //}
            _ready = false;
            _callbackDelegate = BlockCallback;
            do
            {
                retry = false;
                status = Imports.RunBlock(_handle, (int)sampleCountBefore, (int)sampleCountAfter, _timebase, out timeIndisposed, 0, _callbackDelegate, IntPtr.Zero);
                if (status == (short)StatusCodes.PICO_POWER_SUPPLY_CONNECTED || status == (short)StatusCodes.PICO_POWER_SUPPLY_NOT_CONNECTED || status == (short)StatusCodes.PICO_POWER_SUPPLY_UNDERVOLTAGE)
                {
                    status = Imports.ChangePowerSource(_handle, status);
                    retry = true;
                }
                else
                {
                    //  textMessage.AppendText("Run Block Called\n");
                }
            }
            while (retry);
            while (!_ready)
            {
                Thread.Sleep(30);
                Application.DoEvents();
            }
            Imports.Stop(_handle);
            if (_ready)
            {
                short overflow;
                status = Imports.GetValues(_handle, 0, ref all_, 1, Imports.RatioMode.None, 0, out overflow);

                if (status == (short)StatusCodes.PICO_OK)
                {
                    for (x = 0; x < all_; x++)
                        masA[x] += maxBuffersA[x] + minBuffersA[x];//=========================================================!
                }

            }

            Imports.Stop(_handle);
            foreach (PinnedArray<short> p in minPinned)
            {
                if (p != null)
                    p.Dispose();
            }
            foreach (PinnedArray<short> p in maxPinned)
            {
                if (p != null)
                    p.Dispose();
            }

            Application.DoEvents();
        }

        double T0; double OffsetT; uint n = 10000; int time_scale; int countz = 100;
        double dt_ = 104 * 1.0E-9;

        double[] arrA;

        /*
         * 
         * частоты от 0 Гц 10Mhz
         * dt =10^-7
         * df = 100;
         *
         */
        Complex[] Transform_dataA; 
        async void CalcTransform(double f0, double f1 , int sc)
        {
            double fl = f1 - f0;
            double df = (f1 - f0) / (sc - 1);
            int count_approx = sc;
            Transform_dataA = new Complex[count_approx]; 

            all = sc;
            timer1.Start();
            await Task.Run(() => { 
            for (int j = 0; j<count_approx; j++)
            {
                save = j;
                //if ((j % 100) == 0)     textData.AppendText($"{j} / {count_approx}\n");
                for (int j1= 0;j1<n; j1++)
                {
                    Complex buf = new Complex (0,(f0 + df * j) * j1 * dt_*2*Math.PI);
                    Transform_dataA[j] += arrA[j1] * Complex.Exp(buf)* dt_; 
                    }
            }
            });
            timer1.Stop();
            progressBar1.Text = "Преобразование завершено. Данные записываются в файл";

            //  string filename = filenames[0]; 
      //первый 
            using (StreamWriter fs = new StreamWriter(filenames[0]))
            {
                fs.WriteLine("w Re(f(w)) Im(f(w))");
                for (int i = 0; i < count_approx; i++)
                {
                    //if ((i%100 )==0)   textData.AppendText($"{i} / {count_approx}\n");

                    double freq = (f0 + df * i) * 2* Math.PI * 1.0E-6;

                    fs.WriteLine(freq + " " + Transform_dataA[i].Real + " " + Transform_dataA[i].Imaginary);
                }

            }
            string filename2 = "TransA.txt";
            using (StreamWriter fs = new StreamWriter(filename2))
            { 
                for (int i = 0; i < count_approx; i++)
                {
                    //if ((i % 100) == 0) textData.AppendText($"{i} / {count_approx}\n");
                    fs.WriteLine((f0 + df * i).ToString() + " "+ Transform_dataA[i].Magnitude.ToString());
                }
            }

        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.label1 = new System.Windows.Forms.Label();
            this.comboRangeA = new System.Windows.Forms.ComboBox();
            this.textBox9 = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.textBox14 = new System.Windows.Forms.TextBox();
            this.label21 = new System.Windows.Forms.Label();
            this.textBox13 = new System.Windows.Forms.TextBox();
            this.label20 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.textBox11 = new System.Windows.Forms.TextBox();
            this.textBox10 = new System.Windows.Forms.TextBox();
            this.label14 = new System.Windows.Forms.Label();
            this.button2 = new System.Windows.Forms.Button();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.checkBox2 = new System.Windows.Forms.CheckBox();
            this.checkBox3 = new System.Windows.Forms.CheckBox();
            this.checkBox4 = new System.Windows.Forms.CheckBox();
            this.checkBox5 = new System.Windows.Forms.CheckBox();
            this.checkBox6 = new System.Windows.Forms.CheckBox();
            this.checkBox7 = new System.Windows.Forms.CheckBox();
            this.checkBox8 = new System.Windows.Forms.CheckBox();
            this.checkBox9 = new System.Windows.Forms.CheckBox();
            this.checkBox10 = new System.Windows.Forms.CheckBox();
            this.checkBox11 = new System.Windows.Forms.CheckBox();
            this.checkBox12 = new System.Windows.Forms.CheckBox();
            this.checkBox13 = new System.Windows.Forms.CheckBox();
            this.checkBox14 = new System.Windows.Forms.CheckBox();
            this.checkBox15 = new System.Windows.Forms.CheckBox();
            this.checkBox16 = new System.Windows.Forms.CheckBox();
            this.checkBox17 = new System.Windows.Forms.CheckBox();
            this.checkBox18 = new System.Windows.Forms.CheckBox();
            this.checkBox19 = new System.Windows.Forms.CheckBox();
            this.button3 = new System.Windows.Forms.Button();
            this.button4 = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.button5 = new System.Windows.Forms.Button();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label16 = new System.Windows.Forms.Label();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Controls.Add(this.tabPage5);
            this.tabControl1.Location = new System.Drawing.Point(3, 2);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(668, 333);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.label1);
            this.tabPage1.Controls.Add(this.comboRangeA);
            this.tabPage1.Controls.Add(this.textBox9);
            this.tabPage1.Controls.Add(this.label13);
            this.tabPage1.Controls.Add(this.button1);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(660, 307);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Подключение";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(467, 74);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(58, 13);
            this.label1.TabIndex = 26;
            this.label1.Text = "Диапазон";
            // 
            // comboRangeA
            // 
            this.comboRangeA.FormattingEnabled = true;
            this.comboRangeA.Location = new System.Drawing.Point(531, 70);
            this.comboRangeA.Name = "comboRangeA";
            this.comboRangeA.Size = new System.Drawing.Size(121, 21);
            this.comboRangeA.TabIndex = 25;
            // 
            // textBox9
            // 
            this.textBox9.Location = new System.Drawing.Point(310, 71);
            this.textBox9.Name = "textBox9";
            this.textBox9.Size = new System.Drawing.Size(100, 20);
            this.textBox9.TabIndex = 16;
            this.textBox9.Text = "4";
            this.textBox9.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(255, 74);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(49, 13);
            this.label13.TabIndex = 15;
            this.label13.Text = "timebase";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(17, 64);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(98, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "Подключиться";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.checkBox1);
            this.tabPage2.Controls.Add(this.textBox14);
            this.tabPage2.Controls.Add(this.label21);
            this.tabPage2.Controls.Add(this.textBox13);
            this.tabPage2.Controls.Add(this.label20);
            this.tabPage2.Controls.Add(this.label15);
            this.tabPage2.Controls.Add(this.textBox11);
            this.tabPage2.Controls.Add(this.textBox10);
            this.tabPage2.Controls.Add(this.label14);
            this.tabPage2.Controls.Add(this.button2);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(660, 307);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Сбор данных";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // textBox14
            // 
            this.textBox14.Location = new System.Drawing.Point(345, 21);
            this.textBox14.Name = "textBox14";
            this.textBox14.Size = new System.Drawing.Size(100, 20);
            this.textBox14.TabIndex = 32;
            this.textBox14.Text = "26000";
            this.textBox14.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label21
            // 
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(225, 24);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(114, 13);
            this.label21.TabIndex = 31;
            this.label21.Text = "Число подавляемых ";
            // 
            // textBox13
            // 
            this.textBox13.Location = new System.Drawing.Point(345, 56);
            this.textBox13.Name = "textBox13";
            this.textBox13.Size = new System.Drawing.Size(100, 20);
            this.textBox13.TabIndex = 30;
            this.textBox13.Text = "25000";
            this.textBox13.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label20
            // 
            this.label20.AutoSize = true;
            this.label20.Location = new System.Drawing.Point(251, 59);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(88, 13);
            this.label20.TabIndex = 29;
            this.label20.Text = "Число шагов до";
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(238, 131);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(101, 13);
            this.label15.TabIndex = 28;
            this.label15.Text = "Число усреднений";
            // 
            // textBox11
            // 
            this.textBox11.Location = new System.Drawing.Point(345, 128);
            this.textBox11.Name = "textBox11";
            this.textBox11.Size = new System.Drawing.Size(100, 20);
            this.textBox11.TabIndex = 27;
            this.textBox11.Text = "100";
            this.textBox11.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // textBox10
            // 
            this.textBox10.Location = new System.Drawing.Point(345, 93);
            this.textBox10.Name = "textBox10";
            this.textBox10.Size = new System.Drawing.Size(100, 20);
            this.textBox10.TabIndex = 26;
            this.textBox10.Text = "130000";
            this.textBox10.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(233, 100);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(106, 13);
            this.label14.TabIndex = 25;
            this.label14.Text = "Число шагов после";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(6, 18);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(103, 23);
            this.button2.TabIndex = 0;
            this.button2.Text = "Сбор данных";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(7, 338);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(660, 23);
            this.progressBar1.TabIndex = 1;
            this.progressBar1.Click += new System.EventHandler(this.progressBar1_Click);
            // 
            // timer1
            // 
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick_1);
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Checked = true;
            this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox1.Location = new System.Drawing.Point(41, 221);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(240, 17);
            this.checkBox1.TabIndex = 33;
            this.checkBox1.Text = "Ограничение полосы пропускания 20 МГц";
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.label16);
            this.tabPage3.Controls.Add(this.textBox1);
            this.tabPage3.Controls.Add(this.label12);
            this.tabPage3.Controls.Add(this.label11);
            this.tabPage3.Controls.Add(this.label10);
            this.tabPage3.Controls.Add(this.button5);
            this.tabPage3.Controls.Add(this.label9);
            this.tabPage3.Controls.Add(this.label8);
            this.tabPage3.Controls.Add(this.label7);
            this.tabPage3.Controls.Add(this.label6);
            this.tabPage3.Controls.Add(this.label5);
            this.tabPage3.Controls.Add(this.label4);
            this.tabPage3.Controls.Add(this.label3);
            this.tabPage3.Controls.Add(this.label2);
            this.tabPage3.Controls.Add(this.button4);
            this.tabPage3.Controls.Add(this.button3);
            this.tabPage3.Controls.Add(this.checkBox11);
            this.tabPage3.Controls.Add(this.checkBox12);
            this.tabPage3.Controls.Add(this.checkBox13);
            this.tabPage3.Controls.Add(this.checkBox14);
            this.tabPage3.Controls.Add(this.checkBox15);
            this.tabPage3.Controls.Add(this.checkBox16);
            this.tabPage3.Controls.Add(this.checkBox17);
            this.tabPage3.Controls.Add(this.checkBox18);
            this.tabPage3.Controls.Add(this.checkBox19);
            this.tabPage3.Controls.Add(this.checkBox10);
            this.tabPage3.Controls.Add(this.checkBox9);
            this.tabPage3.Controls.Add(this.checkBox8);
            this.tabPage3.Controls.Add(this.checkBox7);
            this.tabPage3.Controls.Add(this.checkBox6);
            this.tabPage3.Controls.Add(this.checkBox5);
            this.tabPage3.Controls.Add(this.checkBox4);
            this.tabPage3.Controls.Add(this.checkBox3);
            this.tabPage3.Controls.Add(this.checkBox2);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(660, 307);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Коммутация";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // tabPage4
            // 
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Size = new System.Drawing.Size(660, 307);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "Предобработка";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // tabPage5
            // 
            this.tabPage5.Location = new System.Drawing.Point(4, 22);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Size = new System.Drawing.Size(660, 307);
            this.tabPage5.TabIndex = 4;
            this.tabPage5.Text = "Обработка";
            this.tabPage5.UseVisualStyleBackColor = true;
            // 
            // checkBox2
            // 
            this.checkBox2.AutoSize = true;
            this.checkBox2.Location = new System.Drawing.Point(506, 65);
            this.checkBox2.Name = "checkBox2";
            this.checkBox2.Size = new System.Drawing.Size(15, 14);
            this.checkBox2.TabIndex = 0;
            this.checkBox2.UseVisualStyleBackColor = true;
            // 
            // checkBox3
            // 
            this.checkBox3.AutoSize = true;
            this.checkBox3.Location = new System.Drawing.Point(506, 65);
            this.checkBox3.Name = "checkBox3";
            this.checkBox3.Size = new System.Drawing.Size(15, 14);
            this.checkBox3.TabIndex = 0;
            this.checkBox3.UseVisualStyleBackColor = true;
            // 
            // checkBox4
            // 
            this.checkBox4.AutoSize = true;
            this.checkBox4.Location = new System.Drawing.Point(506, 88);
            this.checkBox4.Name = "checkBox4";
            this.checkBox4.Size = new System.Drawing.Size(15, 14);
            this.checkBox4.TabIndex = 1;
            this.checkBox4.UseVisualStyleBackColor = true;
            // 
            // checkBox5
            // 
            this.checkBox5.AutoSize = true;
            this.checkBox5.Location = new System.Drawing.Point(506, 111);
            this.checkBox5.Name = "checkBox5";
            this.checkBox5.Size = new System.Drawing.Size(15, 14);
            this.checkBox5.TabIndex = 2;
            this.checkBox5.UseVisualStyleBackColor = true;
            // 
            // checkBox6
            // 
            this.checkBox6.AutoSize = true;
            this.checkBox6.Location = new System.Drawing.Point(506, 134);
            this.checkBox6.Name = "checkBox6";
            this.checkBox6.Size = new System.Drawing.Size(15, 14);
            this.checkBox6.TabIndex = 3;
            this.checkBox6.UseVisualStyleBackColor = true;
            // 
            // checkBox7
            // 
            this.checkBox7.AutoSize = true;
            this.checkBox7.Location = new System.Drawing.Point(506, 157);
            this.checkBox7.Name = "checkBox7";
            this.checkBox7.Size = new System.Drawing.Size(15, 14);
            this.checkBox7.TabIndex = 4;
            this.checkBox7.UseVisualStyleBackColor = true;
            // 
            // checkBox8
            // 
            this.checkBox8.AutoSize = true;
            this.checkBox8.Location = new System.Drawing.Point(506, 180);
            this.checkBox8.Name = "checkBox8";
            this.checkBox8.Size = new System.Drawing.Size(15, 14);
            this.checkBox8.TabIndex = 5;
            this.checkBox8.UseVisualStyleBackColor = true;
            // 
            // checkBox9
            // 
            this.checkBox9.AutoSize = true;
            this.checkBox9.Location = new System.Drawing.Point(506, 203);
            this.checkBox9.Name = "checkBox9";
            this.checkBox9.Size = new System.Drawing.Size(15, 14);
            this.checkBox9.TabIndex = 6;
            this.checkBox9.UseVisualStyleBackColor = true;
            // 
            // checkBox10
            // 
            this.checkBox10.AutoSize = true;
            this.checkBox10.Location = new System.Drawing.Point(506, 226);
            this.checkBox10.Name = "checkBox10";
            this.checkBox10.Size = new System.Drawing.Size(15, 14);
            this.checkBox10.TabIndex = 7;
            this.checkBox10.UseVisualStyleBackColor = true;
            // 
            // checkBox11
            // 
            this.checkBox11.AutoSize = true;
            this.checkBox11.Location = new System.Drawing.Point(605, 226);
            this.checkBox11.Name = "checkBox11";
            this.checkBox11.Size = new System.Drawing.Size(15, 14);
            this.checkBox11.TabIndex = 16;
            this.checkBox11.UseVisualStyleBackColor = true;
            // 
            // checkBox12
            // 
            this.checkBox12.AutoSize = true;
            this.checkBox12.Location = new System.Drawing.Point(605, 203);
            this.checkBox12.Name = "checkBox12";
            this.checkBox12.Size = new System.Drawing.Size(15, 14);
            this.checkBox12.TabIndex = 15;
            this.checkBox12.UseVisualStyleBackColor = true;
            // 
            // checkBox13
            // 
            this.checkBox13.AutoSize = true;
            this.checkBox13.Location = new System.Drawing.Point(605, 180);
            this.checkBox13.Name = "checkBox13";
            this.checkBox13.Size = new System.Drawing.Size(15, 14);
            this.checkBox13.TabIndex = 14;
            this.checkBox13.UseVisualStyleBackColor = true;
            // 
            // checkBox14
            // 
            this.checkBox14.AutoSize = true;
            this.checkBox14.Location = new System.Drawing.Point(605, 157);
            this.checkBox14.Name = "checkBox14";
            this.checkBox14.Size = new System.Drawing.Size(15, 14);
            this.checkBox14.TabIndex = 13;
            this.checkBox14.UseVisualStyleBackColor = true;
            // 
            // checkBox15
            // 
            this.checkBox15.AutoSize = true;
            this.checkBox15.Location = new System.Drawing.Point(605, 134);
            this.checkBox15.Name = "checkBox15";
            this.checkBox15.Size = new System.Drawing.Size(15, 14);
            this.checkBox15.TabIndex = 12;
            this.checkBox15.UseVisualStyleBackColor = true;
            // 
            // checkBox16
            // 
            this.checkBox16.AutoSize = true;
            this.checkBox16.Location = new System.Drawing.Point(605, 111);
            this.checkBox16.Name = "checkBox16";
            this.checkBox16.Size = new System.Drawing.Size(15, 14);
            this.checkBox16.TabIndex = 11;
            this.checkBox16.UseVisualStyleBackColor = true;
            // 
            // checkBox17
            // 
            this.checkBox17.AutoSize = true;
            this.checkBox17.Location = new System.Drawing.Point(605, 88);
            this.checkBox17.Name = "checkBox17";
            this.checkBox17.Size = new System.Drawing.Size(15, 14);
            this.checkBox17.TabIndex = 10;
            this.checkBox17.UseVisualStyleBackColor = true;
            // 
            // checkBox18
            // 
            this.checkBox18.AutoSize = true;
            this.checkBox18.Location = new System.Drawing.Point(605, 65);
            this.checkBox18.Name = "checkBox18";
            this.checkBox18.Size = new System.Drawing.Size(15, 14);
            this.checkBox18.TabIndex = 8;
            this.checkBox18.UseVisualStyleBackColor = true;
            // 
            // checkBox19
            // 
            this.checkBox19.AutoSize = true;
            this.checkBox19.Location = new System.Drawing.Point(605, 65);
            this.checkBox19.Name = "checkBox19";
            this.checkBox19.Size = new System.Drawing.Size(15, 14);
            this.checkBox19.TabIndex = 9;
            this.checkBox19.UseVisualStyleBackColor = true;
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(26, 97);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(197, 23);
            this.button3.TabIndex = 17;
            this.button3.Text = "Подключить как приемник";
            this.button3.UseVisualStyleBackColor = true;
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(26, 155);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(197, 23);
            this.button4.TabIndex = 18;
            this.button4.Text = "Подключть как приемник ";
            this.button4.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(416, 65);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(65, 13);
            this.label2.TabIndex = 19;
            this.label2.Text = "Датчик A\\0";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(416, 89);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(65, 13);
            this.label3.TabIndex = 20;
            this.label3.Text = "Датчик B\\1";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(416, 111);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(65, 13);
            this.label4.TabIndex = 21;
            this.label4.Text = "Датчик C\\2";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(416, 134);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(66, 13);
            this.label5.TabIndex = 22;
            this.label5.Text = "Датчик D\\3";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(415, 158);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(65, 13);
            this.label6.TabIndex = 23;
            this.label6.Text = "Датчик E\\4";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(415, 181);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(64, 13);
            this.label7.TabIndex = 24;
            this.label7.Text = "Датчик F\\5";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(415, 204);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(66, 13);
            this.label8.TabIndex = 25;
            this.label8.Text = "Датчик G\\6";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(416, 227);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(66, 13);
            this.label9.TabIndex = 26;
            this.label9.Text = "Датчик H\\7";
            // 
            // button5
            // 
            this.button5.Location = new System.Drawing.Point(26, 126);
            this.button5.Name = "button5";
            this.button5.Size = new System.Drawing.Size(197, 23);
            this.button5.TabIndex = 27;
            this.button5.Text = "Отключить";
            this.button5.UseVisualStyleBackColor = true;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(477, 49);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(65, 13);
            this.label10.TabIndex = 28;
            this.label10.Text = "Приемники";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(574, 49);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(61, 13);
            this.label11.TabIndex = 29;
            this.label11.Text = "Источники";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(45, 33);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(127, 13);
            this.label12.TabIndex = 30;
            this.label12.Text = "Введите номер датчика";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(178, 27);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(100, 20);
            this.textBox1.TabIndex = 31;
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(403, 27);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(221, 13);
            this.label16.TabIndex = 32;
            this.label16.Text = "Роли датчиков в автоматическом режиме";
            // 
            // PS5000ABlockForm
            // 
            this.ClientSize = new System.Drawing.Size(671, 358);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.tabControl1);
            this.Name = "PS5000ABlockForm";
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.ResumeLayout(false);

        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }
        public void CollectData()
        {
            all = int.Parse(textBox11.Text);
            for (uint i = 0; i < uint.Parse(textBox11.Text); i++)
            {
                save = (int)i+1; 
                start(uint.Parse(textBox13.Text), uint.Parse(textBox10.Text), 1);
            }

        }

        private void button2_Click(object sender, EventArgs e)
        {
            timer1.Enabled = true;
            CollectData();
            timer1.Enabled = false;
        }

        private void timer1_Tick_1(object sender, EventArgs e)
        {
            Invalidate();
            Application.DoEvents();


        }

        private async void buttonStart_Click(object sender, EventArgs e)
        {

            ////////////////////SetFiles();
            ////////////////////arrA = new double[n];
            ////////////////////arrB = new double[n];
            ////////////////////arrC = new double[n];
            ////////////////////arrD = new double[n];
            ////////////////////masA = new long[n];
            ////////////////////masB = new long[n];
            ////////////////////masC = new long[n];
            ////////////////////masD = new long[n];
            ////////////////////all = countz;
            ////////////////////timer1.Start();
            ////////////////////for(int i=0;i<countz;i++)
            ////////////////////{
            ////////////////////   toolStripStatusLabel1.Text=$"Замер {i+1} выполняется";
            ////////////////////    save = i;
            ////////////////////    await System.Threading.Tasks.Task.Run(() =>start(n,250));
                
            ////////////////////}
            ////////////////////toolStripStatusLabel1.Text = "Замеры выполнены";
            ////////////////////timer1.Stop();

            ////////////////////double middleA = 0;
            ////////////////////double middleB = 0;
            ////////////////////double middleC = 0;
            ////////////////////double middleD = 0;
            ////////////////////for (int i = 0; i < n; i++)
            ////////////////////{

            ////////////////////    arrA[i] = ((double)masA[i]) / ((double)countz) / ((double)32767) * 0.5 * 1000;
            ////////////////////    middleA += arrA[i];
            ////////////////////    arrB[i] = ((double)masB[i]) / ((double)countz) / ((double)32767) * 0.5 * 1000;
            ////////////////////    middleB += arrB[i];
            ////////////////////    arrC[i] = ((double)masC[i]) / ((double)countz) / ((double)32767) * 0.5 * 1000;
            ////////////////////    middleC += arrC[i];
            ////////////////////    arrD[i] = ((double)masD[i]) / ((double)countz) / ((double)32767) * 0.5 * 1000;
            ////////////////////    middleD += arrD[i];
            ////////////////////}
            ////////////////////middleA /= (double)n;
            ////////////////////middleB /= (double)n;
            ////////////////////middleC /= (double)n;
            ////////////////////middleD /= (double)n;
            ////////////////////for (int i = 0; i < n; i++)
            ////////////////////{
            ////////////////////    arrA[i] -= middleA;
            ////////////////////    arrB[i] -= middleB;
            ////////////////////    arrC[i] -= middleC;
            ////////////////////    arrD[i] -= middleD;
            ////////////////////}

            ////////////////////string filenameA = "ArrayA.txt";
            ////////////////////string filenameB = "ArrayB.txt";
            ////////////////////string filenameC = "ArrayC.txt";
            ////////////////////string filenameD = "ArrayD.txt";
            ////////////////////using (StreamWriter fs=new StreamWriter(filenameA))
            ////////////////////{
            ////////////////////    for (int i = 0; i < n; i++)
            ////////////////////        fs.WriteLine(arrA[i].ToString().Replace(',','.'));
            ////////////////////}
            ////////////////////using (StreamWriter fs = new StreamWriter(filenameB))
            ////////////////////{
            ////////////////////    for (int i = 0; i < n; i++)
            ////////////////////        fs.WriteLine(arrB[i].ToString().Replace(',', '.'));
            ////////////////////}
            ////////////////////using (StreamWriter fs = new StreamWriter(filenameC))
            ////////////////////{
            ////////////////////    for (int i = 0; i < n; i++)
            ////////////////////        fs.WriteLine(arrC[i].ToString().Replace(',', '.'));
            ////////////////////}
            ////////////////////using (StreamWriter fs = new StreamWriter(filenameD))
            ////////////////////{
            ////////////////////    for (int i = 0; i < n; i++)
            ////////////////////        fs.WriteLine(arrD[i].ToString().Replace(',', '.'));
            ////////////////////}

            ////////////////////toolStripStatusLabel1.Text = "Запущено преобразование Фурье";
            ////////////////////double dddf =( w1 / 2 / Math.PI * 1.0E6 - w0 / 2 / Math.PI * 1.0E6)/ (double)l;
            ////////////////////CalcTransform(w0/ 2 / Math.PI*1.0E6, w1 / 2 / Math.PI * 1.0E6, l);
            ////////////////////// CalcTransform(5E3, 1.1E5, 2E2);

            ////////////////////buttonOpen_Click(new object(), new EventArgs());
            //////////////////////Process.Start(filenames[0]);
            //////////////////////Process.Start(filenames[1]);
            //////////////////////Process.Start(filenames[2]);
            //////////////////////Process.Start(filenames[3]);
            ////////////////////this.Close();
        }

    }
}