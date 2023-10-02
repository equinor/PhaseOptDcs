using Microsoft.VisualStudio.TestTools.UnitTesting;
using PhaseOptDcs;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Serialization;

namespace Tests
{
    [TestClass]
    public class ConfigurationTest
    {
        [TestMethod]
        public void GenerateAndReadConfigModel()
        {
            // Stream 0
            PhaseOptDcs.ConfigModel config = new PhaseOptDcs.ConfigModel
            {
                OpcUrl = "opc.tcp://localhost:62548/Quickstarts/DataAccessServer",
                OpcUser = "user",
                OpcPassword = "password",
                Interval = 60_000.0
            };
            config.Streams.Item.Add(new PhaseOptDcs.Stream { Name = "Statpipe", FluidTune = false });
            config.Streams.Item[0].Composition.Item.Add(new PhaseOptDcs.Component { Name = "CO2", Id = 1, Tag = "31AI0157A_K", ScaleFactor = 1.0 });
            config.Streams.Item[0].Composition.Item.Add(new PhaseOptDcs.Component { Name = "N2", Id = 2, Tag = "31AI0157A_J", ScaleFactor = 1.0 });
            config.Streams.Item[0].Cricondenbar.Temperature.Tag = "31TY0157_A";
            config.Streams.Item[0].Cricondenbar.Pressure.Tag = "31PY0157_A";
            config.Streams.Item[0].LiquidDropouts.Item.Add(new PhaseOptDcs.LiquidDropout());
            config.Streams.Item[0].LiquidDropouts.Item[0].Raw = true;
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint = new PhaseOptDcs.WorkingPoint
                { Name = "Kårstø" };
            
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.Pressure.Name = "Press Name";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.Pressure.Tag = "31PI0157";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.Pressure.Value = 5.5;
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.Pressure.Unit = PhaseOptDcs.ConfigModel.PressureUnit.bara;

            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.Temperature.Name = "Temperature Name";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.Temperature.Tag = "31TI0157";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.Temperature.Value = 5.5;
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.Temperature.Unit = PhaseOptDcs.ConfigModel.TemperatureUnit.C;

            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPointMargin.Name = "Margin Name";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPointMargin.Tag = "31DPY0157";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPointMargin.Value = 5.5;
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPointMargin.Unit = PhaseOptDcs.ConfigModel.PressureUnit.barg;
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPointMargin.Type = "single";

            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPoint.Name = "DewPoint Name";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPoint.Tag = "31PY0157";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPoint.Value = 5.5;
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPoint.Unit = PhaseOptDcs.ConfigModel.PressureUnit.barg;
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPoint.Type = "single";

            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DropoutPoint.Name = "DropoutPoint Name";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DropoutPoint.Tag = "31DPY0158";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DropoutPoint.Unit = PhaseOptDcs.ConfigModel.PressureUnit.barg;
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DropoutPoint.Type = "single";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DropoutPoint.DropoutPercent = 2.0;

            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DropoutPointMargin.Name = "DropoutPointMargin Name";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DropoutPointMargin.Tag = "31DPY0159";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DropoutPointMargin.Unit = PhaseOptDcs.ConfigModel.PressureUnit.barg;
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DropoutPointMargin.Type = "single";

            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DropoutValue.Name = "DropoutValue Name";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DropoutValue.Tag = "31AY0159";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DropoutValue.Type = "single";

            // Stream 1
            config.Streams.Item.Add(new PhaseOptDcs.Stream { Name = "Åsgard", FluidTune = true });
            config.Streams.Item[1].Composition.Item.Add(new PhaseOptDcs.Component { Name = "CO2", Id = 1, Tag = "31AI0161B_K", ScaleFactor = 1.0 });
            config.Streams.Item[1].Composition.Item.Add(new PhaseOptDcs.Component { Name = "N2", Id = 2, Tag = "31AI0161B_J", ScaleFactor = 1.0 });
            config.Streams.Item[1].Cricondenbar.Temperature.Tag = "31TY0161_A";
            config.Streams.Item[1].Cricondenbar.Pressure.Tag = "31PY0161_A";

            XmlWriterSettings writerSettings = new XmlWriterSettings
            {
                Indent = true,
            };
            XmlWriter writer = XmlWriter.Create("phaseopt.xml", writerSettings);
            XmlSerializer configSerializer = new XmlSerializer(typeof(PhaseOptDcs.ConfigModel));
            configSerializer.Serialize(writer, config);
            writer.Close();

            string file = AppDomain.CurrentDomain.BaseDirectory.ToString(CultureInfo.InvariantCulture) + "\\phaseopt.xml";
            PhaseOptDcs.ConfigModel readConfig = PhaseOptDcs.ConfigModel.ReadConfig(file);

            Assert.AreEqual("Statpipe", readConfig.Streams.Item[0].Name);
            Assert.AreEqual("Åsgard", readConfig.Streams.Item[1].Name);
        }
    }

    [TestClass]
    public class UnitTests
    {
        [TestMethod]
        public void CompositionList_GetValues()
        {

            PhaseOptDcs.CompositionList composition = new PhaseOptDcs.CompositionList();

            Int32 length = 35;

            for (int i = 0; i < length; i++)
            {
                composition.Item.Add(new PhaseOptDcs.Component { Name = "CO2", Id = 1, Tag = "31AI0157A_K", ScaleFactor = 1.0 });
                composition.Item[i].Value = Convert.ToDouble(i);
            }

            double[] values = composition.GetValues();

            for (int i = 0; i < length; i++)
            {
                Assert.AreEqual(values[i], Convert.ToDouble(i), 1.0e-13);
            }
        }

        [TestMethod]
        public void CompositionList_GetIds()
        {

            PhaseOptDcs.CompositionList composition = new PhaseOptDcs.CompositionList();

            Int32 length = 35;

            for (int i = 0; i < length; i++)
            {
                composition.Item.Add(new PhaseOptDcs.Component { Name = "CO2", Id = 1, Tag = "31AI0157A_K", ScaleFactor = 1.0 });
                composition.Item[i].Id = i;
            }

            Int32[] ids = composition.GetIds();

            for (int i = 0; i < length; i++)
            {
                Assert.AreEqual(ids[i], i);
            }
        }

        [TestMethod]
        public void Component_GetScaledValue()
        {
            PhaseOptDcs.Component component = new PhaseOptDcs.Component
            {
                Id = 101,
                Name = "C3",
                ScaleFactor = 3.14,
                Tag = "AI0001C",
                Value = 6.822
            };

            Assert.AreEqual(21.42108, component.GetScaledValue(), 1.0e-10);
            component.Value = 95.881;
            Assert.AreEqual(301.06634, component.GetScaledValue(), 1.0e-10);
        }

        [TestMethod]
        public void Cricondenbar_GetTemperature_SingleType()
        {
            PhaseOptDcs.Cricondenbar cricondenbar = new PhaseOptDcs.Cricondenbar();
            cricondenbar.Temperature.Value = 3.1415;
            cricondenbar.Temperature.Type = "single";

            Assert.AreEqual(typeof(System.Single), cricondenbar.GetTemperature().GetType());
        }

        [TestMethod]
        public void Cricondenbar_GetTemperature_DoubleType()
        {
            PhaseOptDcs.Cricondenbar cricondenbar = new PhaseOptDcs.Cricondenbar();
            cricondenbar.Temperature.Value = 3.1415;
            cricondenbar.Temperature.Type = "double";

            Assert.AreEqual(typeof(System.Double), cricondenbar.GetTemperature().GetType());
        }

        [TestMethod]
        public void Cricondenbar_GetTemperature_DefaultType()
        {
            PhaseOptDcs.Cricondenbar cricondenbar = new PhaseOptDcs.Cricondenbar();
            cricondenbar.Temperature.Value = 3.1415;

            Assert.AreEqual(typeof(System.Double), cricondenbar.GetTemperature().GetType());
        }

        [TestMethod]
        public void Cricondenbar_GetPressure_SingleType()
        {
            PhaseOptDcs.Cricondenbar cricondenbar = new PhaseOptDcs.Cricondenbar();
            cricondenbar.Pressure.Value = 3.1415;
            cricondenbar.Pressure.Type = "single";

            Assert.AreEqual(typeof(System.Single), cricondenbar.GetPressure().GetType());
        }

        [TestMethod]
        public void Cricondenbar_GetPressure_DoubleType()
        {
            PhaseOptDcs.Cricondenbar cricondenbar = new PhaseOptDcs.Cricondenbar();
            cricondenbar.Pressure.Value = 3.1415;
            cricondenbar.Pressure.Type = "double";

            Assert.AreEqual(typeof(System.Double), cricondenbar.GetPressure().GetType());
        }

        [TestMethod]
        public void Cricondenbar_GetPressure_DefaultType()
        {
            PhaseOptDcs.Cricondenbar cricondenbar = new PhaseOptDcs.Cricondenbar();
            cricondenbar.Pressure.Value = 3.1415;

            Assert.AreEqual(typeof(System.Double), cricondenbar.GetPressure().GetType());
        }

        [TestMethod]
        public void WorkingPoint_GetMargin_SingleType()
        {
            PhaseOptDcs.WorkingPoint workingPoint = new PhaseOptDcs.WorkingPoint();
            workingPoint.DewPointMargin.Type = "single";
            workingPoint.Pressure.Value = 3.1415;
            workingPoint.DewPoint.Value = 1.4142;

            Assert.AreEqual(typeof(System.Single), workingPoint.GetDewPointMargin().GetType());
        }

        [TestMethod]
        public void WorkingPoint_GetMargin_DoubleType()
        {
            PhaseOptDcs.WorkingPoint workingPoint = new PhaseOptDcs.WorkingPoint();
            workingPoint.DewPointMargin.Type = "double";
            workingPoint.Pressure.Value = 3.1415;
            workingPoint.DewPoint.Value = 1.4142;

            Assert.AreEqual(typeof(System.Double), workingPoint.GetDewPointMargin().GetType());
        }

        [TestMethod]
        public void WorkingPoint_GetMargin_DefaultType()
        {
            PhaseOptDcs.WorkingPoint workingPoint = new PhaseOptDcs.WorkingPoint();
            workingPoint.Pressure.Value = 3.1415;
            workingPoint.DewPoint.Value = 1.4142;

            Assert.AreEqual(typeof(System.Double), workingPoint.GetDewPointMargin().GetType());
        }

        [TestMethod]
        public void WorkingPoint_GetDewPoint_SingleType()
        {
            PhaseOptDcs.WorkingPoint workingPoint = new PhaseOptDcs.WorkingPoint();
            workingPoint.DewPoint.Type = "single";
            workingPoint.DewPoint.Value = 1.4142;


            Assert.AreEqual(typeof(System.Single), workingPoint.GetDewPoint().GetType());
        }

        [TestMethod]
        public void WorkingPoint_GetDewPoint_DoubleType()
        {
            PhaseOptDcs.WorkingPoint workingPoint = new PhaseOptDcs.WorkingPoint();
            workingPoint.DewPoint.Type = "double";
            workingPoint.DewPoint.Value = 1.4142;

            Assert.AreEqual(typeof(System.Double), workingPoint.GetDewPoint().GetType());
        }

        [TestMethod]
        public void WorkingPoint_GetDewPoint_DefaultType()
        {
            PhaseOptDcs.WorkingPoint workingPoint = new PhaseOptDcs.WorkingPoint();
            workingPoint.DewPoint.Value = 1.4142;

            Assert.AreEqual(typeof(System.Double), workingPoint.GetDewPoint().GetType());
        }

        [TestMethod]
        public void TemperatureMeasurement_GetUMRConverted_UnitIsCelsius_ReturnKelvin()
        {
            var temperature = new PhaseOptDcs.TemperatureMeasurement();
            double testTempKelvin = 446.3;
            temperature.Value = 173.15;
            temperature.Unit = PhaseOptDcs.ConfigModel.TemperatureUnit.C;

            double resultKelvin = temperature.GetUMRConverted();
            Assert.AreEqual(testTempKelvin, resultKelvin, 1.0e-10);
        }

        [TestMethod]
        public void TemperatureMeasurement_GetUMRConverted_UnitIsKelvin_ReturnKelvin()
        {
            var temperature = new PhaseOptDcs.TemperatureMeasurement();
            double testTempKelvin = 73.15;
            temperature.Value = testTempKelvin;
            temperature.Unit = PhaseOptDcs.ConfigModel.TemperatureUnit.K;

            double resultKelvin = temperature.GetUMRConverted();
            Assert.AreEqual(testTempKelvin, resultKelvin, 1.0e-10);
        }

        [TestMethod]
        public void TemperatureMeasurement_GetUnitConverted_UnitIsCelsius_ReturnCelsius()
        {
            var temperature = new PhaseOptDcs.TemperatureMeasurement();
            double testTempCelsius = -17.335;
            temperature.Value = 255.815;
            temperature.Unit = PhaseOptDcs.ConfigModel.TemperatureUnit.C;

            double resultCelsius = temperature.GetUnitConverted();
            Assert.AreEqual(testTempCelsius, resultCelsius, 1.0e-10);
        }

        [TestMethod]
        public void TemperatureMeasurement_GetUnitConverted_UnitIsKelvin_ReturnKelvin()
        {
            var temperature = new PhaseOptDcs.TemperatureMeasurement();
            double testTempKelvin = 255.815;
            temperature.Value = 255.815;
            temperature.Unit = PhaseOptDcs.ConfigModel.TemperatureUnit.K;

            double resultKelvin = temperature.GetUnitConverted();
            Assert.AreEqual(testTempKelvin, resultKelvin, 1.0e-10);
        }

        [TestMethod]
        public void PressureMeasurement_GetUMRConverted_UnitIsBarg_ReturnBara()
        {
            var pressure = new PhaseOptDcs.PressureMeasurement();
            double testPressureBara = 112.713_25;
            pressure.Value = 111.7;
            pressure.Unit = PhaseOptDcs.ConfigModel.PressureUnit.barg;

            double resultBara = pressure.GetUMRConverted();
            Assert.AreEqual(testPressureBara, resultBara, 1.0e-10);
        }

        [TestMethod]
        public void PressureMeasurement_GetUMRConverted_UnitIsBara_ReturnBara()
        {
            var pressure = new PhaseOptDcs.PressureMeasurement();
            double testPressureBara = 157.756_4;
            pressure.Value = 157.756_4;
            pressure.Unit = PhaseOptDcs.ConfigModel.PressureUnit.bara;

            double resultBara = pressure.GetUMRConverted();
            Assert.AreEqual(testPressureBara, resultBara, 1.0e-10);
        }

        [TestMethod]
        public void PressureMeasurement_GetUnitConverted_UnitIsBarg_ReturnBarg()
        {
            var pressure = new PhaseOptDcs.PressureMeasurement();
            double testPressureBarg = 111.7;
            pressure.Value = 112.713_25;
            pressure.Unit = PhaseOptDcs.ConfigModel.PressureUnit.barg;

            double resultBarg = pressure.GetUnitConverted();
            Assert.AreEqual(testPressureBarg, resultBarg, 1.0e-10);
        }

        [TestMethod]
        public void PressureMeasurement_GetUnitConverted_UnitIsBara_ReturnBara()
        {
            var pressure = new PhaseOptDcs.PressureMeasurement();
            double testPressureBara = 157.756_4;
            pressure.Value = 157.756_4;
            pressure.Unit = PhaseOptDcs.ConfigModel.PressureUnit.bara;

            double resultBara = pressure.GetUnitConverted();
            Assert.AreEqual(testPressureBara, resultBara, 1.0e-10);
        }
    }

    [TestClass]
    public class UMRTests
    {
        private readonly double[] composition =
{
            0.023176439, 0.006907786, 0.831210139, 0.077432143, 0.038859448,
            0.005465126, 0.009835248, 0.002111395, 0.002150455, 0.000666455,
            0.000214464, 0.000498599, 0.000200478, 0.000647215, 0.000163487,
            4.15516E-05, 0.000254571, 0.000100149, 1.68092E-05, 1.55526E-05,
            2.39625E-05, 8.52431E-06
        };

        private readonly int[] ids =
        {
            1, 2, 101, 201, 301, 401, 402, 503, 504, 603, 604, 605,
            701, 606, 608, 801, 707, 710, 901, 806, 809, 1016
        };

        [TestMethod]
        public void Cricondenbar()
        {
            PhaseOptDcs.Umrol umrol = new PhaseOptDcs.Umrol(ids, composition);
            PhaseOptDcs.Ccdb result = umrol.Cricondenbar(100.0, 258.0);

            double[] expected = { 102.70644183416010, -13.831775300562015 };
            // bara and K
            Assert.AreEqual(expected[0], result.p, 1.0e-3);
            Assert.AreEqual(expected[1] + 273.15, result.t, 1.0e-1);
        }

        [TestMethod]
        public void Cricondentherm()
        {
            PhaseOptDcs.Umrol umrol = new PhaseOptDcs.Umrol(ids, composition);
            PhaseOptDcs.Ccdb result = umrol.Cricondentherm(-1.0, -1.0);

            double[] expected = { 44.105980716914821, 21.327194366953904 };
            // bara and K
            Assert.AreEqual(expected[0], result.p, 1.0e-1);
            Assert.AreEqual(expected[1] + 273.15, result.t, 1.0e-3);
        }

        [TestMethod]
        public void DropoutSearch()
        {
            PhaseOptDcs.Umrol umrol = new PhaseOptDcs.Umrol(ids, composition);

            double wd = 2.5;
            double PMax = 102.88;
            double T = -12.5 + 273.15;
            double expected = 97.7628125;
            double result = umrol.DropoutSearch(wd: wd, t: T, p_max: PMax);
            Assert.AreEqual(expected, result, 1.0e-5);
        }

        [TestMethod]
        public void DropoutSearch_Raw()
        {
            PhaseOptDcs.Umrol umrol = new PhaseOptDcs.Umrol(ids, composition);

            double wd = 2.5;
            double PMax = 102.88;
            double T = -12.5 + 273.15;
            double expected = 96.083125;
            double result = umrol.DropoutSearch(wd: wd, t: T, p_max: PMax, raw: true);
            Assert.AreEqual(expected, result, 1.0e-5);
        }

        [TestMethod]
        public void Dewp()
        {
            PhaseOptDcs.Umrol umrol = new PhaseOptDcs.Umrol(ids, composition);

            double t = -7.5 + 273.15; 
            double p0 = 95.0;
            double expected = 101.6887;

            double result = umrol.Dewp(t, p0);
            Assert.AreEqual(expected, result, 1.0e-3);
        }

        [TestMethod]
        public void Dropout()
        {
            PhaseOptDcs.Umrol umrol = new PhaseOptDcs.Umrol(ids, composition);

            double t = -7.5 + 273.15;
            double p = 91.0;
            double[] expected = { 0.026342761314175723, 0.032162273488762716, 0.0070583850210421111, 0.0086555605838020914 };

            Dropout result = umrol.Dropout(p, t);
            Assert.AreEqual(expected[0], result.ldom1, 1.0e-3);
            Assert.AreEqual(expected[1], result.ldom2, 1.0e-3);
            Assert.AreEqual(expected[2], result.ldov1, 1.0e-3);
            Assert.AreEqual(expected[3], result.ldov2, 1.0e-3);
        }

        [TestMethod]
        public void TuneFluid()
        {
            PhaseOptDcs.Umrol umrol = new PhaseOptDcs.Umrol(ids, composition);
            umrol.TuneFluid(100.0, 258.0);
            PhaseOptDcs.Ccdb result = umrol.Cricondenbar(100.0, 258.0);

            double[] expected = { 102.70644183416010 + 2.4, -12.1 };
            // bara and K
            Assert.AreEqual(expected[0], result.p, 1.0e-1);
            Assert.AreEqual(expected[1] + 273.15, result.t, 1.0e-1);
        }
    }
}
