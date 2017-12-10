using System;
using System.IO;
using System.Text;
using Sandbox.ModAPI;
using VRage;
using VRage.Utils;

namespace Equinox.Utils.Logging
{
    public class CustomLogger : LoggerBase
    {
        public const string DefaultLogFile = "ResearchCore.log";

        private readonly FastResourceLock _lock, _writeLock;
        private readonly StringBuilder _cache;
        private string _file;
        private TextWriter _writer;
        private DateTime _lastWriteTime;
        private int _readyTicks;

        private const int WRITE_INTERVAL_TICKS = 30;
        private static readonly TimeSpan _writeIntervalTime = new TimeSpan(
            0, 0, 1);

        public CustomLogger()
        {
            _file = DefaultLogFile;
            _writer = null;
            _lock = new FastResourceLock();
            _writeLock = new FastResourceLock();
            _cache = new StringBuilder();
            _readyTicks = 0;
            _lastWriteTime = DateTime.Now;
        }
        
        public void UpdateAfterSimulation()
        {
            var requiresUpdate = false;
            using (_lock.AcquireExclusiveUsing())
                requiresUpdate = _cache.Length > 0;
            if (requiresUpdate)
                _readyTicks++;
            else
                _readyTicks = 0;
            if (_readyTicks <= WRITE_INTERVAL_TICKS) return;
            Flush();
            _readyTicks = 0;
        }

        public void Flush()
        {
            if (MyAPIGateway.Utilities != null)
                MyAPIGateway.Parallel.StartBackground(() =>
                {
                    try
                    {
                        if (_writer == null)
                        {
                            using (_writeLock.AcquireExclusiveUsing())
                            {
                                if (_writer == null)
                                {
                                    _writer = MyAPIGateway.Session.IsServerDecider() ? 
                                        MyAPIGateway.Utilities.WriteFileInWorldStorage(_file, typeof(CustomLogger)) : 
                                        MyAPIGateway.Utilities.WriteFileInLocalStorage(_file, typeof(CustomLogger));
                                    MyLog.Default.WriteLine($"Opened log for {_file}");
                                    MyLog.Default.Flush();
                                }
                            }
                        }
                        if (_writer == null || _cache.Length <= 0) return;
                        string cache = null;
                        using (_lock.AcquireExclusiveUsing())
                        {
                            if (_writer != null && _cache.Length > 0)
                            {
                                cache = _cache.ToString();
                                _cache.Clear();
                                _lastWriteTime = DateTime.UtcNow;
                            }
                        }
                        if (cache == null || _writer == null) return;
                        using (_writeLock.AcquireExclusiveUsing())
                        {
                            _writer.Write(cache);
                            _writer.Flush();
                        }
                    }
                    catch (Exception e)
                    {
                        MyLog.Default.WriteLine($"{_file} LogDump: \r\n" + e.ToString());
                        MyLog.Default.Flush();
                    }
                });
        }

        public void Detach()
        {
            if (_lock == null) return;
            string remains = null;
            if (_cache != null)
                using (_lock.AcquireExclusiveUsing())
                {
                    if (_cache.Length > 0)
                    {
                        remains = _cache.ToString();
                        _cache.Clear();
                    }
                }
            if (_writer == null) return;
            using (_writeLock.AcquireExclusiveUsing())
            {
                if (remains != null)
                    _writer.Write(remains);
                _writer.Close();
                _writer = null;
            }
        }

        private void WriteLineHeader()
        {
            var now = DateTime.Now;
            _cache.AppendFormat("[{0,2:D2}:{1,2:D2}:{2,2:D2}] ", now.Hour, now.Minute, now.Second);
        }

        protected override void Write(StringBuilder message)
        {
            var shouldFlush = false;
            using (_lock.AcquireExclusiveUsing())
            {
                WriteLineHeader();
                _cache.Append(message);
                _cache.Append("\r\n");
                shouldFlush = DateTime.UtcNow - _lastWriteTime > _writeIntervalTime;
            }
            if (shouldFlush)
                Flush();
        }
    }
}