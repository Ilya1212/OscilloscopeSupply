﻿

using PicoPinnedArray;
using PicoStatus;
using PS5000AImports;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PS5000A
{
    public partial class PS5000ABlockForm : Form
    {
        private Switch Switch1;
        private bool stop_flag = false;
        private bool switch_connected = false;
        private double oscilloscope_timestep = 0;
        private string CODES = "ABCDEFGHIKJLMNOP";

        private short _handle;
        public const int BUFFER_SIZE = 1024;
        public const int MAX_CHANNELS = 4;
        public const int QUAD_SCOPE = 4;
        public const int DUAL_SCOPE = 2;
        private uint _timebase = 15;
        private ushort[] inputRanges = { 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000 };
        private bool _ready = false;
        //private ChannelSettings[] _channelSettings;
        private int _channelCount;
        private Imports.ps5000aBlockReady _callbackDelegate;
        private short[] minBuffersA;
        private short[] maxBuffersA;
        private long[] masA;
        private uint n = 10000;
        private double dt_ = 104 * 1.0E-9;
        private double[] arrA;

        /*
         * частоты от 0 Гц 10Mhz
         * dt =10^-7
         * df = 100;
         */
        //private Complex[] Transform_dataA;

        public PS5000ABlockForm()
        {
            InitializeComponent();

            comboRangeA.DataSource = System.Enum.GetValues(typeof(Imports.Range));
            progressBar1.Text = "Готов к работе";
            timer1.Interval = 300;
            timer1.Tick += new EventHandler(Timer1_Tick);
        }

        private int all, save = 0;
        private void Timer1_Tick(object Sender, EventArgs e)
        {
            progressBar1.Value = (int)(((double)save) / all * progressBar1.Maximum);
            Refresh();

        }

        private const double M_PI = 3.1415926535897932384626433832795;
        private void SaveFilter(string filename, double[] filter, double[] x_arg)
        {
            Complex[] buf = new Complex[x_arg.Length];
            for (int i = 0; i < x_arg.Length; i++)
            {
                buf[i] = new Complex(x_arg[i], filter[i]);
            }
            Save2File(filename, buf);
        }
        private Complex[] LoadFilter(string filename)
        {
            return LoadFromFileC(filename);
        }

        //единственная дебажная версия
        //работает
        public double[] FuncMult(double[] f1, double f1dx, double f1x0, Complex[] filter)
        {
            double[] result = new double[f1.Length];
            double mult = 0;
            int j = 0;
            for (int i = 0; i < f1.Length; i++)
            {
                double x = f1x0 + f1dx * (double)i;
                if (x < filter[0].Real)
                {
                    mult = filter[0].Imaginary;
                }
                else
                {
                    if (x > filter[filter.Length - 1].Real)
                    {
                        mult = filter[filter.Length - 1].Imaginary;
                    }

                    else
                    {
                        while (filter[j + 1].Real < x)
                        {
                            j++;
                        }

                        double a = filter[j].Imaginary;
                        double b = (filter[j + 1].Imaginary - filter[j].Imaginary) / (filter[j + 1].Real - filter[j].Real);
                        double x_ = x - filter[j].Real;
                        mult = a + b * x_;


                        //============================================================================
                        //где нарушена логика, надо переписать
                        //============================================================================

                        //while ((filter[j].Real < x) && (j < (filter.Length - 1)))
                        //{
                        //    j++;
                        //}
                        //if (j == (filter.Length - 1))
                        //{
                        //    mult = filter[j].Real;
                        //}
                        //else
                        //{
                        //    double b = (filter[j + 1].Imaginary - filter[j].Imaginary) / (filter[j + 1].Real - filter[j].Real);
                        //    double a = filter[j].Imaginary;
                        //    double x_ = x - filter[j].Real;
                        //    mult = a + b * x_;
                        //}
                    }
                }
                result[i] = mult * f1[i];
            }
            return result;
        }


        public Complex[] FuncMult(Complex[] f1, double f1dx, double f1x0, Complex[] filter)
        {
            Complex[] result = new Complex[f1.Length];
            double mult = 0;
            int j = 0;
            for (int i = 0; i < f1.Length; i++)
            {
                double x = f1x0 + f1dx * (double)i;
                if (x < filter[0].Real)
                {
                    mult = filter[0].Imaginary;
                }
                else
                {
                    if (x > filter[filter.Length - 1].Real)
                    {
                        mult = filter[filter.Length - 1].Imaginary;
                    }

                    else
                    {
                        while (filter[j + 1].Real < x)
                        {
                            j++;
                        }

                        double a = filter[j].Imaginary;
                        double b = (filter[j + 1].Imaginary - filter[j].Imaginary) / (filter[j + 1].Real - filter[j].Real);
                        double x_ = x - filter[j].Real;
                        mult = a + b * x_;
                    }
                }
                result[i] = mult * f1[i];
            }

            return result;
        }



        private Complex[] FurieTransf(double[] data, double dt, double t0, double f0, double df, int nf)
        {
            double w0 = 2 * M_PI * f0;
            double dw = 2 * M_PI * df;
            double mult = Math.Sqrt(1.0 / 2.0 / M_PI);
            Complex[] result = new Complex[nf];
            double w = w0;
            int l = data.Length;
            for (int j = 0; j < nf; j++)
            {
                Complex base_exp = Complex.Exp(-1 * w * t0 * Complex.ImaginaryOne);
                Complex mult_exp = Complex.Exp(-1 * w * dt * Complex.ImaginaryOne);
                Complex _exp = base_exp * mult_exp;
                result[j] = (data[0] * base_exp +
                data[l - 1] * Complex.Exp(-1 * w * (t0 + (double)(l - 1) * dt) * Complex.ImaginaryOne)) / 2.0;
                for (int k = 1; k < l - 1; k++)
                {
                    result[j] = result[j] + data[k] * _exp;
                    _exp = _exp * mult_exp;
                }
                result[j] = dt * result[j] * mult;
                w = w + dw;
            }
            return result;
        }

        private Complex[] FurieTransfReverse(Complex[] data, double dt, double t0, int nt, double f0, double df)
        {
            double w0 = 2 * M_PI * f0;
            double dw = 2 * M_PI * df;
            double mult = Math.Sqrt(1.0 / 2.0 / M_PI);
            Complex[] result = new Complex[nt];
            double t = t0;
            int l = data.Length;
            for (int j = 0; j < nt; j++)
            {
                Complex base_exp = Complex.Exp(t * w0 * Complex.ImaginaryOne);
                Complex mult_exp = Complex.Exp(t * dw * Complex.ImaginaryOne);
                Complex _exp = base_exp * mult_exp;
                result[j] = (data[0] * base_exp +
                data[l - 1] * Complex.Exp(t * (w0 + (double)(l - 1) * dw)
                * Complex.ImaginaryOne)) / 2.0;
                for (int k = 1; k < l - 1; k++)
                {
                    result[j] = result[j] + data[k] * _exp;
                    _exp = _exp * mult_exp;
                }
                result[j] = dw * result[j] * mult;
                t = t + dt;
            }
            return result;
        }

        private void BlockCallback(short handle, short status, IntPtr pVoid)
        {// flag to say done reading data
            if (status != (short)StatusCodes.PICO_CANCELLED)
            {
                _ready = true;
            }
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


            oscilloscope_timestep = double.Parse(textBox9.Text);
            oscilloscope_timestep = (oscilloscope_timestep - 3.0) / 62500000.0;

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

        private void Save2File(string filename, double[] data)
        {
            using (StreamWriter Writer = new StreamWriter(filename))
            {
                Writer.WriteLine(data.Length);
                for (int i = 0; i < data.Length; i++)
                {
                    Writer.WriteLine(data[i].ToString().Replace(',', '.'));
                }
                Writer.Flush();
                Writer.Close();
            }
        }
        private double[] LoadFromFile(string filename)
        {
            using (StreamReader Reader = new StreamReader(filename))
            {
                int l = int.Parse(Reader.ReadLine());
                double[] A = new double[l];
                for (int i = 0; i < l; i++)
                {
                    A[i] = double.Parse(Reader.ReadLine().Replace('.', ','));
                }
                Reader.Close();
                return A;
            }
        }
        private Complex[] LoadFromFileC(string filename)
        {
            using (StreamReader Reader = new StreamReader(filename))
            {
                int l = int.Parse(Reader.ReadLine());
                Complex[] A = new Complex[l];
                for (int i = 0; i < l; i++)
                {
                    A[i] = Str2Cmpl(Reader.ReadLine());
                    //   A[i] = Complex.Parse(Reader.ReadLine().Replace('.', ','));
                }
                Reader.Close();
                return A;
            }
        }

        private double[] ExtractFilterArgs(Complex[] f)
        {
            double[] r = new double[f.Length];
            for (int i = 0; i < f.Length; i++)
            {
                r[i] = f[i].Real;
            }
            return r;
        }

        private double[] ExtractFilterVals(Complex[] f)
        {
            double[] r = new double[f.Length];
            for (int i = 0; i < f.Length; i++)
            {
                r[i] = f[i].Imaginary;
            }
            return r;
        }


        private void Save2File(string filename, Complex[] data)
        {
            using (StreamWriter Writer = new StreamWriter(filename))
            {
                Writer.WriteLine(data.Length);
                for (int i = 0; i < data.Length; i++)
                {
                    Writer.WriteLine(data[i].ToString().Replace(',', '.'));
                }
                Writer.Flush();
                Writer.Close();
            }
        }

        private void SortFilterPoints(ref double[] x, ref double[] f)
        {
            int l = x.Length;
            for (int i = 0; i < l - 1; i++)
            {
                double xmin = x[i];
                int index = i;
                for (int j = i + 1; j < l; j++)
                {
                    if (xmin > f[j])
                    {
                        xmin = f[j];
                        index = j;
                    }
                }
                if (index != i)
                {
                    double x_ = x[index];
                    double f_ = f[index];
                    x[index] = x[i];
                    f[index] = f[i];
                    x[i] = x_;
                    f[i] = f_;
                }
            }
        }

        private bool visualising_now = false;

        private void Visualase(Color color, double[] data, int page_num)
        {
            if (!visualising_now)
            {
                Bitmap box = new Bitmap(tabControl1.TabPages[page_num].Width, tabControl1.TabPages[page_num].Height);
                Graphics g = Graphics.FromImage(box);

                visualising_now = true;
                //   tabControl1.TabPages[5].Invalidate();
                //Graphics e_ = tabControl1.TabPages[5].CreateGraphics();
                int sy = tabControl1.TabPages[page_num].Height;
                int sx = tabControl1.TabPages[page_num].Width;
                int l = data.Length;
                double mult = (double)sx / (double)l;
                Pen pp = new Pen(color);
                double max_abs = 0;
                //for (int i = 0; i < sx - 1; i++)
                for (int i = 0; i < l; i++)
                {
                    //if (max_abs < Math.Abs(data[(int)(i / mult)] - data[0]))
                    //{
                    //    max_abs = Math.Abs(data[(int)(i / mult)] - data[0]);
                    //}
                    //if (max_abs < Math.Abs(data[i] - data[0]))
                    //{
                    //    max_abs = Math.Abs(data[i] - data[0]);
                    //}
                    if (max_abs < Math.Abs(data[i]))
                    {
                        max_abs = Math.Abs(data[i]);
                    }
                }

                //for (int i = 0; i < l - 1; i++)
                //{
                //    int x0 = (int)((double)i * mult);
                //    int x1 = (int)((double)(i + 1) * mult);
                //    int y0 = (int)(data[i] / 65535.0 * (double)sy * 0.8 / 2 * 100 + (double)sy / 2.0);
                //    int y1 = (int)(data[i + 1] / 65535.0 * (double)sy * 0.8 / 2 * 100 + (double)sy / 2.0);
                //    e_.DrawLine(pp, x0, y0, x1, y1);
                //}
                //    for (int i = 0; i < sx - 1; i++)
                //{
                //    int x0 = (int)((double)i * mult);
                //    int x1 = (int)((double)(i + 1) * mult);
                //    int y0 = (int)((data[i] - 32768 )/ max_abs * (double)sy * 0.8 / 2.0  + (double)sy / 2.0);
                //    int y1 = (int)((data[i + 1] - 32768 )/ max_abs * (double)sy * 0.8 / 2.0  + (double)sy / 2.0);
                //    e_.DrawLine(pp, x0, y0, x1, y1);
                //}
                //if (max_abs != 0)
                //{
                //    for (int i = 0; i < sx - 1; i++)
                //    {
                //        int x0 = i;
                //        int x1 = i + 1;
                //        int y0 = (int)((data[(int)(i / mult)] - data[0]) / max_abs * (double)sy * 0.8 / 2.0 + (double)sy / 2.0);
                //        int y1 = (int)((data[(int)((i + 1) / mult)] - data[0]) / max_abs * (double)sy * 0.8 / 2.0 + (double)sy / 2.0);
                //        g.DrawLine(pp, x0, y0, x1, y1);
                //    }
                //}
                //pictureBox1.Image = box;
                if (max_abs != 0)
                {
                    for (int i = 0; i < l - 1; i++)
                    {
                        int x0 = (int)((double)i * mult);
                        int x1 = (int)((double)(i + 1) * mult);
                        int y0 = sy - ((int)(data[i] / max_abs * (double)sy * 0.8 / 2 + (double)sy / 2.0));
                        int y1 = sy - ((int)(data[i + 1] / max_abs * (double)sy * 0.8 / 2 + (double)sy / 2.0));

                        g.DrawLine(pp, x0, y0, x1, y1);
                    }
                }

                pictureBox1.Image = box;

            }
            //Brush ff;
            //e_.FillPolygon()
            //Graphics g = this.CreateGraphics();
            //g.DrawLine(new Pen(Color.Red), 0, 0, 100, 100);

            visualising_now = false;
        }

        //private void Visualase(Color color, long[] data)
        //{
        //    //tabControl1.TabPages[5].Invalidate();
        //    Graphics e_ = tabControl1.TabPages[5].CreateGraphics();
        //    int sy = tabControl1.TabPages[5].Height;
        //    int sx = tabControl1.TabPages[5].Width;
        //    int l = data.Length;
        //    double mult = (double)sx / (double)l;
        //    Pen pp = new Pen(color);
        //    double max_abs = 0;

        //    for (int i = 0; i < sx - 1; i++)
        //    {
        //        if (max_abs < Math.Abs(data[(int)(i / mult)] - data[0]))
        //        {
        //            max_abs = Math.Abs(data[(int)(i / mult)] - data[0]);
        //        }
        //    }

        //    //for (int i = 0; i < l - 1; i++)
        //    //{
        //    //    if (max_abs < Math.Abs(data[i] - data[0]))
        //    //        max_abs = Math.Abs(data[i] - data[0]);
        //    //}


        //    for (int i = 0; i < sx - 1; i++)
        //    {
        //        int x0 = i;
        //        int x1 = i + 1;
        //        int y0 = (int)((data[(int)(i / mult)] - data[0]) / max_abs * (double)sy * 0.8 / 2.0 + (double)sy / 2.0);
        //        int y1 = (int)((data[(int)((i + 1) / mult)] - data[0]) / max_abs * (double)sy * 0.8 / 2.0 + (double)sy / 2.0);
        //        e_.DrawLine(pp, x0, y0, x1, y1);
        //    }
        //}
        private void Visualase(Color color, long[] data)
        {
            if (!visualising_now)
            {
                double[] dadada = new double[data.Length];
                for (int i = 0; i < data.Length; i++)
                {
                    dadada[i] = (double)data[i];
                }
                Visualase(color, dadada, 5);
            }
        }
        private void start(uint sampleCountBefore = 50000, uint sampleCountAfter = 50000, int write_every = 100)
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
            minBuffersA = new short[all_];
            maxBuffersA = new short[all_];
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
                    {
                        masA[x] += maxBuffersA[x] + minBuffersA[x];//=========================================================!
                    }
                }

            }

            Imports.Stop(_handle);
            foreach (PinnedArray<short> p in minPinned)
            {
                if (p != null)
                {
                    p.Dispose();
                }
            }
            foreach (PinnedArray<short> p in maxPinned)
            {
                if (p != null)
                {
                    p.Dispose();
                }
            }

            Application.DoEvents();
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.button13 = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.comboRangeA = new System.Windows.Forms.ComboBox();
            this.textBox9 = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.checkedListBox2 = new System.Windows.Forms.CheckedListBox();
            this.checkedListBox1 = new System.Windows.Forms.CheckedListBox();
            this.textBoxUnitInfo = new System.Windows.Forms.TextBox();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.button7 = new System.Windows.Forms.Button();
            this.button6 = new System.Windows.Forms.Button();
            this.label16 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.button5 = new System.Windows.Forms.Button();
            this.label9 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.button4 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.checkBox10 = new System.Windows.Forms.CheckBox();
            this.textBox6 = new System.Windows.Forms.TextBox();
            this.textBox5 = new System.Windows.Forms.TextBox();
            this.textBox4 = new System.Windows.Forms.TextBox();
            this.label22 = new System.Windows.Forms.Label();
            this.label19 = new System.Windows.Forms.Label();
            this.label18 = new System.Windows.Forms.Label();
            this.checkBox6 = new System.Windows.Forms.CheckBox();
            this.checkBox5 = new System.Windows.Forms.CheckBox();
            this.label17 = new System.Windows.Forms.Label();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.checkBox2 = new System.Windows.Forms.CheckBox();
            this.button11 = new System.Windows.Forms.Button();
            this.checkBox20 = new System.Windows.Forms.CheckBox();
            this.button8 = new System.Windows.Forms.Button();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.textBox14 = new System.Windows.Forms.TextBox();
            this.label21 = new System.Windows.Forms.Label();
            this.textBox13 = new System.Windows.Forms.TextBox();
            this.label20 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.textBox11 = new System.Windows.Forms.TextBox();
            this.textBox10 = new System.Windows.Forms.TextBox();
            this.label14 = new System.Windows.Forms.Label();
            this.button2 = new System.Windows.Forms.Button();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.textBox16 = new System.Windows.Forms.TextBox();
            this.checkBox9 = new System.Windows.Forms.CheckBox();
            this.textBox15 = new System.Windows.Forms.TextBox();
            this.checkBox8 = new System.Windows.Forms.CheckBox();
            this.checkBox4 = new System.Windows.Forms.CheckBox();
            this.checkBox3 = new System.Windows.Forms.CheckBox();
            this.button10 = new System.Windows.Forms.Button();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.button9 = new System.Windows.Forms.Button();
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.textBox12 = new System.Windows.Forms.TextBox();
            this.label25 = new System.Windows.Forms.Label();
            this.checkBox7 = new System.Windows.Forms.CheckBox();
            this.button12 = new System.Windows.Forms.Button();
            this.textBox8 = new System.Windows.Forms.TextBox();
            this.label24 = new System.Windows.Forms.Label();
            this.label23 = new System.Windows.Forms.Label();
            this.textBox7 = new System.Windows.Forms.TextBox();
            this.tabPage6 = new System.Windows.Forms.TabPage();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.tabPage5.SuspendLayout();
            this.tabPage6.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Controls.Add(this.tabPage5);
            this.tabControl1.Controls.Add(this.tabPage6);
            this.tabControl1.Location = new System.Drawing.Point(3, 2);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(668, 333);
            this.tabControl1.TabIndex = 0;
            this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.button13);
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
            // button13
            // 
            this.button13.Location = new System.Drawing.Point(355, 192);
            this.button13.Name = "button13";
            this.button13.Size = new System.Drawing.Size(75, 23);
            this.button13.TabIndex = 27;
            this.button13.Text = "button13";
            this.button13.UseVisualStyleBackColor = true;
            this.button13.Click += new System.EventHandler(this.button13_Click);
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
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.checkedListBox2);
            this.tabPage3.Controls.Add(this.checkedListBox1);
            this.tabPage3.Controls.Add(this.textBoxUnitInfo);
            this.tabPage3.Controls.Add(this.listBox1);
            this.tabPage3.Controls.Add(this.button7);
            this.tabPage3.Controls.Add(this.button6);
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
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(660, 307);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Коммутация";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // checkedListBox2
            // 
            this.checkedListBox2.FormattingEnabled = true;
            this.checkedListBox2.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7"});
            this.checkedListBox2.Location = new System.Drawing.Point(553, 65);
            this.checkedListBox2.Name = "checkedListBox2";
            this.checkedListBox2.Size = new System.Drawing.Size(54, 124);
            this.checkedListBox2.TabIndex = 38;
            // 
            // checkedListBox1
            // 
            this.checkedListBox1.FormattingEnabled = true;
            this.checkedListBox1.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7"});
            this.checkedListBox1.Location = new System.Drawing.Point(487, 65);
            this.checkedListBox1.Name = "checkedListBox1";
            this.checkedListBox1.Size = new System.Drawing.Size(54, 124);
            this.checkedListBox1.TabIndex = 37;
            // 
            // textBoxUnitInfo
            // 
            this.textBoxUnitInfo.Location = new System.Drawing.Point(216, 16);
            this.textBoxUnitInfo.Multiline = true;
            this.textBoxUnitInfo.Name = "textBoxUnitInfo";
            this.textBoxUnitInfo.Size = new System.Drawing.Size(184, 275);
            this.textBoxUnitInfo.TabIndex = 36;
            // 
            // listBox1
            // 
            this.listBox1.FormattingEnabled = true;
            this.listBox1.Location = new System.Drawing.Point(143, 16);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(68, 69);
            this.listBox1.TabIndex = 35;
            // 
            // button7
            // 
            this.button7.Location = new System.Drawing.Point(13, 46);
            this.button7.Name = "button7";
            this.button7.Size = new System.Drawing.Size(124, 23);
            this.button7.TabIndex = 34;
            this.button7.Text = "Подключить порт";
            this.button7.UseVisualStyleBackColor = true;
            this.button7.Click += new System.EventHandler(this.button7_Click);
            // 
            // button6
            // 
            this.button6.Location = new System.Drawing.Point(13, 16);
            this.button6.Name = "button6";
            this.button6.Size = new System.Drawing.Size(124, 23);
            this.button6.TabIndex = 33;
            this.button6.Text = "Список портов";
            this.button6.UseVisualStyleBackColor = true;
            this.button6.Click += new System.EventHandler(this.button6_Click);
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
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(143, 91);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(67, 20);
            this.textBox1.TabIndex = 31;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(10, 98);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(127, 13);
            this.label12.TabIndex = 30;
            this.label12.Text = "Введите номер датчика";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(546, 49);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(61, 13);
            this.label11.TabIndex = 29;
            this.label11.Text = "Источники";
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
            // button5
            // 
            this.button5.Location = new System.Drawing.Point(13, 153);
            this.button5.Name = "button5";
            this.button5.Size = new System.Drawing.Size(197, 23);
            this.button5.TabIndex = 27;
            this.button5.Text = "Отключить";
            this.button5.UseVisualStyleBackColor = true;
            this.button5.Click += new System.EventHandler(this.button5_Click);
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(414, 173);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(66, 13);
            this.label9.TabIndex = 26;
            this.label9.Text = "Датчик H\\7";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(414, 158);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(66, 13);
            this.label8.TabIndex = 25;
            this.label8.Text = "Датчик G\\6";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(415, 144);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(64, 13);
            this.label7.TabIndex = 24;
            this.label7.Text = "Датчик F\\5";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(415, 129);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(65, 13);
            this.label6.TabIndex = 23;
            this.label6.Text = "Датчик E\\4";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(416, 113);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(66, 13);
            this.label5.TabIndex = 22;
            this.label5.Text = "Датчик D\\3";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(416, 97);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(65, 13);
            this.label4.TabIndex = 21;
            this.label4.Text = "Датчик C\\2";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(415, 82);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(65, 13);
            this.label3.TabIndex = 20;
            this.label3.Text = "Датчик B\\1";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(414, 68);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(65, 13);
            this.label2.TabIndex = 19;
            this.label2.Text = "Датчик A\\0";
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(13, 182);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(197, 23);
            this.button4.TabIndex = 18;
            this.button4.Text = "Подключть как приемник ";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(13, 124);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(197, 23);
            this.button3.TabIndex = 17;
            this.button3.Text = "Подключить как источник";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.checkBox10);
            this.tabPage2.Controls.Add(this.textBox6);
            this.tabPage2.Controls.Add(this.textBox5);
            this.tabPage2.Controls.Add(this.textBox4);
            this.tabPage2.Controls.Add(this.label22);
            this.tabPage2.Controls.Add(this.label19);
            this.tabPage2.Controls.Add(this.label18);
            this.tabPage2.Controls.Add(this.checkBox6);
            this.tabPage2.Controls.Add(this.checkBox5);
            this.tabPage2.Controls.Add(this.label17);
            this.tabPage2.Controls.Add(this.textBox3);
            this.tabPage2.Controls.Add(this.checkBox2);
            this.tabPage2.Controls.Add(this.button11);
            this.tabPage2.Controls.Add(this.checkBox20);
            this.tabPage2.Controls.Add(this.button8);
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
            // checkBox10
            // 
            this.checkBox10.AutoSize = true;
            this.checkBox10.Checked = true;
            this.checkBox10.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox10.Location = new System.Drawing.Point(463, 48);
            this.checkBox10.Name = "checkBox10";
            this.checkBox10.Size = new System.Drawing.Size(178, 17);
            this.checkBox10.TabIndex = 48;
            this.checkBox10.Text = "Сохранять файл с временами";
            this.checkBox10.UseVisualStyleBackColor = true;
            // 
            // textBox6
            // 
            this.textBox6.Location = new System.Drawing.Point(194, 242);
            this.textBox6.Name = "textBox6";
            this.textBox6.Size = new System.Drawing.Size(100, 20);
            this.textBox6.TabIndex = 47;
            this.textBox6.Text = "1200";
            // 
            // textBox5
            // 
            this.textBox5.Location = new System.Drawing.Point(194, 218);
            this.textBox5.Name = "textBox5";
            this.textBox5.Size = new System.Drawing.Size(100, 20);
            this.textBox5.TabIndex = 46;
            this.textBox5.Text = "250";
            // 
            // textBox4
            // 
            this.textBox4.Location = new System.Drawing.Point(194, 196);
            this.textBox4.Name = "textBox4";
            this.textBox4.Size = new System.Drawing.Size(100, 20);
            this.textBox4.TabIndex = 45;
            this.textBox4.Text = "1000";
            // 
            // label22
            // 
            this.label22.AutoSize = true;
            this.label22.Location = new System.Drawing.Point(8, 245);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(179, 13);
            this.label22.TabIndex = 44;
            this.label22.Text = "Количество шагов по частоте ПФ";
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Location = new System.Drawing.Point(8, 221);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(106, 13);
            this.label19.TabIndex = 43;
            this.label19.Text = "Шаг по частоте ПФ";
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(8, 199);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(111, 13);
            this.label18.TabIndex = 42;
            this.label18.Text = "Нижняя частота ПФ";
            // 
            // checkBox6
            // 
            this.checkBox6.AutoSize = true;
            this.checkBox6.Location = new System.Drawing.Point(11, 179);
            this.checkBox6.Name = "checkBox6";
            this.checkBox6.Size = new System.Drawing.Size(238, 17);
            this.checkBox6.TabIndex = 41;
            this.checkBox6.Text = "Если считается ПФ, то посчитать модуль";
            this.checkBox6.UseVisualStyleBackColor = true;
            // 
            // checkBox5
            // 
            this.checkBox5.AutoSize = true;
            this.checkBox5.Location = new System.Drawing.Point(11, 155);
            this.checkBox5.Name = "checkBox5";
            this.checkBox5.Size = new System.Drawing.Size(88, 17);
            this.checkBox5.TabIndex = 40;
            this.checkBox5.Text = "Считать ПФ";
            this.checkBox5.UseVisualStyleBackColor = true;
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(0, 268);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(128, 13);
            this.label17.TabIndex = 39;
            this.label17.Text = "Путь хранения замеров";
            // 
            // textBox3
            // 
            this.textBox3.Location = new System.Drawing.Point(3, 284);
            this.textBox3.Name = "textBox3";
            this.textBox3.Size = new System.Drawing.Size(654, 20);
            this.textBox3.TabIndex = 38;
            this.textBox3.Text = "C:\\TEMP\\";
            // 
            // checkBox2
            // 
            this.checkBox2.AutoSize = true;
            this.checkBox2.Checked = true;
            this.checkBox2.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox2.Location = new System.Drawing.Point(463, 21);
            this.checkBox2.Name = "checkBox2";
            this.checkBox2.Size = new System.Drawing.Size(169, 17);
            this.checkBox2.TabIndex = 37;
            this.checkBox2.Text = "Подавлять начальную часть";
            this.checkBox2.UseVisualStyleBackColor = true;
            // 
            // button11
            // 
            this.button11.Location = new System.Drawing.Point(7, 6);
            this.button11.Name = "button11";
            this.button11.Size = new System.Drawing.Size(102, 35);
            this.button11.TabIndex = 36;
            this.button11.Text = "Автоматический сбор данных";
            this.button11.UseVisualStyleBackColor = true;
            this.button11.Click += new System.EventHandler(this.button11_Click);
            // 
            // checkBox20
            // 
            this.checkBox20.AutoSize = true;
            this.checkBox20.Location = new System.Drawing.Point(11, 131);
            this.checkBox20.Name = "checkBox20";
            this.checkBox20.Size = new System.Drawing.Size(98, 17);
            this.checkBox20.TabIndex = 35;
            this.checkBox20.Text = "Визуализация";
            this.checkBox20.UseVisualStyleBackColor = true;
            // 
            // button8
            // 
            this.button8.Location = new System.Drawing.Point(7, 48);
            this.button8.Name = "button8";
            this.button8.Size = new System.Drawing.Size(102, 23);
            this.button8.TabIndex = 34;
            this.button8.Text = "Стоп";
            this.button8.UseVisualStyleBackColor = true;
            this.button8.Click += new System.EventHandler(this.button8_Click);
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Checked = true;
            this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox1.Location = new System.Drawing.Point(228, 154);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(240, 17);
            this.checkBox1.TabIndex = 33;
            this.checkBox1.Text = "Ограничение полосы пропускания 20 МГц";
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // textBox14
            // 
            this.textBox14.Location = new System.Drawing.Point(345, 21);
            this.textBox14.Name = "textBox14";
            this.textBox14.Size = new System.Drawing.Size(100, 20);
            this.textBox14.TabIndex = 32;
            this.textBox14.Text = "25100";
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
            this.textBox10.Text = "60000";
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
            this.button2.Enabled = false;
            this.button2.Location = new System.Drawing.Point(529, 245);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(103, 23);
            this.button2.TabIndex = 0;
            this.button2.Text = "Сбор данных";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Visible = false;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.textBox16);
            this.tabPage4.Controls.Add(this.checkBox9);
            this.tabPage4.Controls.Add(this.textBox15);
            this.tabPage4.Controls.Add(this.checkBox8);
            this.tabPage4.Controls.Add(this.checkBox4);
            this.tabPage4.Controls.Add(this.checkBox3);
            this.tabPage4.Controls.Add(this.button10);
            this.tabPage4.Controls.Add(this.textBox2);
            this.tabPage4.Controls.Add(this.button9);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Size = new System.Drawing.Size(660, 307);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "Предобработка";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // textBox16
            // 
            this.textBox16.Location = new System.Drawing.Point(6, 185);
            this.textBox16.Name = "textBox16";
            this.textBox16.Size = new System.Drawing.Size(646, 20);
            this.textBox16.TabIndex = 8;
            // 
            // checkBox9
            // 
            this.checkBox9.AutoSize = true;
            this.checkBox9.Location = new System.Drawing.Point(4, 161);
            this.checkBox9.Name = "checkBox9";
            this.checkBox9.Size = new System.Drawing.Size(246, 17);
            this.checkBox9.TabIndex = 7;
            this.checkBox9.Text = "Использовать фильтр в частотной области";
            this.checkBox9.UseVisualStyleBackColor = true;
            // 
            // textBox15
            // 
            this.textBox15.Location = new System.Drawing.Point(5, 134);
            this.textBox15.Name = "textBox15";
            this.textBox15.Size = new System.Drawing.Size(647, 20);
            this.textBox15.TabIndex = 6;
            this.textBox15.Text = "C:\\TEMP\\my_filter_t.txt";
            // 
            // checkBox8
            // 
            this.checkBox8.AutoSize = true;
            this.checkBox8.Checked = true;
            this.checkBox8.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox8.Location = new System.Drawing.Point(6, 111);
            this.checkBox8.Name = "checkBox8";
            this.checkBox8.Size = new System.Drawing.Size(251, 17);
            this.checkBox8.TabIndex = 5;
            this.checkBox8.Text = "Использовать фильтр в временной области";
            this.checkBox8.UseVisualStyleBackColor = true;
            // 
            // checkBox4
            // 
            this.checkBox4.AutoSize = true;
            this.checkBox4.Checked = true;
            this.checkBox4.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox4.Location = new System.Drawing.Point(229, 58);
            this.checkBox4.Name = "checkBox4";
            this.checkBox4.Size = new System.Drawing.Size(423, 17);
            this.checkBox4.TabIndex = 4;
            this.checkBox4.Text = "Применять устранение средней величины при автоматическом сборе данных";
            this.checkBox4.UseVisualStyleBackColor = true;
            // 
            // checkBox3
            // 
            this.checkBox3.AutoSize = true;
            this.checkBox3.Checked = true;
            this.checkBox3.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox3.Location = new System.Drawing.Point(282, 25);
            this.checkBox3.Name = "checkBox3";
            this.checkBox3.Size = new System.Drawing.Size(356, 17);
            this.checkBox3.TabIndex = 3;
            this.checkBox3.Text = "Применять бегущее среднее при автоматическом сборе данных";
            this.checkBox3.UseVisualStyleBackColor = true;
            // 
            // button10
            // 
            this.button10.Location = new System.Drawing.Point(5, 57);
            this.button10.Name = "button10";
            this.button10.Size = new System.Drawing.Size(132, 37);
            this.button10.TabIndex = 2;
            this.button10.Text = "Устранить среднюю величину";
            this.button10.UseVisualStyleBackColor = true;
            this.button10.Click += new System.EventHandler(this.button10_Click);
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(157, 23);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(100, 20);
            this.textBox2.TabIndex = 1;
            this.textBox2.Text = "10";
            // 
            // button9
            // 
            this.button9.Location = new System.Drawing.Point(5, 14);
            this.button9.Name = "button9";
            this.button9.Size = new System.Drawing.Size(132, 37);
            this.button9.TabIndex = 0;
            this.button9.Text = "Применить бегущее среднее";
            this.button9.UseVisualStyleBackColor = true;
            this.button9.Click += new System.EventHandler(this.button9_Click);
            // 
            // tabPage5
            // 
            this.tabPage5.Controls.Add(this.textBox12);
            this.tabPage5.Controls.Add(this.label25);
            this.tabPage5.Controls.Add(this.checkBox7);
            this.tabPage5.Controls.Add(this.button12);
            this.tabPage5.Controls.Add(this.textBox8);
            this.tabPage5.Controls.Add(this.label24);
            this.tabPage5.Controls.Add(this.label23);
            this.tabPage5.Controls.Add(this.textBox7);
            this.tabPage5.Location = new System.Drawing.Point(4, 22);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Size = new System.Drawing.Size(660, 307);
            this.tabPage5.TabIndex = 4;
            this.tabPage5.Text = "Обработка";
            this.tabPage5.UseVisualStyleBackColor = true;
            // 
            // textBox12
            // 
            this.textBox12.Location = new System.Drawing.Point(181, 66);
            this.textBox12.Name = "textBox12";
            this.textBox12.Size = new System.Drawing.Size(476, 20);
            this.textBox12.TabIndex = 7;
            // 
            // label25
            // 
            this.label25.AutoSize = true;
            this.label25.Location = new System.Drawing.Point(5, 72);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(178, 13);
            this.label25.TabIndex = 6;
            this.label25.Text = "Папка для сохранения разностей";
            // 
            // checkBox7
            // 
            this.checkBox7.AutoSize = true;
            this.checkBox7.Location = new System.Drawing.Point(8, 143);
            this.checkBox7.Name = "checkBox7";
            this.checkBox7.Size = new System.Drawing.Size(241, 17);
            this.checkBox7.TabIndex = 5;
            this.checkBox7.Text = "Нормализовывать перед выводом в файл";
            this.checkBox7.UseVisualStyleBackColor = true;
            // 
            // button12
            // 
            this.button12.Location = new System.Drawing.Point(8, 99);
            this.button12.Name = "button12";
            this.button12.Size = new System.Drawing.Size(167, 23);
            this.button12.TabIndex = 4;
            this.button12.Text = "Построить разности";
            this.button12.UseVisualStyleBackColor = true;
            this.button12.Click += new System.EventHandler(this.button12_Click);
            // 
            // textBox8
            // 
            this.textBox8.Location = new System.Drawing.Point(181, 40);
            this.textBox8.Name = "textBox8";
            this.textBox8.Size = new System.Drawing.Size(476, 20);
            this.textBox8.TabIndex = 3;
            // 
            // label24
            // 
            this.label24.AutoSize = true;
            this.label24.Location = new System.Drawing.Point(5, 46);
            this.label24.Name = "label24";
            this.label24.Size = new System.Drawing.Size(166, 13);
            this.label24.TabIndex = 2;
            this.label24.Text = "Папка с замерами с дефектом";
            // 
            // label23
            // 
            this.label23.AutoSize = true;
            this.label23.Location = new System.Drawing.Point(5, 21);
            this.label23.Name = "label23";
            this.label23.Size = new System.Drawing.Size(170, 13);
            this.label23.TabIndex = 1;
            this.label23.Text = "Папка с замерами без дефекта";
            // 
            // textBox7
            // 
            this.textBox7.Location = new System.Drawing.Point(181, 14);
            this.textBox7.Name = "textBox7";
            this.textBox7.Size = new System.Drawing.Size(476, 20);
            this.textBox7.TabIndex = 0;
            // 
            // tabPage6
            // 
            this.tabPage6.Controls.Add(this.pictureBox1);
            this.tabPage6.Location = new System.Drawing.Point(4, 22);
            this.tabPage6.Name = "tabPage6";
            this.tabPage6.Size = new System.Drawing.Size(660, 307);
            this.tabPage6.TabIndex = 5;
            this.tabPage6.Text = "Визуализация";
            this.tabPage6.UseVisualStyleBackColor = true;
            this.tabPage6.Click += new System.EventHandler(this.tabPage6_Click);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Location = new System.Drawing.Point(0, 0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(660, 307);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
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
            // PS5000ABlockForm
            // 
            this.ClientSize = new System.Drawing.Size(671, 358);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.tabControl1);
            this.Name = "PS5000ABlockForm";
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.tabPage4.ResumeLayout(false);
            this.tabPage4.PerformLayout();
            this.tabPage5.ResumeLayout(false);
            this.tabPage5.PerformLayout();
            this.tabPage6.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }
        public void CollectData()
        {

            masA = new long[uint.Parse(textBox13.Text) + uint.Parse(textBox10.Text)];
            arrA = new double[uint.Parse(textBox13.Text) + uint.Parse(textBox10.Text)];
            all = int.Parse(textBox11.Text);
            for (uint i = 0; i < uint.Parse(textBox11.Text); i++)
            {
                if (stop_flag)
                {
                    break;
                }
                save = (int)i + 1;
                start(uint.Parse(textBox13.Text), uint.Parse(textBox10.Text), 1);
                if (checkBox20.Checked)
                {
                    Visualase(Color.Blue, masA);
                }
            }
            for (uint i = 0; i < uint.Parse(textBox13.Text) + uint.Parse(textBox10.Text); i++)
            {
                if (stop_flag)
                {
                    break;
                }
                arrA[i] = (double)masA[i] / 2.0 / (double)uint.Parse(textBox11.Text) * inputRanges[comboRangeA.SelectedIndex] / 65536.0;


            }


        }

        private double FindAvg(double[] data)
        {
            double a = 0;
            for (int i = 0; i < data.Length; i++)
            {
                a += data[i];
            }
            return a / (double)data.Length;
        }

        private void SuppressSpikes(double[] data, int index)
        {
            double A = FindAvg(data);
            for (int i = 0; i < index; i++)
            {
                data[i] = A;
            }
        }

        private void NoOffset(double[] data)
        {
            double A = FindAvg(data);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] -= A;
            }
        }
        private void button2_Click(object sender, EventArgs e)
        {
            timer1.Enabled = true;
            CollectData();
            if (checkBox2.Checked)
            {
                SuppressSpikes(arrA, int.Parse(textBox14.Text));
            }

            timer1.Enabled = false;
            Save2File(@"C:\Temp\Measurement.txt", arrA);
            // Visualase(Color.Red, arrA);
        }

        private void timer1_Tick_1(object sender, EventArgs e)
        {
            //    Invalidate();
            Application.DoEvents();

        }

        private void button3_Click(object sender, EventArgs e)
        {
            Switch1.SendCmd(0, int.Parse(textBox1.Text));
            Thread.Sleep(500);
            textBoxUnitInfo.AppendText(Switch1.GetAccepted() + "\n");
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Switch1.SendCmd(2, int.Parse(textBox1.Text));
            Thread.Sleep(500);
            textBoxUnitInfo.AppendText(Switch1.GetAccepted() + "\n");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Switch1.SendCmd(1, int.Parse(textBox1.Text));
            Thread.Sleep(500);
            textBoxUnitInfo.AppendText(Switch1.GetAccepted() + "\n");
        }

        private string[] names_;
        private void button6_Click(object sender, EventArgs e)
        {
            names_ = SerialPort.GetPortNames();
            listBox1.Items.Clear();
            listBox1.Items.AddRange(names_);

            if (listBox1.Items.Count > 0)
            {
                listBox1.SelectedIndex = listBox1.Items.Count - 1;
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            Switch1 = new Switch();
            Switch1.OpenPort(listBox1.SelectedIndex);
            Thread.Sleep(500);
            textBoxUnitInfo.AppendText(Switch1.GetAccepted() + "\n");
            switch_connected = true;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (!stop_flag)
            {
                stop_flag = true;
            }

            timer1.Enabled = false;
        }
        private static void RunAvg(ref double[] Array_, int kernel_len = 10)
        {
            double kl = kernel_len;
            double[] Array_buf = new double[Array_.Length];
            for (int i = kernel_len + 1; i < Array_.Length - (kernel_len + 1); i++)
            {
                double summ = 0;
                for (int j = -kernel_len; j < kernel_len / 2; j++)
                {
                    summ += Array_[i + j];
                }
                Array_buf[i] = summ / kl;
            }
            for (int i = kernel_len + 1; i < Array_.Length - (kernel_len + 1); i++)
            {
                Array_[i] = Array_buf[i];
            }

        }
        private void button9_Click(object sender, EventArgs e)
        {
            RunAvg(ref arrA, int.Parse(textBox2.Text));
            Visualase(Color.Red, arrA, 5);
            string path = String.Concat(@"C:\Temp\", DateTime.Now.ToString().Replace(':', '_'), "TempCapture.txt");

            Save2File(path, arrA);

        }

        private void tabPage6_Click(object sender, EventArgs e)
        {
            if (arrA != null)
            {
                Visualase(Color.Red, arrA, 5);
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //if (arrA != null)
            //{
            //    Visualase(Color.Red, arrA, 5);
            //}
        }

        private void button11_Click(object sender, EventArgs e)
        {
            if (switch_connected)
            {
                string dir = String.Concat(textBox3.Text, "/");
                Directory.CreateDirectory(dir);

                //==============================================================
                //вывод файла с временами
                if (checkBox10.Checked)
                {
                    int l = int.Parse(textBox10.Text) + int.Parse(textBox13.Text);
                    double[] dt = new double[l];
                    double n0 = double.Parse(textBox13.Text);
                    for (int i = 0; i < l; i++)
                    {
                        dt[i] = oscilloscope_timestep * i - oscilloscope_timestep * n0;
                    }
                    string fn = "times.txt";
                    Save2File(String.Concat(dir, fn), dt);
                }

                //==============================================================


                for (int i = 0; i < checkedListBox1.CheckedIndices.Count; i++)
                {
                    int j = checkedListBox1.CheckedIndices[i];
                    textBoxUnitInfo.AppendText(Switch1.GetAccepted() + "\n");
                    Switch1.SendCmd(0, j);
                    while (Switch1.port.BytesToRead == 0) { Thread.Sleep(50); };
                    textBoxUnitInfo.AppendText(Switch1.GetAccepted() + "\n");
                    for (int k = 0; k < checkedListBox2.CheckedIndices.Count; k++)
                    {

                        int m = checkedListBox2.CheckedIndices[k];
                        if (m != j)
                        {
                            textBoxUnitInfo.AppendText(Switch1.GetAccepted() + "\n");
                            Switch1.SendCmd(1, m);
                            while (Switch1.port.BytesToRead == 0) { Thread.Sleep(50); };
                            textBoxUnitInfo.AppendText(Switch1.GetAccepted() + "\n");

                            timer1.Enabled = true;
                            CollectData();
                            if (checkBox2.Checked)
                            {
                                SuppressSpikes(arrA, int.Parse(textBox14.Text));
                            }
                            if (checkBox3.Checked)
                            {
                                RunAvg(ref arrA, int.Parse(textBox2.Text));
                            };
                            if (checkBox4.Checked)
                            {
                                NoOffset(arrA);
                            };
                            timer1.Enabled = false;
                            if (stop_flag)
                            {
                                save = 0; stop_flag = false;
                            }
                            dir = String.Concat(textBox3.Text, "/", CODES[j], "/");
                            Directory.CreateDirectory(dir);
                            string fn = String.Concat("raw_", CODES[j], "2", CODES[m], ".txt");
                            Save2File(String.Concat(dir, fn), arrA);
                            //============================================================
                            //вставить применение фильтра
                            if (checkBox8.Checked)
                            {
                                if (textBox15.Text.Length > 0)
                                {
                                    Complex[] filtr = LoadFromFileC(textBox15.Text);
                                    arrA = FuncMult(arrA, oscilloscope_timestep, -oscilloscope_timestep * double.Parse(textBox13.Text), filtr);
                                }
                            }

                            dir = String.Concat(textBox3.Text, "/", CODES[j], "/");
                            Directory.CreateDirectory(dir);
                            fn = String.Concat("afterf_", CODES[j], "2", CODES[m], ".txt");

                            Save2File(String.Concat(dir, fn), arrA);

                            if (checkBox9.Checked)
                            {
                                if (textBox16.Text.Length > 0)
                                {
                                    Complex[] f = FurieTransf(arrA, oscilloscope_timestep, -oscilloscope_timestep * double.Parse(textBox13.Text), 1 * double.Parse(textBox4.Text), double.Parse(textBox5.Text), int.Parse(textBox6.Text));
                                    Complex[] filtr = LoadFromFileC(textBox16.Text);
                                    f = FuncMult(f, double.Parse(textBox4.Text), double.Parse(textBox5.Text), filtr);
                                    Complex[] restored = FurieTransfReverse(f, oscilloscope_timestep, -oscilloscope_timestep * double.Parse(textBox13.Text), arrA.Length, double.Parse(textBox4.Text), double.Parse(textBox5.Text));
                                    for (int k1 = 0; k1 < restored.Length; k1++)
                                    {
                                        arrA[k1] = restored[k1].Real * 2;//важно делать умножение на 2 так как интеграл по полубесконечному промежутку
                                    }
                                }
                            }



                            Visualase(Color.Red, arrA, 5);
                            ///===========================================================

                            //   Save2File(String.Concat(textBox3.Text,"ft_", CODES[j], "2", CODES[m], ".txt"), abs_f);
                            dir = String.Concat(textBox3.Text, "/", CODES[j], "/");
                            Directory.CreateDirectory(dir);
                            fn = String.Concat(CODES[j], "2", CODES[m], ".txt");
                            Save2File(String.Concat(dir, fn), arrA);
                            if (checkBox5.Checked)
                            {
                                Complex[] f = FurieTransf(arrA, oscilloscope_timestep, -oscilloscope_timestep * double.Parse(textBox13.Text), 1 * double.Parse(textBox4.Text), double.Parse(textBox5.Text), int.Parse(textBox6.Text));
                                Save2File(String.Concat(dir, "f_", fn), f);
                                if (checkBox6.Checked)
                                {
                                    double[] abs_f = new double[f.Length];
                                    for (int k1 = 0; k1 < f.Length; k1++)
                                    {
                                        abs_f[k1] = f[k1].Magnitude;
                                    }
                                    Save2File(String.Concat(dir, "abs_f_", fn), abs_f);
                                }

                            }
                        }
                    }
                }
            }
        }

        private void button12_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox1.CheckedIndices.Count; i++)
            {
                int j = checkedListBox1.CheckedIndices[i];
                for (int k = 0; k < checkedListBox2.CheckedIndices.Count; k++)
                {
                    int m = checkedListBox2.CheckedIndices[k];
                    if (m != j)
                    {
                        string dir1 = String.Concat(textBox7.Text, "/", CODES[j], "/");
                        string dir2 = String.Concat(textBox8.Text, "/", CODES[j], "/");
                        string dir3 = String.Concat(textBox12.Text, "/", CODES[j], "/");
                        string fn = String.Concat(CODES[j], "2", CODES[m], ".txt");
                        StreamReader R1 = new StreamReader(String.Concat(dir1, fn));
                        StreamReader R2 = new StreamReader(String.Concat(dir2, fn));
                        int l1 = int.Parse(R1.ReadLine());
                        int l2 = int.Parse(R2.ReadLine());
                        int l = l1;
                        if (l > l2)
                        {
                            l = l2;
                        }
                        Directory.CreateDirectory(dir3);
                        using (StreamWriter Writer = new StreamWriter(String.Concat(dir3, fn)))
                        {
                            Writer.WriteLine(l);
                            for (int n = 0; n < l; n++)
                            {
                                string s1 = R1.ReadLine().Replace('.', ',');
                                double d1 = double.Parse(s1);
                                string s2 = R2.ReadLine().Replace('.', ','); ;
                                double d2 = double.Parse(s2);
                                Writer.WriteLine((d1 - d2).ToString().Replace(',', '.'));
                            }

                            Writer.Flush();
                            Writer.Close();
                        }
                        //while  (!(R1.EndOfStream || R2.EndOfStream  ))
                        //    {
                        //}
                    }
                }
            }
        }

        private Complex Str2Cmpl(string s)
        {
            int pos = s.IndexOf(". ");
            string s1 = s.Substring(1, pos - 1).Replace('.', ',');
            string s2 = s.Substring(pos + 1, s.Length - pos - 3).Replace('.', ',');
            Complex r = new Complex(double.Parse(s1), double.Parse(s2));
            return r;

        }
        private void button13_Click(object sender, EventArgs e)
        {
            double[] filtr = { 1.1, 0.4, 111111.7 };
            double[] xxzx = { 1111111.1, 2.1, 3.2 };
            SaveFilter(@"C:\TEMP\fff.txt", filtr, xxzx);
            Complex[] cc = LoadFromFileC(@"C:\TEMP\fff.txt");


        }

        private void button10_Click(object sender, EventArgs e)
        {
            NoOffset(arrA);
            string path = String.Concat(textBox3.Text, DateTime.Now.ToString().Replace(':', '_'), "TempCapture.txt");
            Save2File(path, arrA);
        }

    }
}