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
        }

        public async void Start()
        {
            logger.Info("Starting service.");
            await opcClient.Connect().ConfigureAwait(false);
            timer.Start();
        }

        private void Worker(object sender, ElapsedEventArgs ea)
        {
            List<UMROL> umrCallerList = new List<UMROL>();

            logger.Info("Reading composition.");
            foreach (var stream in config.Streams.Item)
            {
                logger.Info(CultureInfo.InvariantCulture, "Processing stream \"{0}\"", stream.Name);
                logger.Info(CultureInfo.InvariantCulture, "Reading composition \"{0}\"", stream.Name);
                foreach (var component in stream.Composition.Item)
                {
                    DataValue dataValue = opcClient.OpcSession.ReadValue(component.Tag);
                    component.Value = Convert.ToDouble(dataValue.Value, CultureInfo.InvariantCulture);
                    logger.Debug(CultureInfo.InvariantCulture,
                        "Component Value: {0} Name: {1} Id: {2} Tag: \"{3}\"",
                        component.GetScaledValue(), component.Name, component.Id, component.Tag);
                }

                if (stream.LiquidDropouts.Item.Count > 0)
                {
                    logger.Info(CultureInfo.InvariantCulture, "Reading liquid dropout inputs \"{0}\"", stream.Name);
                }
                foreach (var dropout in stream.LiquidDropouts.Item)
                {
                    DataValue dataValue = opcClient.OpcSession.ReadValue(dropout.WorkingPoint.PressureTag);
                    dropout.WorkingPoint.Pressure = Convert.ToDouble(dataValue.Value, CultureInfo.InvariantCulture);
                    logger.Debug(CultureInfo.InvariantCulture, "Pressure: {0} Tag: \"{1}\"",
                        dropout.WorkingPoint.Pressure, dropout.WorkingPoint.PressureTag);

                    dataValue = opcClient.OpcSession.ReadValue(dropout.WorkingPoint.TemperatureTag);
                    dropout.WorkingPoint.Temperature = Convert.ToDouble(dataValue.Value, CultureInfo.InvariantCulture);
                    logger.Debug(CultureInfo.InvariantCulture, "Temperature: {0} Tag: \"{1}\"",
                        dropout.WorkingPoint.Temperature, dropout.WorkingPoint.TemperatureTag);

                }

                umrCallerList.Add(new UMROL(stream.Composition.GetIds(), stream.Composition.GetValues()));
            }

            Parallel.For(0, umrCallerList.Count, i =>
                {
                    if (!string.IsNullOrEmpty(config.Streams.Item[i].Cricondenbar.PressureTag) &&
                        !string.IsNullOrEmpty(config.Streams.Item[i].Cricondenbar.TemperatureTag) )
                    {
                        try
                        {
                            double[] result = umrCallerList[i].Cricondenbar();
                            config.Streams.Item[i].Cricondenbar.Pressure = result[0];
                            config.Streams.Item[i].Cricondenbar.Temperature = result[1];
                            logger.Debug(CultureInfo.InvariantCulture,
                                "Cricondenbar pressure Value: {0} Tag: \"{1}\"",
                                config.Streams.Item[i].Cricondenbar.Pressure,
                                config.Streams.Item[i].Cricondenbar.PressureTag);

                            logger.Debug(CultureInfo.InvariantCulture,
                                "Cricondenbar temperature Value: {0} Tag: \"{1}\"",
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
                        if (resultType == typeof(float))
                        {
                            wv.Value.Value = Convert.ToSingle(config.Streams.Item[i].Cricondenbar.Temperature);
                        }
                        else if (resultType == typeof(double))
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
            timer.Dispose();
            opcClient.DisConnect();
        }
    }
}
