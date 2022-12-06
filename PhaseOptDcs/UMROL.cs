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

        [DllImport("umrol", EntryPoint = "umrol_dropout_search")]
        internal static extern double UmrolDropoutSearch(UmrolHandle umrol, double wd, double t, double p_max, double limit, int max_itr, bool raw);

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

        public Umrol(int[] id, double[] composition)
        {
            umrol = NativeMethods.UmrolNew();
            DataIn(id, composition);
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

        public Ccdb Cricondenbar(double p0 = -1.0, double t0 = -1.0)
        {
            return NativeMethods.UmrolCricondenbar(umrol, p0, t0);
        }

        public double Dewp(double t, double p0 = 0.0)
        {
            return NativeMethods.UmrolDewp(umrol, t, p0);
        }

        public Dropout Dropout(double t, double p)
        {
            return NativeMethods.UmrolVpl(umrol, t, p);
        }

        public double DropoutSearch(double wd, double t, double p_max, double limit = 1.0e-2, int max_itr = 25, bool raw = false)
        {
            return NativeMethods.UmrolDropoutSearch(umrol, wd, t, p_max, limit, max_itr, raw);
        }

        public void FluidTune()
        {

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
}