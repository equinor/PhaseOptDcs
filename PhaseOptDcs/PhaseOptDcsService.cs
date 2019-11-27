using System;
using System.Globalization;

namespace PhaseOptDcs
{
    class PhaseOptDcsService
    {
        private readonly ConfigModel config;
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
        }
        public void Start()
        {
            logger.Info("Starting service.");
        }

        public void Stop()
        {
            logger.Info("Stopping service.");
        }
    }
}
