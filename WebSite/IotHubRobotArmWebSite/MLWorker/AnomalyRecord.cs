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

namespace WorkerHost
{
    public class AnomalyRecord
    {
        public DateTime Time { get; set; }
        public double Data { get; set; }
        public int Spike1 { get; set; }
        public int Spike2 { get; set; }
        public double LevelScore { get; set; }
        public int LevelAlert { get; set; }
        public double TrendScore { get; set; }
        public int TrendAlert { get; set; }

        public static AnomalyRecord Parse(string[] values)
        {
            if (values.Length < 8)
                throw new ArgumentException("Anomaly Record expects 8 values.");
            return new AnomalyRecord()
            {

                Time = DateTime.Parse(values[0]),
                Data = double.Parse(values[1]),
                Spike1 = int.Parse(values[2]),
                Spike2 = int.Parse(values[3]),
                LevelScore = double.Parse(values[4]),
                LevelAlert = int.Parse(values[5]),
                TrendScore = double.Parse(values[6]),
                TrendAlert = int.Parse(values[7]),
            };
        }

        public override string ToString()
        {
            return Time.ToLocalTime() + ", " + Data + ", " + Spike1 + ", " + Spike2 + ", " +
                LevelScore + ", " + LevelAlert + ", " + TrendScore + ", " + TrendAlert;
        }
    }
}
