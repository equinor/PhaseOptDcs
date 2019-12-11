using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
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
            };
            config.Streams.Item.Add(new PhaseOptDcs.Stream { Name = "Statpipe" });
            config.Streams.Item[0].Composition.Item.Add(new PhaseOptDcs.Component { Name = "CO2", Id = 1, Tag = "31AI0157A_K", ScaleFactor = 1.0 });
            config.Streams.Item[0].Composition.Item.Add(new PhaseOptDcs.Component { Name = "N2", Id = 2, Tag = "31AI0157A_J", ScaleFactor = 1.0 });
            config.Streams.Item[0].Cricondenbar.TemperatureTag = "31TY0157_A";
            config.Streams.Item[0].Cricondenbar.PressureTag = "31PY0157_A";
            config.Streams.Item[0].LiquidDropouts.Item.Add(new PhaseOptDcs.LiquidDropout());
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

            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.Margin.Name = "Margin Name";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.Margin.Tag = "31DPY0157";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.Margin.Value = 5.5;
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.Margin.Unit = PhaseOptDcs.ConfigModel.PressureUnit.barg;
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.Margin.Type = "single";

            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPoint.Name = "DewPoint Name";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPoint.Tag = "31PY0157";
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPoint.Value = 5.5;
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPoint.Unit = PhaseOptDcs.ConfigModel.PressureUnit.barg;
            config.Streams.Item[0].LiquidDropouts.Item[0].WorkingPoint.DewPoint.Type = "single";

            // Stream 1
            config.Streams.Item.Add(new PhaseOptDcs.Stream { Name = "Åsgard" });
            config.Streams.Item[1].Composition.Item.Add(new PhaseOptDcs.Component { Name = "CO2", Id = 1, Tag = "31AI0161B_K", ScaleFactor = 1.0 });
            config.Streams.Item[1].Composition.Item.Add(new PhaseOptDcs.Component { Name = "N2", Id = 2, Tag = "31AI0161B_J", ScaleFactor = 1.0 });
            config.Streams.Item[1].Cricondenbar.TemperatureTag = "31TY0161_A";
            config.Streams.Item[1].Cricondenbar.PressureTag = "31PY0161_A";

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
            PhaseOptDcs.Cricondenbar cricondenbar = new PhaseOptDcs.Cricondenbar
            {
                Temperature = 3.1415,
                TemperatureType = "single",
            };

            Assert.AreEqual(typeof(System.Single), cricondenbar.GetTemperature().GetType());
            Assert.AreEqual(3.1415, Convert.ToDouble(cricondenbar.GetTemperature(), CultureInfo.InvariantCulture), 1.0e-5);
        }

        [TestMethod]
        public void Cricondenbar_GetTemperature_DoubleType()
        {
            PhaseOptDcs.Cricondenbar cricondenbar = new PhaseOptDcs.Cricondenbar
            {
                Temperature = 3.1415,
                TemperatureType = "double",
            };

            Assert.AreEqual(typeof(System.Double), cricondenbar.GetTemperature().GetType());
            Assert.AreEqual(3.1415, Convert.ToDouble(cricondenbar.GetTemperature(), CultureInfo.InvariantCulture), 1.0e-10);
        }

        [TestMethod]
        public void Cricondenbar_GetTemperature_DefaultType()
        {
            PhaseOptDcs.Cricondenbar cricondenbar = new PhaseOptDcs.Cricondenbar
            {
                Temperature = 3.1415,
            };

            Assert.AreEqual(typeof(System.Double), cricondenbar.GetTemperature().GetType());
            Assert.AreEqual(3.1415, Convert.ToDouble(cricondenbar.GetTemperature(), CultureInfo.InvariantCulture), 1.0e-10);
        }

        [TestMethod]
        public void Cricondenbar_GetPressure_SingleType()
        {
            PhaseOptDcs.Cricondenbar cricondenbar = new PhaseOptDcs.Cricondenbar
            {
                Pressure = 3.1415,
                PressureType = "single",
            };

            Assert.AreEqual(typeof(System.Single), cricondenbar.GetPressure().GetType());
            Assert.AreEqual(3.1415, Convert.ToDouble(cricondenbar.GetPressure(), CultureInfo.InvariantCulture), 1.0e-5);
        }

        [TestMethod]
        public void Cricondenbar_GetPressure_DoubleType()
        {
            PhaseOptDcs.Cricondenbar cricondenbar = new PhaseOptDcs.Cricondenbar
            {
                Pressure = 3.1415,
                PressureType = "double",
            };

            Assert.AreEqual(typeof(System.Double), cricondenbar.GetPressure().GetType());
            Assert.AreEqual(3.1415, Convert.ToDouble(cricondenbar.GetPressure(), CultureInfo.InvariantCulture), 1.0e-10);
        }

        [TestMethod]
        public void Cricondenbar_GetPressure_DefaultType()
        {
            PhaseOptDcs.Cricondenbar cricondenbar = new PhaseOptDcs.Cricondenbar
            {
                Pressure = 3.1415,
            };

            Assert.AreEqual(typeof(System.Double), cricondenbar.GetPressure().GetType());
            Assert.AreEqual(3.1415, Convert.ToDouble(cricondenbar.GetPressure(), CultureInfo.InvariantCulture), 1.0e-10);
        }

        [TestMethod]
        public void WorkingPoint_GetMargin_SingleType()
        {
            PhaseOptDcs.WorkingPoint workingPoint = new PhaseOptDcs.WorkingPoint();
            workingPoint.Margin.Type = "single";
            workingPoint.Pressure.Value = 3.1415;
            workingPoint.DewPoint.Value = 1.4142;

            Assert.AreEqual(typeof(System.Single), workingPoint.GetMargin().GetType());
            Assert.AreEqual(1.7273, Convert.ToDouble(workingPoint.GetMargin(), CultureInfo.InvariantCulture), 1.0e-5);
        }

        [TestMethod]
        public void WorkingPoint_GetMargin_DoubleType()
        {
            PhaseOptDcs.WorkingPoint workingPoint = new PhaseOptDcs.WorkingPoint();
            workingPoint.Margin.Type = "double";
            workingPoint.Pressure.Value = 3.1415;
            workingPoint.DewPoint.Value = 1.4142;

            Assert.AreEqual(typeof(System.Double), workingPoint.GetMargin().GetType());
            Assert.AreEqual(1.7273, Convert.ToDouble(workingPoint.GetMargin(), CultureInfo.InvariantCulture), 1.0e-10);
        }

        [TestMethod]
        public void WorkingPoint_GetMargin_DefaultType()
        {
            PhaseOptDcs.WorkingPoint workingPoint = new PhaseOptDcs.WorkingPoint();
            workingPoint.Pressure.Value = 3.1415;
            workingPoint.DewPoint.Value = 1.4142;

            Assert.AreEqual(typeof(System.Double), workingPoint.GetMargin().GetType());
            Assert.AreEqual(1.7273, Convert.ToDouble(workingPoint.GetMargin(), CultureInfo.InvariantCulture), 1.0e-10);
        }

        [TestMethod]
        public void WorkingPoint_GetDewPoint_SingleType()
        {
            PhaseOptDcs.WorkingPoint workingPoint = new PhaseOptDcs.WorkingPoint();
            workingPoint.DewPoint.Type = "single";
            workingPoint.DewPoint.Value = 1.4142;


            Assert.AreEqual(typeof(System.Single), workingPoint.GetDewPoint().GetType());
            Assert.AreEqual(1.4142, Convert.ToDouble(workingPoint.GetDewPoint(), CultureInfo.InvariantCulture), 1.0e-5);
        }

        [TestMethod]
        public void WorkingPoint_GetDewPoint_DoubleType()
        {
            PhaseOptDcs.WorkingPoint workingPoint = new PhaseOptDcs.WorkingPoint();
            workingPoint.DewPoint.Type = "double";
            workingPoint.DewPoint.Value = 1.4142;

            Assert.AreEqual(typeof(System.Double), workingPoint.GetDewPoint().GetType());
            Assert.AreEqual(1.4142, Convert.ToDouble(workingPoint.GetDewPoint(), CultureInfo.InvariantCulture), 1.0e-10);
        }

        [TestMethod]
        public void WorkingPoint_GetDewPoint_DefaultType()
        {
            PhaseOptDcs.WorkingPoint workingPoint = new PhaseOptDcs.WorkingPoint();
            workingPoint.DewPoint.Value = 1.4142;

            Assert.AreEqual(typeof(System.Double), workingPoint.GetDewPoint().GetType());
            Assert.AreEqual(1.4142, Convert.ToDouble(workingPoint.GetDewPoint(), CultureInfo.InvariantCulture), 1.0e-10);
        }

        [TestMethod]
        public void TemperatureMeasurement_GetConvertedTemperature_UnitIsCelsius_ReturnKelvin()
        {
            var temperature = new PhaseOptDcs.TemperatureMeasurement();
            double testTempKelvin = 446.3;
            temperature.Value = 173.15;

            double resultKelvin = temperature.GetConvertedTemperature(PhaseOptDcs.ConfigModel.TemperatureUnit.C);
            Assert.AreEqual(testTempKelvin, resultKelvin, 1.0e-10);
        }

        [TestMethod]
        public void TemperatureMeasurement_GetConvertedTemperature_UnitIsKelvin_ReturnKelvin()
        {
            var temperature = new PhaseOptDcs.TemperatureMeasurement();
            double testTempKelvin = 73.15;
            temperature.Value = testTempKelvin;

            double resultKelvin = temperature.GetConvertedTemperature(PhaseOptDcs.ConfigModel.TemperatureUnit.K);
            Assert.AreEqual(testTempKelvin, resultKelvin, 1.0e-10);
        }

        [TestMethod]
        public void PressureMeasurement_GetConvertedPressure_UnitIsBarg_ReturnBara()
        {
            var pressure = new PhaseOptDcs.PressureMeasurement();
            double testPressureBara = 112.713_25;
            pressure.Value = 111.7;

            double resultBara = pressure.GetConvertedPressure(PhaseOptDcs.ConfigModel.PressureUnit.barg);
            Assert.AreEqual(testPressureBara, resultBara, 1.0e-10);
        }

        [TestMethod]
        public void PressureMeasurement_GetConvertedPressure_UnitIsBara_ReturnBara()
        {
            var pressure = new PhaseOptDcs.PressureMeasurement();
            double testPressureBara = 157.756_4;
            pressure.Value = 157.756_4;

            double resultBara = pressure.GetConvertedPressure(PhaseOptDcs.ConfigModel.PressureUnit.bara);
            Assert.AreEqual(testPressureBara, resultBara, 1.0e-10);
        }


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
        public void UMROL_Cricondenbar()
        {
            PhaseOptDcs.UMROL uMROL = new PhaseOptDcs.UMROL(ids, composition);

            double[] expected = { 102.70644183416010, -13.831775300562015 };
            // bara and K
            double[] result = uMROL.Cricondenbar(0);
            Assert.AreEqual(expected[0], result[0], 1.0e-10);
            Assert.AreEqual(expected[1] + 273.15, result[1], 1.0e-5);

            // barg and °C
            result = uMROL.Cricondenbar(1);
            Assert.AreEqual(expected[0] - 1.01325, result[0], 1.0e-10);
            Assert.AreEqual(expected[1], result[1], 1.0e-5);

        }
    }
}
