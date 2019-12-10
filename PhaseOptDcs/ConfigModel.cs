using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace PhaseOptDcs
{
    [XmlRoot("configuration")]
    public class ConfigModel
    {
        [XmlElement]
        public string OpcUrl { get; set; }
        [XmlElement]
        public string OpcUser { get; set; }
        [XmlElement]
        public string OpcPassword { get; set; }
        [XmlElement]
        public StreamList Streams { get; set; } = new StreamList();

        public static ConfigModel ReadConfig(string file)
        {
            XmlReaderSettings readerSettings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true
            };

            XmlReader configFileReader = XmlReader.Create(file, readerSettings);
            XmlSerializer configSerializer = new XmlSerializer(typeof(ConfigModel));
            ConfigModel result = (ConfigModel)configSerializer.Deserialize(configFileReader);
            configFileReader.Close();

            return result;
        }
    }

    public class StreamList
    {
        public StreamList() { Item = new List<Stream>(); }
        [XmlElement("Stream")]
        public List<Stream> Item { get; }
    }

    public class Stream
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlElement]
        public CompositionList Composition { get; set; } = new CompositionList();
        [XmlElement]
        public Cricondenbar Cricondenbar { get; set; } = new Cricondenbar();
        [XmlElement]
        public LiquidDropoutList LiquidDropouts { get; set; } = new LiquidDropoutList();
    }

    public class CompositionList
    {
        public CompositionList() { Item = new List<Component>(); }
        [XmlElement("Component")]
        public List<Component> Item { get; }

        public double[] GetValues()
        {
            List<double> vs = new List<double>();

            foreach (var component in Item)
            {
                vs.Add(component.Value);
            }

            return vs.ToArray();
        }

        public double[] GetScaledValues()
        {
            List<double> vs = new List<double>();

            foreach (var component in Item)
            {
                vs.Add(component.GetScaledValue());
            }

            return vs.ToArray();
        }

        public Int32[] GetIds()
        {
            List<Int32> vs = new List<Int32>();

            foreach (var component in Item)
            {
                vs.Add(component.Id);
            }

            return vs.ToArray();
        }

    }

    public class Component
    {
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public Int32 Id { get; set; }
        [XmlAttribute]
        public string Tag { get; set; }
        [XmlAttribute]
        public double ScaleFactor { get; set; }
        [XmlIgnore]
        public double Value { get; set; }

        public double GetScaledValue()
        {
            return Value * ScaleFactor;
        }
    }

    public class Cricondenbar
    {
        [XmlAttribute]
        public string TemperatureTag { get; set; }
        [XmlAttribute]
        public string PressureTag { get; set; }

        [XmlAttribute]
        public string TemperatureType { get; set; }
        [XmlAttribute]
        public string PressureType { get; set; }

        [XmlIgnore]
        public double Pressure { get; set; }
        [XmlIgnore]
        public double Temperature { get; set; }

        public object GetTemperature() {
            if (TemperatureType == "single")
            {
                return Convert.ToSingle(Temperature);
            }
            else if (TemperatureType == "double")
            {
                return Convert.ToDouble(Temperature);
            }
            else
            {
                return Convert.ToDouble(Temperature);
            }
        }

        public object GetPressure()
        {
            if (PressureType == "single")
            {
                return Convert.ToSingle(Pressure);
            }
            else if (PressureType == "double")
            {
                return Convert.ToDouble(Pressure);
            }
            else
            {
                return Convert.ToDouble(Pressure);
            }
        }
    }

    public class LiquidDropoutList
    {
        public LiquidDropoutList() { Item = new List<LiquidDropout>(); }
        [XmlElement("LiquidDropout")]
        public List<LiquidDropout> Item { get; }
    }
    public class LiquidDropout
    {
        [XmlElement]
        public WorkingPoint WorkingPoint { get; set; }
    }

    public class WorkingPoint
    {
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public string PressureTag { get; set; }
        [XmlAttribute]
        public string TemperatureTag { get; set; }
        [XmlAttribute]
        public string MarginTag { get; set; }
        [XmlAttribute]
        public string MarginType { get; set; }
        [XmlAttribute]
        public string DewPointTag { get; set; }
        [XmlAttribute]
        public string DewPointType { get; set; }
        [XmlIgnore]
        public double Pressure { get; set; }
        [XmlIgnore]
        public double Temperature { get; set; }
        [XmlIgnore]
        public double DewPoint { get; set; }

        public double GetMargin()
        {
            return Pressure - DewPoint;
        }
    }
}
