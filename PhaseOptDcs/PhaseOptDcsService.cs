using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace PhaseOptDcs
{
    class PhaseOptDcsService : IDisposable
    {
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
            Worker();
        }

        private void Worker()
        {
            List<UMROL> umrCallerList = new List<UMROL>();

            logger.Info("Reading composition.");
            foreach (var stream in config.Streams.Item)
            {
                logger.Info(CultureInfo.InvariantCulture, "Processing stream \"{0}\"", stream.Name);
                foreach (var component in stream.Composition.Item)
                {
                    DataValue dataValue = opcClient.OpcSession.ReadValue(component.Tag);
                    component.Value = Convert.ToDouble(dataValue.Value, CultureInfo.InvariantCulture);
                    logger.Debug(CultureInfo.InvariantCulture,
                        "Component Value: {0} Name: {1} Id: {2} Tag: \"{3}\"",
                        component.GetScaledValue(), component.Name, component.Id, component.Tag);
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
                    }
                });
        }

        public void Stop()
        {
            logger.Info("Stopping service.");
            opcClient.DisConnect();
        }
    }
}
