using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml;
using System.Xml.Serialization;
using System.Globalization;

namespace Tests
{
    [TestClass]
    public class ConfigurationTest
    {
        [TestMethod]
        public void GeneratePhaseOptDcsConfiguration()
        {
            // Stream 0
            PhaseOptDcs.ConfigModel config = new PhaseOptDcs.ConfigModel
            {
                OpcUrl = "opc.tcp://localhost:62548/Quickstarts/DataAccessServer",
                OpcUser = "user",
                OpcPassword = "password",
                OpcEndpoint = "xxx"
            };
            config.Streams.Item.Add(new PhaseOptDcs.Stream { Name = "Statpipe" });
            config.Streams.Item[0].Composition.Item.Add(new PhaseOptDcs.Component { Name = "CO2", Id = 1, Tag = "31AI0157A_K", ScaleFactor = 1.0 });
            config.Streams.Item[0].Composition.Item.Add(new PhaseOptDcs.Component { Name = "N2", Id = 2, Tag = "31AI0157A_J", ScaleFactor = 1.0 });
            config.Streams.Item[0].Cricondenbar.TemperatureTag = "31TY0157_A";
            config.Streams.Item[0].Cricondenbar.PressureTag = "31PY0157_A";

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
            writer.Dispose();

            string file = AppDomain.CurrentDomain.BaseDirectory.ToString(CultureInfo.InvariantCulture) + "\\phaseopt.xml";
            PhaseOptDcs.ConfigModel readConfig = PhaseOptDcs.ConfigModel.ReadConfig(file);

            Assert.AreEqual("Statpipe", readConfig.Streams.Item[0].Name);
            Assert.AreEqual("Åsgard", readConfig.Streams.Item[1].Name);
        }
    }
}
