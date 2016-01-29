//  ---------------------------------------------------------------------------------
//  Copyright (c) Microsoft Open Technologies, Inc.  All rights reserved.
// 
//  The MIT License (MIT)
// 
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
//  ---------------------------------------------------------------------------------

//#define DEBUG_LOG
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Diagnostics;
using Newtonsoft.Json;


namespace WorkerHost
{
    public class MLWorker
    {
        public class Configuration
        {
            public string AlertEHConnectionString;
            public string AlertEHName;
            public string AnomalyDetectionApiUrl;
            public string AnomalyDetectionAuthKey;
            public string LiveId;
            public string MeasureNameFilter;

            public string TukeyThresh;
            public string ZscoreThresh;

            public bool UseMarketApi;
            public int MessagesBufferSize;
            public int AlertsIntervalSec;

            public string StorageConnectionString;
            public string BlobContainerName;
            public string BlobNamePrefix;
            public string SqlDatabaseConnectionString;
        }

        private static Analyzer        _analyzer;
        private static IoTHubMessageHandler _IoTHubMessageHandler;
        private static Timer           _timer;

        //private static BlobWriter _blobWriter;
        //private static SQLOutputRepository _sqlOutputRepository;

        public static void RunMLHost()
        {
            StartHost("LocalWorker");
        }

        private static void StartHost(string consumerGroupPrefix)
        {
            Trace.WriteLine("Starting Worker...");
#if DEBUG_LOG
            RoleEnvironment.TraceSource.TraceInformation("Starting Worker...");
#endif
            var config = new Configuration();

            config.AlertEHConnectionString = ConfigurationManager.AppSettings.Get("Microsoft.ServiceBus.ConnectionStringAlerts");
            config.AlertEHName = ConfigurationManager.AppSettings.Get("Microsoft.ServiceBus.EventHubAlerts");

            config.AnomalyDetectionApiUrl = ConfigurationManager.AppSettings.Get("AnomalyDetectionApiUrl");
            config.AnomalyDetectionAuthKey = ConfigurationManager.AppSettings.Get("AnomalyDetectionAuthKey");
            config.LiveId = ConfigurationManager.AppSettings.Get("LiveId");

            config.MeasureNameFilter = ConfigurationManager.AppSettings.Get("MeasureNameFilter");

            config.TukeyThresh = ConfigurationManager.AppSettings.Get("TukeyThresh");
            config.ZscoreThresh = ConfigurationManager.AppSettings.Get("ZscoreThresh");

            bool.TryParse(ConfigurationManager.AppSettings.Get("UseMarketApi"), out config.UseMarketApi);

            int.TryParse(ConfigurationManager.AppSettings.Get("MessagesBufferSize"), out config.MessagesBufferSize);
            int.TryParse(ConfigurationManager.AppSettings.Get("AlertsIntervalSec"), out config.AlertsIntervalSec);

            config.StorageConnectionString = ConfigurationManager.AppSettings.Get("Microsoft.Storage.ConnectionString");
            config.BlobContainerName = ConfigurationManager.AppSettings.Get("blobContainerName");
            config.BlobNamePrefix = ConfigurationManager.AppSettings.Get("blobNamePrefix");
            config.SqlDatabaseConnectionString = ConfigurationManager.AppSettings.Get("sqlDatabaseConnectionString");

            _analyzer = new Analyzer(config.AnomalyDetectionApiUrl, config.AnomalyDetectionAuthKey,
                config.LiveId, config.UseMarketApi, config.TukeyThresh, config.ZscoreThresh);

            _IoTHubMessageHandler = new IoTHubMessageHandler(config.MessagesBufferSize, config.MeasureNameFilter);

            //if (ConfigurationManager.AppSettings.Get("sendToBlob") == "true")
            //{
            //    _blobWriter = new BlobWriter();
            //    if (_blobWriter.Connect(config.BlobNamePrefix, config.BlobContainerName, config.StorageConnectionString))
            //    {
            //        _blobWriter = null;
            //    }
            //}
            //if (ConfigurationManager.AppSettings.Get("sendToSQL") == "true")
            //{
            //    _sqlOutputRepository = new SQLOutputRepository(config.SqlDatabaseConnectionString);
            //}
            Process(config);
        }

        // static method used by hosting code to process event data
        public static void ProcessPayload(IDictionary<string, object> messagePayload)
        {
            _IoTHubMessageHandler.Process(messagePayload);
        }

        // this uses a timer to preiodally get data
        // accumulated in circular buffer, and send it to anomaly detection
        // if an anomaly is detected sends the data to alert event hub
        public static void Process(Configuration config)
        {
            var alertEventHub = EventHubClient.CreateFromConnectionString(config.AlertEHConnectionString, config.AlertEHName);

            var timerInterval = TimeSpan.FromSeconds(1);
            var alertLastTimes = new Dictionary<string, DateTime>();

            TimerCallback timerCallback = state =>
            {

                var historicData = _IoTHubMessageHandler.GetHistoricData();

                try
                {
                    var tasks = historicData.ToDictionary(kvp => kvp.Key, kvp => _analyzer.Analyze(kvp.Value));

                    Task.WaitAll(tasks.Values.ToArray());

                    List<SensorDataContract> alertsToSQl = new List<SensorDataContract>();

                    foreach (var kvp in tasks)
                    {
                        var key = kvp.Key;
                        var alerts = kvp.Value.Result;

                        DateTime alertLastTime;
                        if (!alertLastTimes.TryGetValue(@key, out alertLastTime))
                        {
                            alertLastTime = DateTime.MinValue;
                        }

                        
                        foreach (var alert in alerts)
                        {
                            if ((alert.Time - alertLastTime).TotalSeconds >= config.AlertsIntervalSec)
                            {
                                Trace.TraceInformation("Alert - {0}", alert.ToString());

                                string eventJSON = OutputResults(key, historicData[key].LastOrDefault(), alert);
                                alertEventHub.Send(
                                    new EventData(Encoding.UTF8.GetBytes(eventJSON)));

                                alertLastTime = alert.Time;
                                alertLastTimes[@key] = alertLastTime;

                                //if (historicData[key].Length > 0)
                                //{
                                //    alertsToSQl.Add(historicData[key].Last());
                                //    if (_blobWriter != null)
                                //    {
                                //        _blobWriter.WriteLine(eventJSON);
                                //    }    
                                //}
                            }
                        }
                    }

                    //if (_sqlOutputRepository != null)
                    //{
                    //    _sqlOutputRepository.ProcessEvents(alertsToSQl);
                    //}
                    //if (_blobWriter != null)
                    //{
                    //    _blobWriter.Flush();
                    //}
                }
#if DEBUG_LOG
                catch (Exception e)
                {
                    Trace.TraceError(e.Message);
                    Trace.TraceError(e.ToString());
                    //throw;
                }
#else
                catch (Exception)
                {
                    //throw;
                }
#endif

                _timer.Change((int)timerInterval.TotalMilliseconds, Timeout.Infinite);
            };

            _timer = new Timer(timerCallback, null, Timeout.Infinite, Timeout.Infinite);
            _timer.Change(0, Timeout.Infinite);

            Trace.TraceInformation("Reading events from Event Hub (press ctrl+c to abort)");
        }

        public static IEnumerable<IEnumerable<T>> Batch<T>(IEnumerable<T> collection, int batchSize)
        {
            List<T> nextbatch = new List<T>(batchSize);
            foreach (T item in collection)
            {
                nextbatch.Add(item);
                if (nextbatch.Count == batchSize)
                {
                    yield return nextbatch;
                    nextbatch = new List<T>();
                }
            }
            if (nextbatch.Count > 0)
                yield return nextbatch;
        }

        private static string OutputResults(string from, SensorDataContract sensorMeta, AnomalyRecord alert)
        {
            string msg = sensorMeta.MeasureName + " anomaly detected by ML model on joint_" + sensorMeta.Index.ToString();
            return JsonConvert.SerializeObject(
                new
                {
                    measurename = sensorMeta.MeasureName,
                    time = alert.Time,
                    value = alert.Data,
                    index = sensorMeta.Index,
                    alerttype = "MLModelAlert",
                    isanomaly = 1,
                    message = msg
                });
        }

    }
}
