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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace WorkerHost
{
    public class IoTHubMessageHandler
    {
        private const int DEFAULT_BUFFER_SIZE = 200;
        private const int MIN_COUNT_FOR_ANALYSIS = 10;

        private static int _bufferSize;

        private Dictionary<string, CircularBuffer<SensorDataContract>> _buffers;
        private object _lock = new object();
        private string _measureNameFilter;

        public IoTHubMessageHandler(int messagesBufferSize, string measureNameFilter)
        {
            if (messagesBufferSize == 0)
            {
                _bufferSize = DEFAULT_BUFFER_SIZE;
            }
            else
            {
                _bufferSize = messagesBufferSize;
            }
            _buffers = new Dictionary<string, CircularBuffer<SensorDataContract>>();
            _measureNameFilter = measureNameFilter;
        }

        // adds measurement data from IoTHub to circular buffer
        public void Process(IDictionary<string, object> messagePayload)
        {
            // Filter on MeasureName and time
            if ((messagePayload.ContainsKey(_measureNameFilter)) &&
                (messagePayload.ContainsKey("time")))
            {
                DateTime eventTime;
                if (DateTime.TryParse(messagePayload["time"].ToString(), out eventTime))
                {
                    // the measure is an array of values.
                    // array position maps to joint in device
                    IEnumerable<object> measures = messagePayload[_measureNameFilter] as IEnumerable<object>;
                    if (measures != null)
                    {
                        int index = 0;
                        foreach (var ovalue in measures)
                        {
                            try
                            {
                                var val = (Newtonsoft.Json.Linq.JValue)ovalue;
                                double value = (double) val.Value;

                                var sensorData = new SensorDataContract
                                {
                                    MeasureName = _measureNameFilter,
                                    TimeCreated = eventTime,
                                    Index = index,
                                    Value = value
                                };

                                // UniqueId treats each joint as a separate device for anomaly detection
                                var from = sensorData.UniqueId();

                                lock (_lock)
                                {
                                    CircularBuffer<SensorDataContract> buffer;
                                    if (!_buffers.TryGetValue(from, out buffer))
                                    {
                                        buffer = new CircularBuffer<SensorDataContract>(_bufferSize);
                                        _buffers.Add(from, buffer);
                                    }

                                    buffer.Add(sensorData);
#if DEBUG_LOG
                                    Console.WriteLine("Data from device {0}, Total count: {1}", from, buffer.Count);
#endif
                                }
                            }
                            catch (Exception)
                            {
#if DEBUG_LOG
                                Trace.TraceError("Ignored invalid event data: {0}", line);
#endif
                            }
                            index++;
                        }
                    }
                }
            }

        }

        public Dictionary<string, SensorDataContract[]> GetHistoricData()
        {
            lock (_lock)
            {
                return _buffers.Where(kvp => kvp.Value.Count > MIN_COUNT_FOR_ANALYSIS)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetAll());
            }
        }
    }
}