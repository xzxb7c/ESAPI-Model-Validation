using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Microsoft.Win32;
using System.IO;

namespace Model_Validation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        VMS.TPS.Common.Model.API.Application app = VMS.TPS.Common.Model.API.Application.CreateApplication();

        //public patient info
        List<DataScan> ds_list = new List<DataScan>();
        public Patient pat = null;
        public Course course = null;
        public PlanSetup plan = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void patLoad_btn_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(patId_txt.Text))
            {
                MessageBox.Show("Input a patient ID before cling Load Patient.");
            }
            else
            {
                try
                {
                    app.ClosePatient();
                    pat = app.OpenPatientById(patId_txt.Text);
                }
                catch
                {
                    MessageBox.Show("Could not find patient ID");
                }
                if (pat != null)
                {
                    foreach (Course c in pat.Courses)
                    {
                        course_cmb.Items.Add(c.Id);
                    }
                }
                else { MessageBox.Show("Patient ID is Incorrect"); }
            }
        }

        private void course_cmb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            course = pat.Courses.First(x => x.Id == course_cmb.SelectedItem.ToString());
            foreach (PlanSetup ps in course.PlanSetups)
            {
                plan_cmb.Items.Add(ps.Id);
            }
        }

        private void getScan_btn_Click(object sender, RoutedEventArgs e)
        {
            ds_list.Clear();
            OpenFileDialog fdlg = new OpenFileDialog();
            fdlg.DefaultExt = ".asc"; ; //search acsii files
            fdlg.Filter = "Ascii Files (*.asc)|*.asc|Text Files (*.txt)|*.txt|W2CAD Files (*.cdp)|*.cdp";
            if (fdlg.ShowDialog() == true)
            {
                //read out the content here
                foreach (string line in File.ReadAllLines(fdlg.FileName))
                {
                    if (line.Contains("STOM"))
                    {
                        ds_list.Add(new DataScan());
                    }
                    if (line.Contains("FLSZ"))
                    {
                        ds_list.Last().FieldX = Convert.ToDouble(line.Split(' ').Last().Split('*').First());
                        ds_list.Last().FieldY = Convert.ToDouble(line.Split('*').Last());
                    }
                    if (line.Contains("TYPE"))
                    {
                        switch (line.Split(' ').Last())
                        {
                            case "OPP":
                                ds_list.Last().axisDir = "X";
                                break;
                            case "OPD":
                                ds_list.Last().axisDir = "Z";
                                break;
                            case "DPR":
                                ds_list.Last().axisDir = "X";
                                break;
                        }

                    }
                    if (line.Contains("PNTS"))
                    {
                        ds_list.Last().ScanLength = Convert.ToInt32(line.Split(' ').Last());
                    }
                    if (line.Contains("STEP"))
                    {
                        ds_list.Last().stepSize = Convert.ToDouble(line.Split(' ').Last());
                    }
                    if (line.Contains("DPTH"))
                    {
                        ds_list.Last().depth = Convert.ToDouble(line.Split(' ').Last());
                    }
                    if (line[0] == '<')
                    {
                        double pos = ds_list.Last().axisDir == "X" ? Convert.ToDouble(line.Split(' ').First().Trim('<')) : Convert.ToDouble(line.Split(' ')[2]);
                        double val = Convert.ToDouble(line.Split(' ').Last().Trim('>'));
                        ds_list.Last().scan_data.Add(new Tuple<double, double>(pos, val));
                    }
                }
                prevScans_sp.Children.Clear();
                foreach (DataScan ds in ds_list)
                {
                    Label lbl = new Label();
                    string scan_type = ds.axisDir == "X" ? "Profile" : "PDD";
                    string depth_type = ds.axisDir == "X" ? ds.depth.ToString() : "NA";
                    lbl.Content = String.Format("{0} - FLSZ ({1} x {2}) - Depth ({3})", scan_type, ds.FieldX, ds.FieldY, depth_type);
                    prevScans_sp.Children.Add(lbl);
                }
            }


        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            app.ClosePatient();
            app.Dispose();
        }

        public class DataScan
        {
            public double FieldX { get; set; }
            public double FieldY { get; set; }
            public double StartPos { get; set; }
            public double stepSize { get; set; }
            public int ScanLength { get; set; }
            public string axisDir { get; set; }
            public double depth { get; set; }
            public List<Tuple<double, double>> scan_data { get; set; }
            public DataScan()
            {
                scan_data = new List<Tuple<double, double>>();
            }
        }

        private void analyze_btn_Click(object sender, RoutedEventArgs e)
        {
            if (pat == null)
            {
                MessageBox.Show("Open a Patient");
            }
            else
            {
                if (plan_cmb.SelectedIndex == -1) { MessageBox.Show("Select a plan."); }
                else
                {
                    prevScans_sp.Children.Clear();
                    plan = course.PlanSetups.First(x => x.Id == plan_cmb.SelectedItem.ToString());
                }

                foreach (DataScan ds in ds_list)
                {
                    string s_output = "";
                    Beam b_keep = null;
                    foreach (Beam b in plan.Beams)
                    {
                        double x1 = b.ControlPoints.First().JawPositions.X1;
                        double x2 = b.ControlPoints.First().JawPositions.X2;
                        double y1 = b.ControlPoints.First().JawPositions.Y1;
                        double y2 = b.ControlPoints.First().JawPositions.Y2;

                        if (x2 - x1 == ds.FieldX && y2 - y1 == ds.FieldY)
                        {
                            b_keep = b;
                            break;
                        }

                    }
                    if (b_keep == null) { s_output = "No Scan"; }
                    else
                    {
                        //getting the dose profile
                        VVector start = new VVector();
                        start.x = ds.axisDir == "X" ? ds.scan_data.First().Item1 : 0;
                        start.y = ds.axisDir == "X" ? ds.depth - 200 : ds.scan_data.First().Item1 - 200;
                        start.z = 0; //inline direction !
                        VVector end = new VVector();
                        end.x = ds.axisDir == "X" ? ds.scan_data.Last().Item1 : 0;
                        end.y = ds.axisDir == "X" ? ds.depth - 200 : ds.scan_data.Last().Item1 - 200;
                        end.z = 0; //inline direction ! 
                        double[] size = new double[ds.ScanLength];
                        DoseProfile dp = b_keep.Dose.GetDoseProfile(start, end, size);
                        //Normalization Factor
                        double norm_factor = ds.axisDir == "X" ?
                            dp.Where(x => x.Position.x >= 0).First().Value :
                            dp.Max(x => x.Value);
                        using (StreamWriter sw = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) +
                            "\\Scan" + ds.FieldX.ToString() + "x" + ds.FieldY.ToString() + "_" + ds.depth.ToString() + ".csv"))
                        {
                            sw.WriteLine("Scan Pos, Scan Dose, Calc pos, Calc Dose, Gamma");
                            foreach (Tuple<double, double> tdd in ds.scan_data)
                            {
                                string calc_pos = "";
                                string calc_dos = "";
                                string gamma = "";

                                IEnumerable<ProfilePoint> pp_check = ds.axisDir == "X" ?
                                    dp.Where(x => x.Position.x >= tdd.Item1) :
                                    dp.Where(x => x.Position.y + 200 >= tdd.Item1);
                                ProfilePoint pp;
                                if (pp_check.Count() == 0) { break; }
                                else { pp = pp_check.First(); }

                                calc_pos = ds.axisDir == "X" ?
                                     pp.Position.x.ToString() :
                                     Convert.ToString(pp.Position.y + 200);

                                calc_dos = Convert.ToString(pp.Value / norm_factor * 100);

                                double gam = Get_Gamma(dp, tdd.Item1, tdd.Item2, Convert.ToDouble(calc_pos), Convert.ToDouble(calc_dos), pp, ds.axisDir, norm_factor);
                                sw.WriteLine(String.Format("{0}, {1}, {2}, {3}, {4}", tdd.Item1, tdd.Item2, calc_pos, calc_dos, gam));
                            }
                            sw.Flush();
                            s_output = "Success";
                        }
                    }
                    Label lbl = new Label();
                    string scan_type = ds.axisDir == "X" ? "Profile" : "PDD";
                    string depth_type = ds.axisDir == "X" ? ds.depth.ToString() : "NA";

                    lbl.Content = String.Format("{0} - FLSZ ({1} x {2}) - Depth ({3})", scan_type, ds.FieldX, ds.FieldY, depth_type);
                    lbl.Background = s_output == "Success" ? Brushes.LightGreen : Brushes.Pink;
                    prevScans_sp.Children.Add(lbl);
                }
            }

        }
        private double Get_Gamma(DoseProfile dp, double item1, double item2, double v1, double v2, ProfilePoint pp, string axisDir, double norm_factor)
        {
            //throw new NotImplementedException();
            List<double> gamma_values = new List<double>();
            double dd = 0.03 * norm_factor;
            double dta = 3;
            int loc = dp.ToList().IndexOf(pp);
            int start = loc - 10 * dta < 0 ? 0 : loc - Convert.ToInt16(10 * dta);
            int end = loc + 10 * dta > dp.Count() ? dp.Count() : loc + Convert.ToInt16(10 * dta);

            for (double i = start; i < end -1; i += 0.1)
            {
                int r0 = (int)Math.Floor(i);
                int r1 = (int)Math.Ceiling(i);
                double x0 = axisDir == "X" ? dp[r0].Position.x : dp[r0].Position.y + 200;
                double x1 = axisDir == "X" ? dp[r1].Position.x : dp[r1].Position.y + 200;
                double y0 = dp[r0].Value;
                double y1 = dp[r1].Value;
                double pos = 0;
                double dos = 0;

                if (r0 == r1)
                {
                    pos = x0;
                    dos = y0;
                }
                else
                {
                    pos = x0 + (i - r0) * (x1 - x0) / (r1 - r0);
                    dos = y0 + (i - r0) * (y1 - y0) / (r1 - r0);
                }
                double gamma = Math.Sqrt(Math.Pow((pos - item1) / dta, 2) + Math.Pow((dos/norm_factor*100 - item2) / dd, 2));
                gamma_values.Add(gamma);

            }
            return (gamma_values.Min());
        }

    }
}