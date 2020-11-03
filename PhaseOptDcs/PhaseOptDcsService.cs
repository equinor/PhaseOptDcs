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
        private readonly object WorkerLock = new object();
        private bool working = false;

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

            timer = new Timer(config.Interval) { AutoReset = true, SynchronizingObject = null };
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
            if (working)
            {
                logger.Warn(CultureInfo.InvariantCulture, "Worker not completed within Interval. Interval might be too short.");
            }

            lock (WorkerLock)
            {
                working = true;
                List<UMROL> umrCallerList = ReadFromOPC();
                ProcessStreams(umrCallerList);
                WriteToOPC();
                working = false;
            }
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
                    nodes.Add(dropout.WorkingPoint.Pressure.Tag); types.Add(typeof(object));
                    nodes.Add(dropout.WorkingPoint.Temperature.Tag); types.Add(typeof(object));
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
                    dropout.WorkingPoint.Pressure.Value = Convert.ToDouble(result[it++], CultureInfo.InvariantCulture);
                    logger.Debug(CultureInfo.InvariantCulture, "Stream: \"{0}\" Working point \"{1}\": Pressure: {2} Unit: \"{3}\" Tag: \"{4}\"",
                        stream.Name, dropout.WorkingPoint.Name, dropout.WorkingPoint.Pressure.Value,
                        dropout.WorkingPoint.Pressure.Unit, dropout.WorkingPoint.Pressure.Tag);

                    dropout.WorkingPoint.Temperature.Value = Convert.ToDouble(result[it++], CultureInfo.InvariantCulture);
                    logger.Debug(CultureInfo.InvariantCulture, "Stream: \"{0}\" Working point \"{1}\": Temperature: {2} Unit: \"{3}\" Tag: \"{4}\"",
                        stream.Name, dropout.WorkingPoint.Name, dropout.WorkingPoint.Temperature.Value,
                        dropout.WorkingPoint.Temperature.Unit, dropout.WorkingPoint.Temperature.Tag);
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
                if (config.Streams.Item[i].FluidTune)
                {
                    umrCallerList[i].FluidTune();
                }

                if (!string.IsNullOrEmpty(config.Streams.Item[i].Cricondenbar.Pressure.Tag) ||
                    !string.IsNullOrEmpty(config.Streams.Item[i].Cricondenbar.Temperature.Tag))
                {
                    try
                    {
                        double[] res = umrCallerList[i].Cricondenbar();
                        config.Streams.Item[i].Cricondenbar.Pressure.Value = res[0];
                        config.Streams.Item[i].Cricondenbar.Temperature.Value = res[1];
                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Cricondenbar pressure Value: {1} Unit: \"{2}\" Tag: \"{3}\"",
                            config.Streams.Item[i].Name,
                            config.Streams.Item[i].Cricondenbar.Pressure.GetUnitConverted(),
                            config.Streams.Item[i].Cricondenbar.Pressure.Unit,
                            config.Streams.Item[i].Cricondenbar.Pressure.Tag);

                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Cricondenbar temperature Value: {1} Unit: \"{2}\" Tag: \"{3}\"",
                            config.Streams.Item[i].Name,
                            config.Streams.Item[i].Cricondenbar.Temperature.GetUnitConverted(),
                            config.Streams.Item[i].Cricondenbar.Temperature.Unit,
                            config.Streams.Item[i].Cricondenbar.Temperature.Tag);
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
                        dropOut.WorkingPoint.DewPoint.Value = umrCallerList[i]
                            .DewP(dropOut.WorkingPoint.Temperature.GetUMRConverted());
                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Working point \"{1}\": Dew point: Pressure: {2} Unit: \"{3}\" Pressure tag: \"{4}\"",
                            config.Streams.Item[i].Name, dropOut.WorkingPoint.Name,
                            dropOut.WorkingPoint.DewPoint.GetUnitConverted(),
                            dropOut.WorkingPoint.DewPoint.Unit,
                            dropOut.WorkingPoint.DewPoint.Tag);
                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Working point \"{1}\": Dew point margin: {2} tag: \"{3}\"",
                            config.Streams.Item[i].Name, dropOut.WorkingPoint.Name,
                            dropOut.WorkingPoint.GetDewPointMargin(), dropOut.WorkingPoint.DewPointMargin.Tag);

                        dropOut.WorkingPoint.DropoutPoint.Value = umrCallerList[i]
                            .DropoutSearch(dropOut.WorkingPoint.DropoutPoint.DropoutPercent,
                                dropOut.WorkingPoint.Temperature.GetUMRConverted(),
                                dropOut.WorkingPoint.DewPoint.GetUMRConverted());
                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Working point \"{1}\": Dropout point: Pressure {2} Unit: \"{3}\" Pressure tag: \"{4}\"",
                            config.Streams.Item[i].Name, dropOut.WorkingPoint.Name,
                            dropOut.WorkingPoint.DropoutPoint.GetUnitConverted(),
                            dropOut.WorkingPoint.DropoutPoint.Unit,
                            dropOut.WorkingPoint.DropoutPoint.Tag);
                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Working point: \"{1}\": Dropout point margin: {2} tag: \"{3}\"",
                            config.Streams.Item[i].Name, dropOut.WorkingPoint.Name,
                            dropOut.WorkingPoint.GetDropoutMargin(), dropOut.WorkingPoint.DropoutPointMargin.Tag);

                        dropOut.WorkingPoint.DropoutValue.Value = umrCallerList[i]
                            .Dropout(dropOut.WorkingPoint.Pressure.GetUMRConverted(),
                                dropOut.WorkingPoint.Temperature.GetUMRConverted())[0] * 100.0;
                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Working point: \"{1}\" Dropout value: {2} tag: \"{3}\"",
                            config.Streams.Item[i].Name, dropOut.WorkingPoint.Name,
                            dropOut.WorkingPoint.GetDropoutValue(), dropOut.WorkingPoint.DropoutValue.Tag);
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
                if (stream.Cricondenbar.Pressure.Tag != null)
                {
                    wvc.Add(new WriteValue
                    {
                        NodeId = stream.Cricondenbar.Pressure.Tag,
                        AttributeId = Attributes.Value,
                        Value = new DataValue { Value = stream.Cricondenbar.GetPressure() }
                    });
                }

                if (stream.Cricondenbar.Temperature.Tag != null)
                {

                    wvc.Add(new WriteValue
                    {
                        NodeId = stream.Cricondenbar.Temperature.Tag,
                        AttributeId = Attributes.Value,
                        Value = new DataValue { Value = stream.Cricondenbar.GetTemperature() }
                    });
                }

                foreach (var dropout in stream.LiquidDropouts.Item)
                {
                    if (!string.IsNullOrEmpty(dropout.WorkingPoint.DewPointMargin.Tag))
                    {
                        wvc.Add(new WriteValue
                        {
                            NodeId = dropout.WorkingPoint.DewPointMargin.Tag,
                            AttributeId = Attributes.Value,
                            Value = new DataValue { Value = dropout.WorkingPoint.GetDewPointMargin() }
                        });
                    }

                    if (!string.IsNullOrEmpty(dropout.WorkingPoint.DewPoint.Tag))
                    {
                        wvc.Add(new WriteValue
                        {
                            NodeId = dropout.WorkingPoint.DewPoint.Tag,
                            AttributeId = Attributes.Value,
                            Value = new DataValue { Value = dropout.WorkingPoint.GetDewPoint() }
                        });
                    }

                    if (!string.IsNullOrEmpty(dropout.WorkingPoint.DropoutPointMargin.Tag))
                    {
                        wvc.Add(new WriteValue
                        {
                            NodeId = dropout.WorkingPoint.DropoutPointMargin.Tag,
                            AttributeId = Attributes.Value,
                            Value = new DataValue { Value = dropout.WorkingPoint.GetDropoutMargin() }
                        });
                    }

                    if (!string.IsNullOrEmpty(dropout.WorkingPoint.DropoutPoint.Tag))
                    {
                        wvc.Add(new WriteValue
                        {
                            NodeId = dropout.WorkingPoint.DropoutPoint.Tag,
                            AttributeId = Attributes.Value,
                            Value = new DataValue { Value = dropout.WorkingPoint.GetDropoutPoint() }
                        });
                    }

                    if (!string.IsNullOrEmpty(dropout.WorkingPoint.DropoutValue.Tag))
                    {
                        wvc.Add(new WriteValue
                        {
                            NodeId = dropout.WorkingPoint.DropoutValue.Tag,
                            AttributeId = Attributes.Value,
                            Value = new DataValue { Value = dropout.WorkingPoint.GetDropoutValue() }
                        });
                    }
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
            logger.Info("Stop service command received.");
            timer.Stop();
            logger.Info("Waiting for current worker.");
            lock (WorkerLock)
            {
                logger.Info("Worker is done.");
                logger.Info("Disconnecting from OPC server.");
                opcClient.DisConnect();
            }
            logger.Info("Stopping service.");
        }
    }
}
