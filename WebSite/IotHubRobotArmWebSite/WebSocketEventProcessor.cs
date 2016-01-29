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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using Microsoft.ServiceBus.Messaging;

using Newtonsoft.Json;

namespace IotHubRobotArmWebSite
{
	class WebSocketEventProcessor : IEventProcessor
	{
		// Keep a buffer of all messages for as long as the client UX needs them
		static TimeSpan bufferTimeInterval = new TimeSpan(0, 10, 0);

		// Message buffer (one per processor instance)
		static SortedList<DateTime, List<IDictionary<string, object>>> sortedDataBuffer = new SortedList<DateTime, List<IDictionary<string, object>>>();
		Stopwatch checkpointStopWatch;
		PartitionContext partitionContext;

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> events)
		{
			try
			{
				var now = DateTime.UtcNow;
                bool sendAlertToDevice = false;

				foreach (var eventData in events)
				{
					// We don't care about messages that are older than bufferTimeInterval
					if ((eventData.EnqueuedTimeUtc + bufferTimeInterval) >= now)
					{
						// Get message from the eventData body and convert JSON string into message object
						string eventBodyAsString = Encoding.UTF8.GetString(eventData.GetBytes()).TrimStart();
                        Debug.Print(eventBodyAsString);

                        // There can be several messages in one
                        IList<IDictionary<string, object>> messagePayloads;
                        // simple check if this is an array
                        if (eventBodyAsString.StartsWith("["))
                        {
                            messagePayloads = JsonConvert.DeserializeObject<IList<IDictionary<string, object>>>(eventBodyAsString);
                        }
                        else
                        {
                            // not an array. May be single object, or list of objects
                            try
                            {
                                // Attempt to deserialze event body as single JSON message
                                messagePayloads = new List<IDictionary<string, object>>
                                {
                                    JsonConvert.DeserializeObject<IDictionary<string, object>>(eventBodyAsString)
                                };
                            }
                            catch
                            {
                                // Not a single JSON message: attempt to deserialize as array of messages

                                // Azure Stream Analytics Preview generates invalid JSON for some multi-values queries
                                // Workaround: turn concatenated json objects (ivalid JSON) into array of json objects (valid JSON)
                                if (eventBodyAsString.IndexOf("}{") >= 0)
                                {
                                    eventBodyAsString = eventBodyAsString.Replace("}{", "},{");
                                }
                                if (eventBodyAsString.IndexOf("}\r\n{") >= 0)
                                {
                                    eventBodyAsString = eventBodyAsString.Replace("}\r\n{", "},{");
                                }
                                if (!eventBodyAsString.EndsWith("]"))
                                    eventBodyAsString = eventBodyAsString + "]";
                                if (!eventBodyAsString.StartsWith("["))
                                    eventBodyAsString = "[" + eventBodyAsString.Substring(eventBodyAsString.IndexOf("{"));

                                messagePayloads = JsonConvert.DeserializeObject<IList<IDictionary<string, object>>>(eventBodyAsString);
                            }
                        }

						var rnd = new Random();
                        bool sendAsbulk = false;
                        if (messagePayloads.Count > 1)
                        {
                            // sending several measurements. Set bulk mode to reduce graph updates
                            MyWebSocketHandler.SendStringToClients("{ \"bulkData\": true }");
                            sendAsbulk = true;
                        }
						foreach (var messagePayload in messagePayloads)
						{
                            // Read time value
                            if (!messagePayload.ContainsKey("time"))
                            {
                                messagePayload["time"] = eventData.EnqueuedTimeUtc;
                            }
                            // process an alert
                            if (messagePayload.ContainsKey("alerttype") && messagePayload.ContainsKey("time"))
							{
                                Debug.Print("Alert message received!");

                                DateTime time = DateTime.Parse(messagePayload["time"].ToString());
								// find the nearest point
								lock (sortedDataBuffer)
								{
                                    int idx = SearchHelper.FindFirstIndexGreaterThanOrEqualTo(sortedDataBuffer, time);
									bool found = false;
									string alertType = messagePayload["alerttype"] as string;

									if (idx >= sortedDataBuffer.Values.Count)
										idx = sortedDataBuffer.Values.Count - 1;

									while (idx >= 0)
									{
										List<IDictionary<string, object>> dictList = sortedDataBuffer.Values[idx];
										foreach (IDictionary<string, object> dict in dictList)
										{
											if (
                                                (dict.ContainsKey("guid") && messagePayload.ContainsKey("guid") && messagePayload["guid"].ToString() == dict["guid"].ToString())
                                                &&
                                                (dict.ContainsKey("measurename") && messagePayload.ContainsKey("measurename") && messagePayload["measurename"].ToString() == dict["measurename"].ToString())
                                                &&
                                                (!messagePayload.ContainsKey("displayname") || dict.ContainsKey("displayname") && messagePayload["measurename"].ToString() == dict["measurename"].ToString())
                                                )
											{
												// fill anomaly message
											    if (!messagePayload.ContainsKey("value"))
											    {
                                                    messagePayload["value"] = dict["value"];
											    }
                                                if (!messagePayload.ContainsKey("displayname") && dict.ContainsKey("displayname"))
											    {
                                                    messagePayload["displayname"] = dict["displayname"];
											    }   

												found = true;
												break;
											}
										}
										if (found) break;
										idx--;
									}
								}
							}

							// We want to read the time value from the message itself.
							// If none is found we will use the enqueued time
							DateTime messageTimeStamp = new DateTime();
							if (messagePayload.ContainsKey("time"))
							{
								messageTimeStamp = DateTime.Parse(messagePayload["time"].ToString());
							}
							else if (messagePayload.ContainsKey("timestart"))
								messageTimeStamp = DateTime.Parse(messagePayload["timestart"].ToString());
							else messageTimeStamp = eventData.EnqueuedTimeUtc;


							// Notify clients
						    MyWebSocketHandler.SendToClients(messagePayload);

                            // if using machine learning anomaly detection and not an alert
                            // pass measurement event on to MLWorker
                            if (Global.globalSettings.UseMachineLearning &&
                                !messagePayload.ContainsKey("alerttype"))
                            {
                                WorkerHost.MLWorker.ProcessPayload(messagePayload);
                            }

                            if (messagePayload.ContainsKey("alerttype") &&
                                messagePayload.ContainsKey("isanomaly"))
                            {
                                sendAlertToDevice = true;
                            }

                            // Buffer messages so we can resend them to clients that connect later
                            // or when a client requests data for a different device

                            // Lock to guard against concurrent reads from client resend
                            lock (sortedDataBuffer)
							{
								if (!sortedDataBuffer.ContainsKey(messageTimeStamp))
									sortedDataBuffer.Add(messageTimeStamp, new List<IDictionary<string, object>>());

								sortedDataBuffer[messageTimeStamp].Add(messagePayload);
							}
						}
                        if (sendAsbulk)
                        {
                            MyWebSocketHandler.SendStringToClients("{ \"bulkData\": false }");
                            sendAsbulk = false;
                        }
                    }
                    else
					{
						Debug.Print("Received old message timestamped:" + eventData.EnqueuedTimeUtc.ToString());
					}
				}

				//Call checkpoint every minute
				if (this.checkpointStopWatch.Elapsed > TimeSpan.FromMinutes(1))
				{
					await context.CheckpointAsync();
					lock (this)
					{
						this.checkpointStopWatch.Restart();
					}

					// trim data buffer to keep only last 10 minutes of data
					lock (sortedDataBuffer)
					{
						DateTime oldDataPoint = now - bufferTimeInterval;
						// find the closest point
						int idx = SearchHelper.FindFirstIndexGreaterThanOrEqualTo(sortedDataBuffer, oldDataPoint);
						// trim
						while (idx > 0 && sortedDataBuffer.Count > 0 && sortedDataBuffer.Keys[0] <= oldDataPoint)
						{
							sortedDataBuffer.RemoveAt(0);
						}
					}
				}

                if (sendAlertToDevice)
                {
                    // send "alert" to device
                    string devId = Global.globalSettings.deviceId;
                    DeviceCommands devcmd = new DeviceCommands();
                    devcmd.Init(devId, "alert");
                    await devcmd.SendMessageToDeviceAsync();
                }
            }
			catch (Exception e)
			{
				Trace.TraceError("Error processing events in EH {0}, partition {1}: {0}",
					 context.EventHubPath, context.Lease.PartitionId, e.Message);
			}
		}

		public Task OpenAsync(PartitionContext context)
		{
			Trace.TraceInformation(
				 String.Format("Opening processor for EH {0}, partition {1}.",
					  context.EventHubPath, context.Lease.PartitionId));
			lock (g_processors)
			{
				try
				{
					g_processors.Add(this);
				}
				catch (Exception e)
				{
					Trace.TraceError("Exception while adding processor for EH {0}, partition {1}: {2}",
						 context.EventHubPath, context.Lease.PartitionId, e.Message);
				}
			}

			this.partitionContext = context;
			this.checkpointStopWatch = new Stopwatch();
			this.checkpointStopWatch.Start();

			return Task.FromResult<object>(null);
		}

		public async Task CloseAsync(PartitionContext context, CloseReason reason)
		{
			Trace.TraceInformation(
				 String.Format("Closing processor for EH {0}, partition {1}. Reason: {2}",
					  context.EventHubPath, context.Lease.PartitionId, reason));

			lock (g_processors)
			{
				try
				{
					g_processors.Remove(this);
				}
				catch (Exception e)
				{
					Trace.TraceError(
						 String.Format("Exception while removing processor for EH {0}, partition {1}: {2}",
							  context.EventHubPath, context.Lease.PartitionId, e.Message));
				}
			}

			if (reason == CloseReason.Shutdown)
			{
				await context.CheckpointAsync();
			}
		}

		public static void ExceptionReceived(object sender, ExceptionReceivedEventArgs e)
		{
			Trace.TraceError("Exception received from EventHostProcessor: {0} - {1}, {2}", e.Exception, e.Action, sender);
		}

		static List<WebSocketEventProcessor> g_processors = new List<WebSocketEventProcessor>();

	}
	public static class SearchHelper
	{
		private static int BinarySearch<T>(IList<T> list, T value)
		{
			if (list == null)
				throw new ArgumentNullException("list");
			var comp = Comparer<T>.Default;
			int lo = 0, hi = list.Count - 1;
			while (lo < hi)
			{
				int m = (hi + lo) / 2;  // this might overflow; be careful.
				if (comp.Compare(list[m], value) < 0) lo = m + 1;
				else hi = m - 1;
			}
			if (lo < list.Count && comp.Compare(list[lo], value) < 0) lo++;
			return lo;
		}

		public static int FindFirstIndexGreaterThanOrEqualTo<T, U>
										  (this SortedList<T, U> sortedList, T key)
		{
			return BinarySearch(sortedList.Keys, key);
		}

	}
}
