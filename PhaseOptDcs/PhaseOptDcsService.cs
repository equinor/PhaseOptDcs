using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Timers;

namespace PhaseOptDcs
{
    class PhaseOptDcsService : IDisposable
    {
        private readonly Timer timer;
        private readonly ConfigModel config;
        private readonly OpcClient opcClient;
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public PhaseOptDcsService()
        {
            logger.Info("Initializing PhaseOptDcsService.");
            string ConfigFile = AppDomain.CurrentDomain.BaseDirectory.ToString(CultureInfo.InvariantCulture) + "PhaseOptDcs.config";
            logger.Debug(CultureInfo.InvariantCulture, "Reading configuration from {0}.", ConfigFile);
            try
            {
                config = ConfigModel.ReadConfig(ConfigFile);
            }
            catch (Exception e)
            {
                logger.Fatal(e, "Failed to read configuration.");
                throw;
            }

            timer = new Timer(20_000.0) { AutoReset = true, SynchronizingObject = null };
            timer.Elapsed += Worker;

            opcClient = new OpcClient(config.OpcUrl, config.OpcUser, config.OpcPassword);
        }

        public void Dispose()
        {
            opcClient.Dispose();
            timer.Dispose();
        }

        public async void Start()
        {
            logger.Info("Starting service.");
            await opcClient.Connect().ConfigureAwait(false);
            timer.Start();
        }

        private void Worker(object sender, ElapsedEventArgs ea)
        {
            List<UMROL> umrCallerList = ReadFromOPC();

            ProcessStreams(umrCallerList);

            WriteToOPC();
        }

        private List<UMROL> ReadFromOPC()
        {
            // One umrCaller for each stream
            List<UMROL> umrCallerList = new List<UMROL>();
            NodeIdCollection nodes = new NodeIdCollection();
            List<Type> types = new List<Type>();
            List<object> result = new List<object>();
            List<ServiceResult> errors = new List<ServiceResult>();

            // Make a list of all the OPC item that we want to read
            foreach (var stream in config.Streams.Item)
            {
                foreach (var component in stream.Composition.Item)
                {
                    nodes.Add(component.Tag); types.Add(typeof(object));
                }

                foreach (var dropout in stream.LiquidDropouts.Item)
                {
                    nodes.Add(dropout.WorkingPoint.PressureTag); types.Add(typeof(object));
                    nodes.Add(dropout.WorkingPoint.TemperatureTag); types.Add(typeof(object));
                }
            }

            foreach (var item in nodes)
            {
                logger.Debug(CultureInfo.InvariantCulture, "Item to read: \"{0}\"", item.ToString());
            }

            // Read all of the inputs
            try
            {
                opcClient.OpcSession.ReadValues(nodes, types, out result, out errors);
            }
            catch (Exception e)
            {
                logger.Error(e, "Error reading values from OPC.");
                return umrCallerList;
            }

            for (int n = 0; n < result.Count; n++)
            {
                logger.Debug(CultureInfo.InvariantCulture, "Item: \"{0}\" Value: \"{1}\" Status: \"{2}\"",
                    nodes[n].ToString(), result[n], errors[n].StatusCode.ToString());
            }

            int it = 0;
            foreach (var stream in config.Streams.Item)
            {
                foreach (var component in stream.Composition.Item)
                {
                    component.Value = Convert.ToDouble(result[it++], CultureInfo.InvariantCulture);
                    logger.Debug(CultureInfo.InvariantCulture,
                        "Stream: \"{0}\" Component Value: {1} Name: {2} Id: {3} Tag: \"{4}\"",
                        stream.Name, component.GetScaledValue(), component.Name, component.Id, component.Tag);
                }

                foreach (var dropout in stream.LiquidDropouts.Item)
                {
                    dropout.WorkingPoint.Pressure = Convert.ToDouble(result[it++], CultureInfo.InvariantCulture);
                    logger.Debug(CultureInfo.InvariantCulture, "Stream: \"{0}\" Working point Pressure: {1} Tag: \"{2}\"",
                        stream.Name, dropout.WorkingPoint.Pressure, nodes[it].ToString());

                    dropout.WorkingPoint.Temperature = Convert.ToDouble(result[it++], CultureInfo.InvariantCulture);
                    logger.Debug(CultureInfo.InvariantCulture, "Stream: \"{0}\" Working point Temperature: {1} Tag: \"{2}\"",
                        stream.Name, dropout.WorkingPoint.Temperature, dropout.WorkingPoint.TemperatureTag);

                }

                umrCallerList.Add(new UMROL(stream.Composition.GetIds(), stream.Composition.GetScaledValues()));
            }

            return umrCallerList;
        }

        private void ProcessStreams(List<UMROL> umrCallerList)
        {
            // Process each stream in parallel
            Parallel.For(0, umrCallerList.Count, i =>
            {
                if (!string.IsNullOrEmpty(config.Streams.Item[i].Cricondenbar.PressureTag) &&
                    !string.IsNullOrEmpty(config.Streams.Item[i].Cricondenbar.TemperatureTag))
                {
                    try
                    {
                        double[] res = umrCallerList[i].Cricondenbar();
                        config.Streams.Item[i].Cricondenbar.Pressure = res[0];
                        config.Streams.Item[i].Cricondenbar.Temperature = res[1];
                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Cricondenbar pressure Value: {1} Tag: \"{2}\"",
                            config.Streams.Item[i].Name,
                            config.Streams.Item[i].Cricondenbar.Pressure,
                            config.Streams.Item[i].Cricondenbar.PressureTag);

                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Cricondenbar temperature Value: {1} Tag: \"{2}\"",
                            config.Streams.Item[i].Name,
                            config.Streams.Item[i].Cricondenbar.Temperature,
                            config.Streams.Item[i].Cricondenbar.TemperatureTag);
                    }
                    catch (System.ComponentModel.Win32Exception e)
                    {
                        logger.Error(e, "Error calculating cricondenbar.");
                    }
                }

                foreach (var dropOut in config.Streams.Item[i].LiquidDropouts.Item)
                {
                    try
                    {
                        dropOut.WorkingPoint.DewPoint = umrCallerList[i].DewP(dropOut.WorkingPoint.Temperature);
                        logger.Debug(CultureInfo.InvariantCulture,
                            "Dew point Pressure: {0} PressureTag: \"{1}\" Temperature: {2} TemperatureTag: \"{3}\"",
                            dropOut.WorkingPoint.DewPoint, dropOut.WorkingPoint.DewPointTag,
                            dropOut.WorkingPoint.Temperature, dropOut.WorkingPoint.TemperatureTag);
                    }
                    catch (System.ComponentModel.Win32Exception e)
                    {

                        logger.Error(e, "Error calculating Dew point.");
                    }
                }
            });

        }

        private void WriteToOPC()
        {
            // Make a list of all the OPC item that we want to write
            WriteValueCollection wvc = new WriteValueCollection();

            foreach (var stream in config.Streams.Item)
            {
                WriteValue wv = new WriteValue
                {
                    NodeId = stream.Cricondenbar.PressureTag,
                    AttributeId = Attributes.Value,
                };

                wv.Value.Value = stream.Cricondenbar.GetPressure();
                wvc.Add(wv);

                wv = new WriteValue
                {
                    NodeId = stream.Cricondenbar.TemperatureTag,
                    AttributeId = Attributes.Value,
                };
                wv.Value.Value = stream.Cricondenbar.GetTemperature();
                wvc.Add(wv);

                foreach (var dropout in stream.LiquidDropouts.Item)
                {
                    wv = new WriteValue
                    {
                        NodeId = dropout.WorkingPoint.MarginTag,
                        AttributeId = Attributes.Value,
                    };
                    wv.Value.Value = dropout.WorkingPoint.GetMargin();
                    wvc.Add(wv);

                    wv = new WriteValue
                    {
                        NodeId = dropout.WorkingPoint.DewPointTag,
                        AttributeId = Attributes.Value,
                    };
                    wv.Value.Value = dropout.WorkingPoint.GetDewPoint();
                    wvc.Add(wv);
                }
            }

            foreach (var item in wvc)
            {
                logger.Debug(CultureInfo.InvariantCulture, "Item to write: \"{0}\" Value: {1}",
                    item.NodeId.ToString(),
                    item.Value.Value);
            }

            try
            {
                opcClient.OpcSession.Write(null, wvc, out StatusCodeCollection results, out DiagnosticInfoCollection diagnosticInfos);


                for (int i = 0; i < results.Count; i++)
                {
                    if (results[i].Code != 0)
                    {
                        logger.Error(CultureInfo.InvariantCulture, "Write result: \"{0}\" Tag: \"{1}\" Value: \"{2}\" Type: \"{3}\"",
                            results[i].ToString(), wvc[i].NodeId, wvc[i].Value.Value, wvc[i].Value.Value.GetType().ToString());
                    }

                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Error writing OPC items");
            }

        }

        public void Stop()
        {
            logger.Info("Stopping service.");
            timer.Stop();
            System.Threading.Thread.Sleep(1_000);
            opcClient.DisConnect();
        }
    }
}
