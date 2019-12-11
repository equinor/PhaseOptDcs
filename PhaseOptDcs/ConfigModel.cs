using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace PhaseOptDcs
{
    [XmlRoot("configuration")]
    public class ConfigModel
    {
        public enum PressureUnit : int
        {
            barg = 0,
            bara = 1,
        }

        public enum TemperatureUnit : int
        {
            C = 0,
            K = 1,
        }

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
        [XmlElement]
        public PressureMeasurement Pressure { get; set; } = new PressureMeasurement();
        [XmlElement]
        public TemperatureMeasurement Temperature { get; set; } = new TemperatureMeasurement();
        [XmlElement]
        public PressureMeasurement Margin { get; set; } = new PressureMeasurement();
        [XmlElement]
        public PressureMeasurement DewPoint { get; set; } = new PressureMeasurement();



        [XmlAttribute]
        public string Name { get; set; }

        public object GetMargin()
        {
            double margin = Pressure.Value - DewPoint.Value;
            if (Margin.Type == "single")
            {
                return Convert.ToSingle(margin);
            }
            else if (DewPoint.Type == "double")
            {
                return Convert.ToDouble(margin);
            }
            else
            {
                return Convert.ToDouble(margin);
            }
        }

        public object GetDewPoint()
        {
            if (DewPoint.Type == "single")
            {
                return Convert.ToSingle(DewPoint.Value);
            }
            else if (DewPoint.Type == "double")
            {
                return Convert.ToDouble(DewPoint.Value);
            }
            else
            {
                return Convert.ToDouble(DewPoint.Value);
            }
        }
    }

    public class Measurement
    {
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public string Tag { get; set; }
        [XmlAttribute]
        public string Type { get; set; }

        [XmlIgnore]
        public double Value { get; set; }
    }

    public class PressureMeasurement : Measurement
    {
        [XmlAttribute]
        public ConfigModel.PressureUnit Unit { get; set; }

        public double GetConvertedPressure(ConfigModel.PressureUnit unit)
        {
            // Convert to bar absolute
            const double stdAtm = 1.01325;
            double result = 0.0;
            switch (unit)
            {
                case ConfigModel.PressureUnit.barg:
                    result = (Value + stdAtm);
                    break;
                case ConfigModel.PressureUnit.bara:
                    result = Value;
                    break;
                default:
                    break;
            }

            return (result);
        }
    }

    public class TemperatureMeasurement : Measurement
    {
        [XmlAttribute]
        public ConfigModel.TemperatureUnit Unit { get; set; }

        public double GetConvertedTemperature(ConfigModel.TemperatureUnit unit)
        {
            // Convert to K
            const double zeroCelsius = 273.15;
            double result = 0.0;
            switch (unit)
            {
                case ConfigModel.TemperatureUnit.C:
                    result = Value + zeroCelsius;
                    break;
                case ConfigModel.TemperatureUnit.K:
                    result = Value;
                    break;
                default:
                    break;
            }

            return (result);
        }
    }
}
