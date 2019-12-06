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
                return;
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

                umrCallerList.Add(new UMROL(stream.Composition.GetIds(), stream.Composition.GetValues()));
            }

            // Process each stream in parallel
            Parallel.For(0, umrCallerList.Count, i =>
                {
                    if (!string.IsNullOrEmpty(config.Streams.Item[i].Cricondenbar.PressureTag) &&
                        !string.IsNullOrEmpty(config.Streams.Item[i].Cricondenbar.TemperatureTag) )
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

                        var resultType = opcClient.OpcSession.ReadValue(config.Streams.Item[i].Cricondenbar.TemperatureTag).Value.GetType();
                        WriteValue wv = new WriteValue
                        {
                            NodeId = config.Streams.Item[i].Cricondenbar.TemperatureTag,
                            AttributeId = Attributes.Value
                        };
                        if ((Type)resultType == typeof(float))
                        {
                            wv.Value.Value = Convert.ToSingle(config.Streams.Item[i].Cricondenbar.Temperature);
                        }
                        else if ((Type)resultType == typeof(double))
                        {
                            wv.Value.Value = Convert.ToDouble(config.Streams.Item[i].Cricondenbar.Temperature);
                        }

                        wv.Value.StatusCode = StatusCodes.Good;

                        WriteValueCollection wvc = new WriteValueCollection
                        {
                            wv
                        };

                        opcClient.OpcSession.Write(null, wvc, out StatusCodeCollection results, out DiagnosticInfoCollection diagnosticInfos);
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

        public void Stop()
        {
            logger.Info("Stopping service.");
            timer.Stop();
            System.Threading.Thread.Sleep(1_000);
            opcClient.DisConnect();
        }
    }
}
