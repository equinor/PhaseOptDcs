using System;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PhaseOptDcs
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Ccdb
    {
        public double t;
        public double p;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Dropout
    {
        public double ldom1;
        public double ldom2;
        public double ldov1;
        public double ldov2;
    }

    internal class NativeMethods
    {
        [DllImport("umrol", EntryPoint = "umrol_new")]
        internal static extern UmrolHandle UmrolNew();

        [DllImport("umrol", EntryPoint = "umrol_free")]
        internal static extern void UmrolFree(IntPtr umrol);

        [DllImport("umrol", EntryPoint = "umrol_data_in")]
        internal static extern void UmrolDataIn(UmrolHandle umrol, int[] id, double[] composition, int size);

        [DllImport("umrol", EntryPoint = "umrol_cricondenbar")]
        internal static extern Ccdb UmrolCricondenbar(UmrolHandle umrol, double p0, double t0);

        [DllImport("umrol", EntryPoint = "umrol_dewp")]
        internal static extern double UmrolDewp(UmrolHandle umrol, double t, double p0);

        [DllImport("umrol", EntryPoint = "umrol_vpl")]
        internal static extern Dropout UmrolVpl(UmrolHandle umrol, double t, double p);

    }

    internal class UmrolHandle : SafeHandle
    {
        public UmrolHandle() : base(IntPtr.Zero, true) { }

        public override bool IsInvalid
        {
            get { return false; }
        }

        protected override bool ReleaseHandle()
        {
            NativeMethods.UmrolFree(handle);
            return true;
        }
    }

    public class Umrol : IDisposable
    {
        private readonly UmrolHandle umrol;
        private bool disposed = false;

        public Umrol()
        {
            umrol = NativeMethods.UmrolNew();
        }

        ~Umrol()
        {
            Dispose(false);
        }

        public void DataIn(int[] id, double[] composition)
        {
            int size = id.Length;

            NativeMethods.UmrolDataIn(umrol, id, composition, size);
        }

        public Ccdb Cricondenbar(double p0, double t0)
        {
            return NativeMethods.UmrolCricondenbar(umrol, p0, t0);
        }

        public double Dewp(double t, double p0)
        {
            return NativeMethods.UmrolDewp(umrol, t, p0);
        }

        public Dropout Vpl(double t, double p)
        {
            return NativeMethods.UmrolVpl(umrol, t, p);
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                // Free managed objects
            }

            umrol.Close();
            disposed = true;
        }
    }
    public class UMROL
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private double[] CompositionValues;
        private Int32[] CompositionIDs;

        public UMROL()
        { }
        public UMROL(Int32[] IDs, double[] Values)
        {
            CompositionIDs = IDs;
            CompositionValues = Values;
        }

        public void SetCompositionValues(double[] Values)
        {
            CompositionValues = Values;
        }

        public void SetCompositionIDs(Int32[] IDs)
        {
            CompositionIDs = IDs;
        }

        /// <summary>
        /// Calculates the density and the compressibility factor, at given pressure and temperature, of the composition.
        /// </summary>
        /// <param name="P">Pressure [bara]</param>
        /// <param name="T">Temperature [K]</param>
        /// <returns>An array containing {Vapour density, Liquid density, Vapour compressibility factor, Liquid compressibility factor}.
        /// If there is only one phase the values for the non existing phase will be -1.</returns>
        public double[] CalculateDensityAndCompressibility(double P = 1.01325, double T = 288.15)
        {
            double[] Results = new double[4];
            string Arguments = "-dens -id";
            string output = "";
            Process umrol = new Process();

            foreach (int i in CompositionIDs)
            {
                Arguments += " " + i.ToString(CultureInfo.InvariantCulture);
            }
            Arguments += " -z";
            foreach (double v in CompositionValues)
            {
                // Make the value string into a Fortran style double presicion literal
                string val = " " + v.ToString("G", CultureInfo.InvariantCulture).Replace('E', 'D');
                if (!val.Contains("D"))
                    val += "D0";

                Arguments += val;
            }
            Arguments += " -p " + P.ToString("G", CultureInfo.InvariantCulture) + "D0";
            Arguments += " -t " + T.ToString("G", CultureInfo.InvariantCulture) + "D0";

            umrol.StartInfo.UseShellExecute = false;
            umrol.StartInfo.RedirectStandardOutput = true;
            umrol.StartInfo.FileName = "umrol.exe";
            umrol.StartInfo.Arguments = Arguments;
            try
            {
                umrol.Start();
                umrol.PriorityClass = ProcessPriorityClass.Idle;
                output = umrol.StandardOutput.ReadToEnd();
                umrol.WaitForExit();
            }
            catch
            {
                Results[0] = 0.0;
                Results[1] = 0.0;
                Results[2] = 0.0;
                Results[3] = 0.0;
                throw;
            }
            finally
            {
                umrol.Dispose();
            }

            if (output.Length > 0)
            {
                Results[0] = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0], CultureInfo.InvariantCulture);
                Results[1] = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1], CultureInfo.InvariantCulture);
                Results[2] = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[2], CultureInfo.InvariantCulture);
                Results[3] = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[3], CultureInfo.InvariantCulture);
            }

            return Results;
        }

        /// <summary>
        /// Calculates the cricondenbar point of the composition.
        /// </summary>
        /// <param name="Units">Sets the engineering units of the outputs. 0: use bara and Kelvin. 1: use barg and ­°C.</param>
        /// <returns>An array containing the cricondenbar pressure and temperature.</returns>
        public double[] Cricondenbar()
        {
            double[] Results = new double[2];
            double CCBT = -1.0;
            double CCBP = -1.0;
            string Arguments = "-ccd -ind 2 -id";
            string output = "";
            Process umrol = new Process();

            foreach (int i in CompositionIDs)
            {
                Arguments += " " + i.ToString(CultureInfo.InvariantCulture);
            }
            Arguments += " -z";
            foreach (double v in CompositionValues)
            {
                // Make the value string into a Fortran style double presicion literal
                string val = " " + v.ToString("G", CultureInfo.InvariantCulture).Replace('E', 'D');
                if (!val.Contains("D"))
                    val += "D0";

                Arguments += val;
            }

            umrol.StartInfo.UseShellExecute = false;
            umrol.StartInfo.RedirectStandardOutput = true;
            umrol.StartInfo.FileName = "umrol.exe";
            umrol.StartInfo.Arguments = Arguments;
            try
            {
                umrol.Start();
                umrol.PriorityClass = ProcessPriorityClass.Idle;
                output = umrol.StandardOutput.ReadToEnd();
                umrol.WaitForExit();
            }
            catch
            {
                CCBP = -1.0;
                CCBT = -1.0;
                throw;
            }
            finally
            {
                umrol.Dispose();
            }

            if (output.Length > 0)
            {
                CCBP = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0], CultureInfo.InvariantCulture);
                CCBT = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1], CultureInfo.InvariantCulture);
            }

            if (CCBP < 0.0) CCBP = double.NaN;
            if (CCBT < 0.0) CCBT = double.NaN;

            Results[0] = CCBP;
            Results[1] = CCBT;

            logger.Debug(CultureInfo.InvariantCulture, "Cricondenbar arguments: {0}", Arguments);
            logger.Debug(CultureInfo.InvariantCulture, "Cricondenbar results: {0}", output);

            return Results;
        }

        /// <summary>
        /// Calculates the cricondentherm point of the composition.
        /// </summary>
        /// <param name="Units">Sets the engineering units of the outputs. 0: use bara and Kelvin. 1: use barg and ­°C.</param>
        /// <returns>An array containing the cricondentherm pressure and temperature.</returns>
        public double[] Cricondentherm()
        {
            // Calculate the cricondentherm point
            double CCTT = -1.0;
            double CCTP = -1.0;
            double[] Results = new double[2];
            string Arguments = "-ccd -ind 1 -id";
            string output = "";
            Process umrol = new Process();

            foreach (int i in CompositionIDs)
            {
                Arguments += " " + i.ToString(CultureInfo.InvariantCulture);
            }
            Arguments += " -z";
            foreach (double v in CompositionValues)
            {
                // Make the value string into a Fortran style double presicion literal
                string val = " " + v.ToString("G", CultureInfo.InvariantCulture).Replace('E', 'D');
                if (!val.Contains("D"))
                    val += "D0";

                Arguments += val;
            }

            umrol.StartInfo.UseShellExecute = false;
            umrol.StartInfo.RedirectStandardOutput = true;
            umrol.StartInfo.FileName = "umrol.exe";
            umrol.StartInfo.Arguments = Arguments;
            try
            {
                umrol.Start();
                umrol.PriorityClass = ProcessPriorityClass.Idle;
                output = umrol.StandardOutput.ReadToEnd();
                umrol.WaitForExit();
            }
            catch
            {
                CCTP = double.NaN;
                CCTT = double.NaN;
                throw;
            }
            finally
            {
                umrol.Dispose();
            }

            if (output.Length > 0)
            {
                CCTP = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0], CultureInfo.InvariantCulture);
                CCTT = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1], CultureInfo.InvariantCulture);
            }

            if (CCTP < 0.0) CCTP = double.NaN;
            if (CCTT < 0.0) CCTT = double.NaN;

            Results[0] = CCTP;
            Results[1] = CCTT;

            return Results;
        }


        /// <summary>
        /// Calculates the liquid dropout of the composition at given pressure and temperature.
        /// </summary>
        /// <param name="P">Pressure [bara]</param>
        /// <param name="T">Temperature [K]</param>
        /// <returns>An array containing the liquid dropout mass and volume fractions.</returns>
        public double[] Dropout(double P, double T)
        {
            double[] Results = new double[4] { double.NaN, double.NaN, double.NaN, double.NaN };
            string Arguments = "-vpl -id";
            string output = "";
            Process umrol = new Process();

            foreach (int i in CompositionIDs)
            {
                Arguments += " " + i.ToString(CultureInfo.InvariantCulture);
            }
            Arguments += " -z";
            foreach (double v in CompositionValues)
            {
                // Make the value string into a Fortran style double presicion literal
                string val = " " + v.ToString("G", CultureInfo.InvariantCulture).Replace('E', 'D');
                if (!val.Contains("D"))
                    val += "D0";

                Arguments += val;
            }
            Arguments += " -p " + P.ToString("G", CultureInfo.InvariantCulture) + "D0";
            Arguments += " -t " + T.ToString("G", CultureInfo.InvariantCulture) + "D0";

            umrol.StartInfo.UseShellExecute = false;
            umrol.StartInfo.RedirectStandardOutput = true;
            umrol.StartInfo.FileName = "umrol.exe";
            umrol.StartInfo.Arguments = Arguments;
            try
            {
                umrol.Start();
                umrol.PriorityClass = ProcessPriorityClass.Idle;
                output = umrol.StandardOutput.ReadToEnd();
                umrol.WaitForExit();
            }
            catch
            {
                Results[0] = 0.0;
                Results[1] = 0.0;
                throw;
            }
            finally
            {
                umrol.Dispose();
            }

            if (output.Length > 0)
            {
                Results[0] = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0], CultureInfo.InvariantCulture);
                Results[1] = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1], CultureInfo.InvariantCulture);
                Results[2] = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[2], CultureInfo.InvariantCulture);
                Results[3] = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[3], CultureInfo.InvariantCulture);
            }

            logger.Debug(CultureInfo.InvariantCulture, "Dropout arguments: {0}", Arguments);
            logger.Debug(CultureInfo.InvariantCulture, "Dropout results: {0}", output);

            return Results;
        }

        /// <summary>
        /// Calculates the pressure for the given liquid dropout value.
        /// </summary>
        /// <param name="wd">Liquid dropout value [liquid %]</param>
        /// <param name="T">Temperature [K]</param>
        /// <param name="PMax">Maximum pressure of the search area [bara]</param>
        /// <param name="limit">Threshold for ending the serach. When a pressure that gives a dropout within this limit to the wantet dropout, the search is done [liquid %]</param>
        /// <param name="maxIterations">The maximum number of iterations that the search is allowed to use. [-]</param>
        /// <returns>The pressure that would give wd % liquid dropout [bara].</returns>
        public double DropoutSearch(double wd, double T, double PMax, double limit = 0.01, int maxIterations = 25, bool Raw = false)
        {
            double P = double.NaN;
            string Arguments = "-ds -id";
            string output = "";
            Process umrol = new Process();

            foreach (int i in CompositionIDs)
            {
                Arguments += " " + i.ToString(CultureInfo.InvariantCulture);
            }
            Arguments += " -z";
            foreach (double v in CompositionValues)
            {
                // Make the value string into a Fortran style double presicion literal
                string val = " " + v.ToString("G", CultureInfo.InvariantCulture).Replace('E', 'D');
                if (!val.Contains("D"))
                    val += "D0";

                Arguments += val;
            }
            Arguments += " -wd " + wd.ToString("G", CultureInfo.InvariantCulture) + "D0";
            Arguments += " -p " + PMax.ToString("G", CultureInfo.InvariantCulture) + "D0";
            Arguments += " -t " + T.ToString("G", CultureInfo.InvariantCulture) + "D0";
            Arguments += " -limit " + limit.ToString("G", CultureInfo.InvariantCulture) + "D0";
            Arguments += " -max-itr " + maxIterations.ToString(CultureInfo.InvariantCulture);
            if (Raw)
            {
                Arguments += " -raw ";
            }

            umrol.StartInfo.UseShellExecute = false;
            umrol.StartInfo.RedirectStandardOutput = true;
            umrol.StartInfo.FileName = "umrol.exe";
            umrol.StartInfo.Arguments = Arguments;
            try
            {
                umrol.Start();
                umrol.PriorityClass = ProcessPriorityClass.Idle;
                output = umrol.StandardOutput.ReadToEnd();
                umrol.WaitForExit();
            }
            catch
            {
                P = double.NaN;
                throw;
            }
            finally
            {
                umrol.Dispose();
            }

            if (output.Length > 0)
            {
                P = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0], CultureInfo.InvariantCulture);
            }

            logger.Debug(CultureInfo.InvariantCulture, "DropoutSearch arguments: {0}", Arguments);
            logger.Debug(CultureInfo.InvariantCulture, "DropoutSearch results: {0}", P);

            return P;
        }


        /// <summary>
        /// Calculates the dew point pressure of the composition.
        /// </summary>
        /// <param name="T">Temperature [K]</param>
        /// <returns>The dew point pressure [bara].</returns>
        public double DewP(double T)
        {
            double P1 = 0.0;
            double P2 = 0.0;
            string Arguments = "-dewp -id";
            string output = "";
            Process umrol = new Process();

            foreach (int i in CompositionIDs)
            {
                Arguments += " " + i.ToString(CultureInfo.InvariantCulture);
            }
            Arguments += " -z";
            foreach (double v in CompositionValues)
            {
                // Make the value string into a Fortran style double presicion literal
                string val = " " + v.ToString("G", CultureInfo.InvariantCulture).Replace('E', 'D');
                if (!val.Contains("D"))
                    val += "D0";

                Arguments += val;
            }
            Arguments += " -t " + T.ToString("G", CultureInfo.InvariantCulture) + "D0";

            umrol.StartInfo.UseShellExecute = false;
            umrol.StartInfo.RedirectStandardOutput = true;
            umrol.StartInfo.FileName = "umrol.exe";
            umrol.StartInfo.Arguments = Arguments;
            try
            {
                umrol.Start();
                umrol.PriorityClass = ProcessPriorityClass.Idle;
                output = umrol.StandardOutput.ReadToEnd();
                umrol.WaitForExit();
            }
            catch
            {
                P1 = 0.0;
                P2 = 0.0;
                throw;
            }
            finally
            {
                umrol.Dispose();
            }

            if (output.Length > 0)
            {
                P1 = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0], CultureInfo.InvariantCulture);
                P2 = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1], CultureInfo.InvariantCulture);
            }

            if (P1 > 900.0) P1 = 0.0;
            if (P2 > 900.0) P2 = 0.0;

            logger.Debug(CultureInfo.InvariantCulture, "DewP arguments: {0}", Arguments);
            logger.Debug(CultureInfo.InvariantCulture, "DewP results: {0}", output);

            return Math.Max(P1, P2);
        }

        public int FluidTune()
        {
            string Arguments = "-tf -id";
            string output = "";
            Process umrol = new Process();

            foreach (int i in CompositionIDs)
            {
                Arguments += " " + i.ToString(CultureInfo.InvariantCulture);
            }
            Arguments += " -z";
            foreach (double v in CompositionValues)
            {
                // Make the value string into a Fortran style double presicion literal
                string val = " " + v.ToString("G", CultureInfo.InvariantCulture).Replace('E', 'D');
                if (!val.Contains("D"))
                    val += "D0";

                Arguments += val;
            }

            umrol.StartInfo.UseShellExecute = false;
            umrol.StartInfo.RedirectStandardOutput = true;
            umrol.StartInfo.FileName = "umrol.exe";
            umrol.StartInfo.Arguments = Arguments;
            try
            {
                umrol.Start();
                umrol.PriorityClass = ProcessPriorityClass.Idle;
                output = umrol.StandardOutput.ReadToEnd();
                umrol.WaitForExit();
            }
            finally
            {
                umrol.Dispose();
            }

            if (output.Length > 0)
            {
                for (int i = 0; i < CompositionValues.Length; i++)
                {
                    CompositionValues[i] = Convert.ToDouble(output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[i], CultureInfo.InvariantCulture);
                }
            }

            logger.Debug(CultureInfo.InvariantCulture, "FluidTune arguments: {0}", Arguments);
            logger.Debug(CultureInfo.InvariantCulture, "FluidTune results: {0}", output);

            return 0;
        }
    }
}
