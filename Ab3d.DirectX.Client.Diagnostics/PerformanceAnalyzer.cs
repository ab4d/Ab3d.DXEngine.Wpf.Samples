using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Ab3d.DirectX.Controls;

namespace Ab3d.DirectX.Client.Diagnostics
{
    public class PerformanceAnalyzer
    {
        // We have lists of preallocated lists
        // This is beter than having only Lists, because when one list if full (at its capacity) all items are copied to a new array
        // In our case we only add a new List to parent List
        private List<List<RenderingStatistics>> _allCollectedRenderingStatistics;

        private List<RenderingStatistics> _currentRenderingStatisticsList;

        private RenderingStatistics _lastRenderingStatistics;

        private int _currentIndex;

        private int _totalSamplesCount;

        private DXScene _dxScene;
        private readonly DXView _dxView;

        private readonly int _initialCapacity;

        private readonly string _name;

        private bool _isCollectingStatistics;

        private bool _isEnabledCollectingStatistics;

        private Stopwatch _stopwatch;

        private double _timeToFirstFrame;

        private int[] _garbageCollectionsCount;

        public PerformanceAnalyzer(DXView dxView, string name = null, int initialCapacity = 10000)
        {
            if (dxView == null) throw new ArgumentNullException("dxView");

            _dxView = dxView;
            _name = name;
            _initialCapacity = initialCapacity;

            _dxScene = _dxView.DXScene;
        }

        public void StartCollectingStatistics()
        {
            if (_isCollectingStatistics) // Already collecting
                return;

            if (_dxScene == null)
            {
                _dxScene = _dxView.DXScene;
                if (_dxScene == null)
                    throw new Exception("Cannot start collecting statistics because DXScene does not exist (probably was not yet initialized)");
            }

            _allCollectedRenderingStatistics = new List<List<RenderingStatistics>>();

            _currentRenderingStatisticsList = CreatePreallocatedList(_initialCapacity);
            _allCollectedRenderingStatistics.Add(_currentRenderingStatisticsList);

            _currentIndex = 0;
            _totalSamplesCount = 0;
            _timeToFirstFrame = -1;


            // Store the number of time garbage collection has occured
            _garbageCollectionsCount = new int[3];
            for (int i = 0; i < GC.MaxGeneration; i++)
                _garbageCollectionsCount[i] = GC.CollectionCount(i);


            if (!DXDiagnostics.IsCollectingStatistics)
            {
                DXDiagnostics.IsCollectingStatistics = true;
                _isEnabledCollectingStatistics = true; // we will disable collecting statistics when we are finished
            }

            _stopwatch = new Stopwatch();
            _stopwatch.Start();

            _dxScene.AfterFrameRendered += DXSceneOnAfterFrameRendered;

            _isCollectingStatistics = true;
        }

        public void StopCollectingStatistics()
        {
            if (!_isCollectingStatistics) // Already stopped
                return;

            _dxScene.AfterFrameRendered -= DXSceneOnAfterFrameRendered;
            _stopwatch.Stop();

            _isCollectingStatistics = false;

            // Store the number of time garbage collection has occured after we started CollectingStatistics
            for (int i = 0; i < GC.MaxGeneration; i++)
                _garbageCollectionsCount[i] = GC.CollectionCount(i) - _garbageCollectionsCount[i];

            if (_isEnabledCollectingStatistics)
            {
                DXDiagnostics.IsCollectingStatistics = false;
                _isEnabledCollectingStatistics = false;
            }
        }

        public double GetAverageRenderTime()
        {
            if (_totalSamplesCount == 0 || _allCollectedRenderingStatistics == null)
                return 0; // No results

            var doubles = GetTimeValuesArray(statistics => statistics.TotalRenderTimeMs);
            var average = doubles.Average();

            return average;
        }

        public List<KeyValuePair<string, Dictionary<string, double>>> CalculateResults()
        {
            if (_totalSamplesCount == 0 || _allCollectedRenderingStatistics == null) 
                return null; // No results

            var results = new List<KeyValuePair<string, Dictionary<string, double>>>();

            var timeValues = GetTimeValuesArray(statistics => statistics.UpdateTimeMs);
            Dictionary<string, double> commonStatistics = CalculateCommonStatistics(timeValues);
            results.Add(new KeyValuePair<string, Dictionary<string, double>>("UpdateTimeMs", commonStatistics));

            timeValues = GetTimeValuesArray(statistics => statistics.PrepareRenderTimeMs);
            commonStatistics = CalculateCommonStatistics(timeValues);
            results.Add(new KeyValuePair<string, Dictionary<string, double>>("PrepareRenderTimeMs", commonStatistics));

            timeValues = GetTimeValuesArray(statistics => statistics.DrawRenderTimeMs);
            commonStatistics = CalculateCommonStatistics(timeValues);
            results.Add(new KeyValuePair<string, Dictionary<string, double>>("DrawRenderTimeMs", commonStatistics));
            
            timeValues = GetTimeValuesArray(statistics => statistics.PostProcessingRenderTimeMs);
            commonStatistics = CalculateCommonStatistics(timeValues);
            results.Add(new KeyValuePair<string, Dictionary<string, double>>("PostProcessingRenderTimeMs", commonStatistics));

            timeValues = GetTimeValuesArray(statistics => statistics.CompleteRenderTimeMs);
            commonStatistics = CalculateCommonStatistics(timeValues);
            results.Add(new KeyValuePair<string, Dictionary<string, double>>("CompleteRenderTimeMs", commonStatistics));

            timeValues = GetTimeValuesArray(statistics => statistics.TotalRenderTimeMs);
            commonStatistics = CalculateCommonStatistics(timeValues);
            results.Add(new KeyValuePair<string, Dictionary<string, double>>("TotalRenderTimeMs", commonStatistics));

            return results;
        }

        public string GetResultsText(bool addSystemInfo = true,
                                     bool addDXEngineInfo = true,
                                     bool addGarbageCollectionsInfo = true,
                                     bool addTotals = true,
                                     bool addStateChangesStatistics = true,
                                     bool addFirstFrameStatisticsWhenAvailable = true,
                                     bool addRenderingStatistics = true,
                                     bool addMultiThreadingStatistics = true)
        {
            var sb = new StringBuilder();

            if (addSystemInfo)
            {
                if (!string.IsNullOrEmpty(_name))
                    sb.AppendFormat("Name: {0}\r\n", _name);

                sb.AppendFormat("Date: {0:F}\r\n",        DateTime.Now);
                sb.AppendFormat("Computer name: {0}\r\n", Environment.MachineName);

                try
                {
                    var cpuInfo = System.Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
                    if (cpuInfo != null)
                        sb.AppendFormat("CPU: {0}\r\n", cpuInfo);
                }
                catch
                {
                    // pass
                }

                if (_dxScene.DXDevice.Adapter != null && !_dxScene.DXDevice.Adapter.IsDisposed)
                {
                    string adapterDescription = _dxScene.DXDevice.Adapter.Description1.Description.Trim();

                    int pos = adapterDescription.IndexOf('\0'); // UH: In SharpDX 3.1, some adapters report description with many zero characters
                    if (pos >= 0)
                        adapterDescription = adapterDescription.Substring(0, pos);

                    sb.AppendFormat("Adapter: {0}\r\n", adapterDescription);
                }

#if NETCOREAPP
                try
                {
                    sb.AppendFormat("FrameworkDescription: {0}\r\nOSDescription: {1} {2}\r\n",
                        System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                        System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                        System.Runtime.InteropServices.RuntimeInformation.OSArchitecture);
                }
                catch
                {
                    // pass
                }
#endif

                sb.AppendLine();
            }

            if (addDXEngineInfo)
            {
                // Set DXEngine assembly version
                var version = typeof(DXDevice).Assembly.GetName().Version;

                var  fieldInfo            = typeof(DXDevice).GetField("IsDebugVersion");
                bool isDXEngineDebugBuild = fieldInfo != null;

                sb.AppendFormat("DXEngine v{0}.{1}.{2}{3}\r\n", version.Major, version.Minor, version.Build, isDXEngineDebugBuild ? " (debug build)" : "");

                sb.AppendLine();


                sb.AppendFormat("DXScene size: {0} x {1}\r\n",        _dxScene.Width, _dxScene.Height);
                sb.AppendFormat("IsDebugDevice: {0}\r\n",             _dxScene.DXDevice.IsDebugDevice);
                sb.AppendFormat("MaxBackgroundThreadsCount: {0}\r\n", _dxScene.MaxBackgroundThreadsCount);
                sb.AppendFormat("IsCachingCommandLists: {0}\r\n",     _dxScene.IsCachingCommandLists);

                if (_dxView.UsedGraphicsProfile != null)
                    sb.AppendFormat("UsedGraphicsProfile: {0}\r\n", _dxView.UsedGraphicsProfile.DisplayName);
            }


            if (addGarbageCollectionsInfo && _garbageCollectionsCount != null)
            { 
                sb.Append("Garbage collections count: ");
                for (var i = 0; i < GC.MaxGeneration; i++)
                    sb.AppendFormat("gen. {0}: {1}x; ", i, _garbageCollectionsCount[i]);
                sb.AppendLine();

                sb.AppendLine();
            }


            if (addTotals && _totalSamplesCount != 0 && _stopwatch != null)
            {
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Test running duration: {0:0.0} s\r\n", _stopwatch.Elapsed.TotalSeconds);

                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                    "Frames count: {0} ({1:0.0} FPS)\r\n",
                    _totalSamplesCount, (double) _totalSamplesCount / _stopwatch.Elapsed.TotalSeconds);

                double averageRenderTime = GetAverageRenderTime();
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                    "Average render time: {0:0.00} ms (theoretically {1:0.0} FPS)\r\n",
                    averageRenderTime, 1000.0 / averageRenderTime);

                sb.AppendLine();
            }

            if (addStateChangesStatistics && _lastRenderingStatistics != null)
            {
                sb.AppendFormat("DrawCallsCount: {0}\r\n",             _lastRenderingStatistics.DrawCallsCount);
                sb.AppendFormat("DrawnIndicesCount: {0}\r\n",          _lastRenderingStatistics.DrawnIndicesCount);
                sb.AppendFormat("StateChangesCount: {0}\r\n",          _lastRenderingStatistics.StateChangesCount);
                sb.AppendFormat("ShaderChangesCount: {0}\r\n",         _lastRenderingStatistics.ShaderChangesCount);
                sb.AppendFormat("VertexBuffersChangesCount: {0}\r\n",  _lastRenderingStatistics.VertexBuffersChangesCount);
                sb.AppendFormat("IndexBuffersChangesCount: {0}\r\n",   _lastRenderingStatistics.IndexBuffersChangesCount);
                sb.AppendFormat("ConstantBufferChangesCount: {0}\r\n", _lastRenderingStatistics.ConstantBufferChangesCount);

                sb.AppendLine();
            }

            // When we have data about the actual first frame, display it there (the first frame usually takes longer to render because some resources also need to be generated)
            // But if this was just a frame in between, then do not consider it as something special
            if (_timeToFirstFrame > 0 && addFirstFrameStatisticsWhenAvailable && _allCollectedRenderingStatistics != null)
            {
                var firstFrameRenderingStatistics = _allCollectedRenderingStatistics[0][0];

                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Time to first frame: {0:0.00} ms\r\n", _timeToFirstFrame);

                sb.AppendLine("First frame time:");

                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "UpdateTimeMs:               \t{0:0.00} ms\r\n", firstFrameRenderingStatistics.UpdateTimeMs);
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "PrepareRenderTimeMs:        \t{0:0.00} ms\r\n", firstFrameRenderingStatistics.PrepareRenderTimeMs);
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "DrawRenderTimeMs:           \t{0:0.00} ms\r\n", firstFrameRenderingStatistics.DrawRenderTimeMs);
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "PostProcessingRenderTimeMs: \t{0:0.00} ms\r\n", firstFrameRenderingStatistics.PostProcessingRenderTimeMs);
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "CompleteRenderTimeMs:       \t{0:0.00} ms\r\n", firstFrameRenderingStatistics.CompleteRenderTimeMs);
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "TotalRenderTimeMs:          \t{0:0.00} ms\r\n", firstFrameRenderingStatistics.TotalRenderTimeMs);

                // Remove first frame because it is special (takes much longer to render)
                _allCollectedRenderingStatistics[0].RemoveAt(0);
                _totalSamplesCount--;
            }

            if (addRenderingStatistics && _allCollectedRenderingStatistics != null)
            {
                var results = CalculateResults();

                if (results == null || results.Count == 0)
                {
                    sb.Append("\r\nNo frames rendered");
                }
                else
                {
                    int maxLength = results.Max(r => r.Key.Length);

                    sb.AppendLine();
                    sb.AppendFormat("Rendering statistics for {0} frames:\r\n", _totalSamplesCount);
                    sb.Append("Statistics".PadRight(maxLength + 1));
                    sb.AppendLine("\t avrg    stdev     min        q1       q2       q3      c90      c95      max   (all values in ms)");

                    foreach (var keyValuePair in results)
                    {
                        sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                            "{0}\t{1,5:0.00}    {2,5:0.00}    {3,5:0.00}    {4,5:0.00}    {5,5:0.00}    {6,5:0.00}    {7,5:0.00}    {8,5:0.00}    {9,5:0.00}\r\n",
                            keyValuePair.Key.PadRight(maxLength + 1), keyValuePair.Value["average"], keyValuePair.Value["stdev"], keyValuePair.Value["min"],
                            keyValuePair.Value["q1"], keyValuePair.Value["q2"], keyValuePair.Value["q3"], keyValuePair.Value["c90"], keyValuePair.Value["c95"], keyValuePair.Value["max"]);
                    }
                }

                sb.AppendLine();
                sb.AppendLine();
            }

            if (addMultiThreadingStatistics)
            {
                try
                {
                    AddMultiThreadingPerformanceReport(sb);
                }
                catch (Exception ex)
                {
                    sb.AppendLine("Error getting multi-threading report:")
                      .AppendLine(ex.Message);

                    if (ex.InnerException != null)
                        sb.AppendLine(ex.InnerException.Message);
                }
            }

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.NoInlining)] // Do not inline so we get MissingMethodException when calling this method and not the calling method
        private void AddMultiThreadingPerformanceReport(StringBuilder sb)
        {
            if (_dxScene.MaxBackgroundThreadsCount <= 0)
            {
                sb.AppendLine("Multi-threading disabled in DXScene.");
            }
            else
            {
                int maxUsedThreadsCount = 0;
                ForEachCollectedRenderingStatistics(statistics =>
                {
                    if (statistics.RenderedObjectsCountPerThread != null && statistics.RenderedObjectsCountPerThread.Length > maxUsedThreadsCount)
                        maxUsedThreadsCount = statistics.RenderedObjectsCountPerThread.Length;
                });

                if (maxUsedThreadsCount > 0)
                {
                    var objectsCountPerThread = new List<int>[maxUsedThreadsCount];
                    var renderTimePerThread   = new List<double>[maxUsedThreadsCount];

                    for (int i = 0; i < maxUsedThreadsCount; i++)
                    {
                        objectsCountPerThread[i] = new List<int>();
                        renderTimePerThread[i]   = new List<double>();
                    }

                    ForEachCollectedRenderingStatistics(statistics =>
                    {
                        if (statistics.RenderedObjectsCountPerThread != null)
                        {
                            for (var i = 0; i < statistics.RenderedObjectsCountPerThread.Length; i++)
                                objectsCountPerThread[i].Add(statistics.RenderedObjectsCountPerThread[i]);
                        }

                        if (statistics.RenderTimePerThread != null)
                        {
                            for (var i = 0; i < statistics.RenderTimePerThread.Length; i++)
                                renderTimePerThread[i].Add(statistics.RenderTimePerThread[i]);
                        }
                    });


                    sb.Append("Multi-threading:\r\n" +
                              "Thread:   Rendered objects:            Render time (ms):\r\n" +
                              "          min       avrg     max       min       avrg    max\r\n");

                    for (int i = 0; i < maxUsedThreadsCount; i++)
                    {
                        var objectsCountStatistics = CalculateCommonStatistics(objectsCountPerThread[i].ToArray());
                        var renderTimeStatistics   = CalculateCommonStatistics(renderTimePerThread[i].ToArray());

                        if (i > 0 && objectsCountStatistics["max"] < 1)
                            continue;

                        sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                            "{0}     {1,-6}    {2,-6:0}   {3,-6}  {4,6:0.00}    {5,6:0.00}  {6,6:0.00}\r\n",
                            i == 0 ? "Main " : "BG_" + i.ToString().PadRight(2),
                            objectsCountStatistics["min"], objectsCountStatistics["average"], objectsCountStatistics["max"],
                            renderTimeStatistics["min"], renderTimeStatistics["average"], renderTimeStatistics["max"]);
                    }
                }
            }
        }

        private void ForEachCollectedRenderingStatistics(Action<RenderingStatistics> action)
        {
            for (int i = 0; i < _allCollectedRenderingStatistics.Count; i++)
            {
                var renderingStatisticList = _allCollectedRenderingStatistics[i];

                int j = 0;
                while (j < renderingStatisticList.Count && renderingStatisticList[j].FrameNumber != 0) // if FrameNumber == 0 this means that this RenderingStatistics was not yet initialized
                {
                    action(renderingStatisticList[j]);
                    j++;
                }
            }
        }

        private double[] GetTimeValuesArray(Func<RenderingStatistics, double> accessorFunc)
        {
            var doubles = new double[_totalSamplesCount];

            int index = 0;
            for (int i = 0; i < _allCollectedRenderingStatistics.Count; i++)
            {
                var renderingStatisticList = _allCollectedRenderingStatistics[i];

                int j = 0;
                while (j < renderingStatisticList.Count && renderingStatisticList[j].FrameNumber != 0) // if FrameNumber == 0 this means that this RenderingStatistics was not yet initialized
                {
                    doubles[index] = accessorFunc(renderingStatisticList[j]);

                    index++;
                    j++;
                }
            }

            return doubles;
        }

        private void DXSceneOnAfterFrameRendered(object sender, EventArgs eventArgs)
        {
            if (_totalSamplesCount == 0 && _dxScene.FrameNumber == 1) // Only store _timeToFirstFrame when we actually get the data for the first frame
                _timeToFirstFrame = _stopwatch.Elapsed.TotalMilliseconds;

            // Copy values into preallocated object
            _dxScene.Statistics.Copy(_currentRenderingStatisticsList[_currentIndex]);

            _lastRenderingStatistics = _currentRenderingStatisticsList[_currentIndex];


            _currentIndex++;
            _totalSamplesCount ++;

            if ((_currentIndex + 1) >= _currentRenderingStatisticsList.Capacity)
            {
                _currentRenderingStatisticsList = CreatePreallocatedList(_initialCapacity);
                _allCollectedRenderingStatistics.Add(_currentRenderingStatisticsList);

                _currentIndex = 0;
            }
        }

        private List<RenderingStatistics> CreatePreallocatedList(int capacity)
        {
            var renderingStatistics = new List<RenderingStatistics>(capacity);

            for (int i = 0; i < capacity; i++)
                renderingStatistics.Add(new RenderingStatistics()); // Allocate the objects

            return renderingStatistics;
        }


        public static Dictionary<string, double> CalculateCommonStatistics(double[] doubles)
        {
            if (doubles == null || doubles.Length == 0)
                return null;

            
            // Sort the array to get min, max, q1, q2 and a3
            Array.Sort((Array) doubles);


            var average = doubles.Average();
            var stdev = Math.Sqrt(doubles.Sum(t => (t - average) * (t - average)) / (doubles.Length - 1));

            double q1, q2, q3;

            GetQuartals(doubles, out q1, out q2, out q3);

            double c90 = InterpolateValueAt(doubles, (double)(doubles.Length - 1) * 0.90);
            double c95 = InterpolateValueAt(doubles, (double)(doubles.Length - 1) * 0.95);

            var keyValuePairs = new Dictionary<string, double>();
            keyValuePairs.Add("count", (double)doubles.Length);
            keyValuePairs.Add("average", average);
            keyValuePairs.Add("stdev", stdev);
            keyValuePairs.Add("min", doubles[0]);
            keyValuePairs.Add("q1", q1);
            keyValuePairs.Add("q2", q2);
            keyValuePairs.Add("q3", q3);
            keyValuePairs.Add("c90", c90);
            keyValuePairs.Add("c95", c95);
            keyValuePairs.Add("max", doubles[doubles.Length - 1]);

            return keyValuePairs;
        }

        public static Dictionary<string, double> CalculateCommonStatistics(int[] values)
        {
            if (values == null || values.Length == 0)
                return null;

            // Convert int values to doubles
            var doubles = new double[values.Length];
            for (int i = 0; i < values.Length; i++)
                doubles[i] = (double) values[i];

            return CalculateCommonStatistics(doubles);
        }

        private static double GetMedian(IList<double> orderedValues)
        {
            if (orderedValues == null || orderedValues.Count == 0)
                return 0.0;

            if (orderedValues.Count == 1)
                return orderedValues[0];

            double median = InterpolateValueAt(orderedValues, (double)(orderedValues.Count - 1) * 0.5);
            return median;
        }

        private static void GetQuartals(IList<double> orderedValues, out double q1, out double q2, out double q3)
        {
            if (orderedValues == null || orderedValues.Count == 0)
            {
                q1 = q2 = q3 = 0.0;
                return;
            }

            int count = orderedValues.Count;

            if (count == 1)
            {
                q1 = q2 = q3 = orderedValues[0];
                return;
            }

            q1 = InterpolateValueAt(orderedValues, (double)(count - 1) * 0.25);
            q2 = InterpolateValueAt(orderedValues, (double)(count - 1) * 0.5);
            q3 = InterpolateValueAt(orderedValues, (double)(count - 1) * 0.75);
        }

        private static double InterpolateValueAt(IList<double> values, double location)
        {
            if (location < 0 || location > (values.Count - 1))
                return 0;

            double interpolatedValue;

            double floor = Math.Floor(location);

            if (floor == location)
            {
                // No decimal value - we have exact location
                interpolatedValue = values[(int)location];
            }
            else
            {
                double ceiling = Math.Ceiling(location);

                interpolatedValue = (location - floor) * values[(int)floor] +
                                    (ceiling - location) * values[(int)ceiling];
            }

            return interpolatedValue;
        }
    }
}