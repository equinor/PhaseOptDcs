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
        public string OpcEndpoint { get; set; }
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
            configFileReader.Dispose();

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
    }

    public class CompositionList
    {
        public CompositionList() { Item = new List<Component>(); }
        [XmlElement("Component")]
        public List<Component> Item { get; }
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
    }
}
