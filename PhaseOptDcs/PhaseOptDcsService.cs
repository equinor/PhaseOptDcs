using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
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
        private readonly object WorkerLock = new();
        private readonly object LoggerLock = new();
        private bool working = false;
        private Subscription subscription;

        public PhaseOptDcsService()
        {
            Assembly assem = Assembly.GetExecutingAssembly();
            string version = assem.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            string title = assem.GetName().Name;

            logger.Info("Initializing \"{0}\" version \"{1}\".", title, version.ToString());

            string ConfigFile = AppDomain.CurrentDomain.BaseDirectory.ToString(CultureInfo.InvariantCulture) + "PhaseOptDcs.config";
            try
            {
                logger.Debug(CultureInfo.InvariantCulture, "Reading configuration from {0}.", ConfigFile);
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
            GenerateNodeIds();
            timer.Start();

            // Create subscription
            subscription = new Subscription(opcClient.OpcSession.DefaultSubscription)
            {
                DisplayName = "PhaseOptDcs",
                PublishingEnabled = true,
                PublishingInterval = Convert.ToInt32(config.Interval / 2.0),
                LifetimeCount = 0,
                MinLifetimeInterval = 120_000,
            };
            opcClient.OpcSession.AddSubscription(subscription);
            subscription.Create();
            logger.Info("Subscription created with SubscriptionId = {0}", subscription.Id);

            // Add monitored items to subscription
            foreach (var stream in config.Streams.Item)
            {
                foreach (var component in stream.Composition.Item)
                {
                    if (string.IsNullOrEmpty(component.NodeId)) { continue; }

                    MonitoredItem item = new(subscription.DefaultItem);
                    item.StartNodeId = component.NodeId;
                    item.AttributeId = Attributes.Value;
                    item.DisplayName = component.Name;
                    item.SamplingInterval = stream.Composition.SamplingInterval;
                    item.QueueSize = 1;
                    item.DiscardOldest = true;
                    item.MonitoringMode = MonitoringMode.Reporting;
                    item.Notification += OnMonitoredItemNotification;

                    if (component.SamplingInterval != -2)
                    {
                        item.SamplingInterval = component.SamplingInterval;
                    }

                    subscription.AddItem(item);

                }

                foreach (var dropout in stream.LiquidDropouts.Item)
                {
                    MonitoredItem itemP = new(subscription.DefaultItem);
                    itemP.StartNodeId = dropout.Pressure.NodeId;
                    itemP.AttributeId = Attributes.Value;
                    itemP.DisplayName = dropout.Pressure.Name;
                    itemP.SamplingInterval = stream.Composition.SamplingInterval;
                    itemP.QueueSize = 1;
                    itemP.DiscardOldest = true;
                    itemP.MonitoringMode = MonitoringMode.Reporting;
                    itemP.Notification += OnMonitoredItemNotification;

                    if (dropout.Pressure.SamplingInterval != -2)
                    {
                        itemP.SamplingInterval = dropout.Pressure.SamplingInterval;
                    }

                    subscription.AddItem(itemP);

                    MonitoredItem itemT = new(subscription.DefaultItem);
                    itemT.StartNodeId = dropout.Temperature.NodeId;
                    itemT.AttributeId = Attributes.Value;
                    itemT.DisplayName = dropout.Temperature.Name;
                    itemT.SamplingInterval = stream.Composition.SamplingInterval;
                    itemT.QueueSize = 1;
                    itemT.DiscardOldest = true;
                    itemT.MonitoringMode = MonitoringMode.Reporting;
                    itemT.Notification += OnMonitoredItemNotification;

                    if (dropout.Temperature.SamplingInterval != -2)
                    {
                        itemT.SamplingInterval = dropout.Temperature.SamplingInterval;
                    }

                    subscription.AddItem(itemT);
                }

                stream.Umrol = new();
            }

            subscription.ApplyChanges();
            subscription.StateChanged += null;
            foreach (MonitoredItem item in subscription.MonitoredItems)
            {
                logger.Debug("Monitored item name {0}, nodeid {1}", item.DisplayName, item.ResolvedNodeId);
                if (!item.Status.Created)
                {
                    logger.Error("Monitored item {0}, {1} not created {2}", item.DisplayName, item.ResolvedNodeId, item.Status.Error);
                }
            }
            logger.Info("MonitoredItems created for SubscriptionId = {0}", subscription.Id);
        }

        private void Worker(object sender, ElapsedEventArgs ea)
        {
            logger.Debug(CultureInfo.InvariantCulture, "Starting worker.");

            if (opcClient.OpcSession.KeepAliveStopped)
            {
                logger.Debug("Server not responding. Suspending Worker.");
                lock (WorkerLock)
                {
                    working = false;
                }
                return;
            }

            if (working)
            {
                logger.Warn(CultureInfo.InvariantCulture, "Worker not completed within Interval. Interval might be too short.");
                timer.Interval *= 1.1;
                logger.Info(CultureInfo.InvariantCulture, "Increasing interval by 10% to {0}ms.", timer.Interval);

                lock (WorkerLock)
                {
                    working = false;
                }
                return;
            }

            Stopwatch watch = Stopwatch.StartNew();

            lock (WorkerLock)
            {
                working = true;
                ProcessStreams();
                WriteToOPC();
                working = false;
            }

            watch.Stop();
            
            if (timer.Interval > config.Interval)
            {
                timer.Interval *= 0.99;
                if (timer.Interval < config.Interval)
                {
                    timer.Interval = config.Interval;
                    logger.Info(CultureInfo.InvariantCulture, "Resetting interval to {0}ms.", timer.Interval);
                }
            }

            logger.Debug(CultureInfo.InvariantCulture, "Worker done. Elapsed time: {0} ms.", watch.ElapsedMilliseconds);
        }

        private void ProcessStreams()
        {
            // Process each stream in parallel
            Parallel.ForEach(config.Streams.Item, stream =>
            {
                lock (LoggerLock)
                {
                    foreach (Component comp in stream.Composition.Item)
                    {
                        logger.Debug(CultureInfo.InvariantCulture, $"Stream \"{stream.Name}\" {comp.Name} {comp.GetScaledValue()}");
                    }
                    InputError err = InputError.Ok;
                    stream.Umrol.DataIn(stream.Composition.GetIds(), stream.Composition.GetScaledValues(), ref err);
                    if (err != InputError.Ok)
                    {
                        logger.Error("Invalid composition {0}: {1}", stream.Name, err.ToString());
                        return;
                    }
                }


                if (stream.FluidTune)
                {
                    logger.Debug(CultureInfo.InvariantCulture, "Tuning composition for Stream \"{0}\"", stream.Name);
                    stream.Umrol.TuneFluid(stream.Cricondenbar.Pressure.Value, stream.Cricondenbar.Temperature.Value);
                }

                // Cricondenbar
                if (!string.IsNullOrEmpty(stream.Cricondenbar.Pressure.NodeId) ||
                    !string.IsNullOrEmpty(stream.Cricondenbar.Temperature.NodeId))
                {
                    stream.Cricondenbar.Pressure.Quality = StatusCodes.Bad;
                    stream.Cricondenbar.Temperature.Quality = StatusCodes.Bad;

                    try
                    {
                        logger.Debug(CultureInfo.InvariantCulture, "Calculating Cricondenbar({0}, {1})", stream.Cricondenbar.Pressure.Value, stream.Cricondenbar.Temperature.Value);
                        var res = stream.Umrol.Cricondenbar(stream.Cricondenbar.Pressure.Value, stream.Cricondenbar.Temperature.Value);
                        if (double.IsNaN(res.p) || double.IsNaN(res.t))
                        {
                            logger.Warn(CultureInfo.InvariantCulture, $"Cricondenbar calculation failed with values: P {stream.Cricondenbar.Pressure.Value}, T {stream.Cricondenbar.Temperature.Value}. Retrying with initial values.");
                            logger.Debug(CultureInfo.InvariantCulture, "Calculating Cricondenbar({0}, {1})", stream.Cricondenbar.Pressure.InitialValue, stream.Cricondenbar.Temperature.InitialValue);
                            res = stream.Umrol.Cricondenbar(stream.Cricondenbar.Pressure.InitialValue, stream.Cricondenbar.Temperature.InitialValue);
                        }
                        if (double.IsNaN(res.p) || double.IsNaN(res.t))
                        {
                            logger.Warn(CultureInfo.InvariantCulture, $"Cricondenbar calculation failed with initial values: P {stream.Cricondenbar.Pressure.InitialValue}, T {stream.Cricondenbar.Temperature.InitialValue}. Retrying with no initial values.");
                            logger.Debug(CultureInfo.InvariantCulture, "Calculating Cricondenbar({0}, {1})", -1, -1);
                            res = stream.Umrol.Cricondenbar();
                        }
                        if (double.IsNaN(res.p) || double.IsNaN(res.t))
                        {
                            logger.Error(CultureInfo.InvariantCulture, $"Cricondenbar calculation failed");
                        }
                        else
                        {
                            stream.Cricondenbar.Pressure.Value = res.p;
                            stream.Cricondenbar.Temperature.Value = res.t;
                            stream.Cricondenbar.Pressure.Quality = StatusCodes.Good;
                            stream.Cricondenbar.Temperature.Quality = StatusCodes.Good;
                        }

                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Cricondenbar pressure Value: {1} Unit: \"{2}\" NodeId: \"{3}\" Quality \"{4}\"",
                            stream.Name,
                            stream.Cricondenbar.Pressure.GetUnitConverted(),
                            stream.Cricondenbar.Pressure.Unit,
                            stream.Cricondenbar.Pressure.NodeId,
                            stream.Cricondenbar.Pressure.Quality);

                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Cricondenbar temperature Value: {1} Unit: \"{2}\" NodeId: \"{3}\" Quality \"{4}\"",
                            stream.Name,
                            stream.Cricondenbar.Temperature.GetUnitConverted(),
                            stream.Cricondenbar.Temperature.Unit,
                            stream.Cricondenbar.Temperature.NodeId,
                            stream.Cricondenbar.Temperature.Quality);
                    }
                    catch (System.ComponentModel.Win32Exception e)
                    {
                        logger.Error(e, "Error calculating cricondenbar.");
                    }
                }

                // Liquid dropouts
                foreach (var dropOut in stream.LiquidDropouts.Item)
                {
                    dropOut.DewPoint.Quality = StatusCodes.Bad;
                    dropOut.DewPointMargin.Quality = StatusCodes.Bad;
                    dropOut.DropoutPoint.Quality = StatusCodes.Bad;

                    try
                    {
                        logger.Debug(CultureInfo.InvariantCulture, "Stream: \"{0}\" Working point \"{1}\": Calculating dewp({2}, {3})", stream.Name, dropOut.Name, dropOut.Temperature.GetUMRConverted(), dropOut.DewPoint.Value);
                        double dewpoint = stream.Umrol.Dewp(dropOut.Temperature.GetUMRConverted(), dropOut.DewPoint.Value);
                        if (dewpoint == 1000.0 || dewpoint < 5.0)
                        {
                            logger.Warn(CultureInfo.InvariantCulture, "Stream: \"{0}\" Working point \"{1}\": Failed to calculate dew point. Retrying with initial value.", stream.Name, dropOut.Name);
                            dewpoint = stream.Umrol.Dewp(dropOut.Temperature.GetUMRConverted(), dropOut.DewPoint.InitialValue);
                        }
                        if (dewpoint == 1000.0 || dewpoint < 5.0)
                        {
                            logger.Error(CultureInfo.InvariantCulture, "Stream: \"{0}\" Working point \"{1}\": Failed to calculate dew point.", stream.Name, dropOut.Name);
                        }
                        else
                        {
                            dropOut.DewPoint.Value = dewpoint;
                            dropOut.DewPoint.Quality = StatusCodes.Good;
                            dropOut.DewPointMargin.Quality = StatusCodes.Good;
                        }

                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Working point \"{1}\": Dew point: Pressure: {2} Unit: \"{3}\" Pressure NodeId: \"{4}\" Quality \"{5}\"",
                            stream.Name, dropOut.Name,
                            dropOut.DewPoint.GetUnitConverted(),
                            dropOut.DewPoint.Unit,
                            dropOut.DewPoint.NodeId,
                            dropOut.DewPoint.Quality);
                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Working point \"{1}\": Dew point margin: {2} NodeId: \"{3}\" Quality \"{4}\"",
                            stream.Name, dropOut.Name,
                            dropOut.GetDewPointMargin(), dropOut.DewPointMargin.NodeId, dropOut.DewPoint.Quality);

                        double p_max = 300.0;
                        double p_min = 10.0;
                        if (dewpoint != 1000.0 && dewpoint > 5.0)
                        {
                            p_max = dewpoint;
                        }

                        double dropout_pressure = stream.Umrol
                            .DropoutSearch(dropOut.DropoutPoint.DropoutPercent,
                                dropOut.Temperature.GetUMRConverted(),
                                p_min,
                                p_max, raw: dropOut.Raw);
                        if (dropout_pressure > 0)
                        {
                            dropOut.DropoutPoint.Value = dropout_pressure;
                            dropOut.DropoutPoint.Quality = StatusCodes.Good;
                        }
                        
                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Working point \"{1}\": Dropout point: Pressure {2} Unit: \"{3}\" Pressure NodeId: \"{4}\"Quality \"{5}\"",
                            stream.Name, dropOut.Name,
                            dropOut.DropoutPoint.GetUnitConverted(),
                            dropOut.DropoutPoint.Unit,
                            dropOut.DropoutPoint.NodeId,
                            dropOut.DropoutPoint.Quality);
                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Working point \"{1}\": Dropout point margin: {2} NodeId: \"{3}\"",
                            stream.Name, dropOut.Name,
                            dropOut.GetDropoutMargin(), dropOut.DropoutPointMargin.NodeId);

                        var dropoutResult = stream.Umrol
                            .Dropout(dropOut.Pressure.GetUMRConverted(),
                                dropOut.Temperature.GetUMRConverted());
                        if (dropOut.Raw)
                        {
                            dropOut.DropoutValue.Value = dropoutResult.ldom1 * 100.0;
                        }
                        else
                        {
                            dropOut.DropoutValue.Value = dropoutResult.ldom2 * 100.0;
                        }

                        logger.Debug(CultureInfo.InvariantCulture,
                            "Stream: \"{0}\" Working point \"{1}\" Dropout value: {2} NodeId: \"{3}\"",
                            stream.Name, dropOut.Name,
                            dropOut.GetDropoutValue(), dropOut.DropoutValue.NodeId);
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
            WriteValueCollection wvc = new();

            foreach (var stream in config.Streams.Item)
            {
                if (!string.IsNullOrEmpty(stream.Cricondenbar.Pressure.NodeId) && stream.Cricondenbar.Pressure.IsValid() && StatusCode.IsGood(stream.Cricondenbar.Pressure.Quality))
                {
                    wvc.Add(new WriteValue
                    {
                        NodeId = stream.Cricondenbar.Pressure.NodeId,
                        AttributeId = Attributes.Value,
                        Value = new DataValue { Value = stream.Cricondenbar.GetPressure() }
                    });
                }

                if (!string.IsNullOrEmpty(stream.Cricondenbar.Temperature.NodeId) && stream.Cricondenbar.Temperature.IsValid() && StatusCode.IsGood(stream.Cricondenbar.Temperature.Quality))
                {

                    wvc.Add(new WriteValue
                    {
                        NodeId = stream.Cricondenbar.Temperature.NodeId,
                        AttributeId = Attributes.Value,
                        Value = new DataValue { Value = stream.Cricondenbar.GetTemperature() }
                    });
                }

                foreach (var dropout in stream.LiquidDropouts.Item)
                {
                    if (!string.IsNullOrEmpty(dropout.DewPointMargin.NodeId) && StatusCode.IsGood(dropout.DewPointMargin.Quality))
                    {
                        wvc.Add(new WriteValue
                        {
                            NodeId = dropout.DewPointMargin.NodeId,
                            AttributeId = Attributes.Value,
                            Value = new DataValue { Value = dropout.GetDewPointMargin() }
                        });
                    }

                    if (!string.IsNullOrEmpty(dropout.DewPoint.NodeId) && StatusCode.IsGood(dropout.DewPoint.Quality))
                    {
                        wvc.Add(new WriteValue
                        {
                            NodeId = dropout.DewPoint.NodeId,
                            AttributeId = Attributes.Value,
                            Value = new DataValue { Value = dropout.GetDewPoint() }
                        });
                    }

                    if (!string.IsNullOrEmpty(dropout.DropoutPointMargin.NodeId) && StatusCode.IsGood(dropout.DropoutPointMargin.Quality))
                    {
                        wvc.Add(new WriteValue
                        {
                            NodeId = dropout.DropoutPointMargin.NodeId,
                            AttributeId = Attributes.Value,
                            Value = new DataValue { Value = dropout.GetDropoutMargin() }
                        });
                    }

                    if (!string.IsNullOrEmpty(dropout.DropoutPoint.NodeId) && StatusCode.IsGood(dropout.DropoutPoint.Quality))
                    {
                        wvc.Add(new WriteValue
                        {
                            NodeId = dropout.DropoutPoint.NodeId,
                            AttributeId = Attributes.Value,
                            Value = new DataValue { Value = dropout.GetDropoutPoint() }
                        });
                    }

                    if (!string.IsNullOrEmpty(dropout.DropoutValue.NodeId) && StatusCode.IsGood(dropout.DropoutValue.Quality))
                    {
                        wvc.Add(new WriteValue
                        {
                            NodeId = dropout.DropoutValue.NodeId,
                            AttributeId = Attributes.Value,
                            Value = new DataValue { Value = dropout.GetDropoutValue() }
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

            if (config.ReadOnly)
            {
                logger.Info("Read only mode active. No values written to OPC server.");
                return;
            }

            // Write values to OPC server
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

                subscription.Delete(true);
                opcClient.OpcSession.RemoveSubscription(subscription);
                opcClient.DisConnect();
            }
            logger.Info("Stopping service.");
        }

        private void OnMonitoredItemNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            lock (WorkerLock)
            {
                try
                {
                    MonitoredItemNotification notification = e.NotificationValue as MonitoredItemNotification;
                    logger.Debug(CultureInfo.InvariantCulture, "Subscription: {0}, Notification: {1} \"{2}\" Value = {3}", monitoredItem.Subscription.Id, notification.Message.SequenceNumber, monitoredItem.DisplayName, notification.Value);

                    if (notification != null)
                    {
                        foreach (var stream in config.Streams.Item)
                        {
                            foreach (var comp in stream.Composition.Item)
                            {
                                if (string.IsNullOrEmpty(comp.NodeId)) { continue; }

                                if (monitoredItem.StartNodeId.ToString() == comp.NodeId)
                                {
                                    comp.Value = Convert.ToDouble(notification.Value.Value);
                                }
                            }

                            foreach (var dropout in stream.LiquidDropouts.Item)
                            {
                                if (monitoredItem.StartNodeId.ToString() == dropout.Pressure.NodeId)
                                {
                                    dropout.Pressure.Value = Convert.ToDouble(notification.Value.Value);
                                }

                                if (monitoredItem.StartNodeId.ToString() == dropout.Temperature.NodeId)
                                {
                                    dropout.Temperature.Value = Convert.ToDouble(notification.Value.Value);
                                }

                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "OnMonitoredItemNotification error");
                }
            }
        }

        private void GenerateNodeIdString(IEnumerable<Measurement> measurement)
        {
            BrowsePathCollection pathsToTranslate = new();
            List<string> paths = new();
            TypeTable typeTable = new(new NamespaceTable());

            foreach (var m in measurement)
            {
                if (string.IsNullOrEmpty(m.Identifier) && string.IsNullOrEmpty(m.RelativePath))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(m.Identifier) && !string.IsNullOrEmpty(m.RelativePath))
                {
                    logger.Warn(CultureInfo.InvariantCulture, "Identifier \"{0}\" and RelativePath \"{1}\" defined for \"{2}\". Identifier will be used and RelativePath will be ignored.", m.Identifier, m.RelativePath, m.Name);
                }

                string namespaceURI = config.DefaultNamespaceURI;
                if (!string.IsNullOrEmpty(m.NamespaceURI)) { namespaceURI = m.NamespaceURI; }
                int namespaceIndex = Array.IndexOf(opcClient.OpcSession.NamespaceUris.ToArray(), namespaceURI);
                if (namespaceIndex < 0)
                {
                    logger.Error(CultureInfo.InvariantCulture, "Namespace URI \"{0}\" not found.", namespaceURI);
                    continue;
                }

                if (!string.IsNullOrEmpty(m.RelativePath) && string.IsNullOrEmpty(m.Identifier))
                {
                    BrowsePath pathToTranslate = new();
                    NodeId startNode;

                    if (!string.IsNullOrEmpty(m.StartIdentifier))
                    {
                        startNode = new NodeId(String.Format("ns={0};{1}", namespaceIndex, m.StartIdentifier));
                    }
                    else
                    {
                        startNode = new NodeId(ObjectIds.ObjectsFolder);
                    }

                    paths.Add(m.RelativePath);

                    RelativePath path = RelativePath.Parse(m.RelativePath, typeTable);
                    path.Elements.ForEach(e => { e.TargetName = new QualifiedName(e.TargetName.Name, (ushort)namespaceIndex); });

                    pathToTranslate.StartingNode = startNode;
                    pathToTranslate.RelativePath = path;
                    pathsToTranslate.Add(pathToTranslate);
                }

                if (!string.IsNullOrEmpty(m.Identifier))
                {
                    m.NodeId = String.Format("ns={0};{1}", namespaceIndex, m.Identifier);
                }
            }

            if (pathsToTranslate.Count > 0)
            {
                BrowsePathResultCollection results;
                DiagnosticInfoCollection diagnosticInfos;
                try
                {
                    opcClient.OpcSession.TranslateBrowsePathsToNodeIds(null, pathsToTranslate, out results, out diagnosticInfos);
                }
                catch (Exception e)
                {
                    logger.Fatal(e, "Failed to translate browse paths.");
                    throw;
                }

                foreach (var m in measurement)
                {
                    if (string.IsNullOrEmpty(m.Identifier) && !string.IsNullOrEmpty(m.RelativePath))
                    {
                        int index = Array.IndexOf(paths.ToArray(), m.RelativePath);
                        if (index >= 0 && StatusCode.IsGood(results[index].StatusCode))
                        {
                            m.NodeId = results[index].Targets[0].TargetId.ToString();
                            logger.Debug(CultureInfo.InvariantCulture, "RelativePath \"{0}\" translates to NodeId \"{1}\"", m.RelativePath, m.NodeId);
                        }
                        else
                        {
                            logger.Error(CultureInfo.InvariantCulture, "RelativePath \"{0}\" failed to translate: \"{1}\"", m.RelativePath, results[index].StatusCode);
                        }
                    }
                }
            }
        }

        private void GenerateNodeIds()
        {
            foreach (var stream in config.Streams.Item)
            {
                GenerateNodeIdString(stream.Composition.Item);

                List<Measurement> list = new()
                {
                    stream.Cricondenbar.Pressure,
                    stream.Cricondenbar.Temperature,
                };

                foreach (var dropout in stream.LiquidDropouts.Item)
                {
                    list.Add(dropout.Pressure);
                    list.Add(dropout.Temperature);
                    list.Add(dropout.DewPoint);
                    list.Add(dropout.DewPointMargin);
                    list.Add(dropout.DropoutPoint);
                    list.Add(dropout.DropoutValue);
                }

                GenerateNodeIdString(list);
            }
        }
    }
}
