﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace OneSTools.EventLog
{
    public class EventLogReader : IDisposable
    {
        private readonly string _logFolder;
        private readonly bool _liveMode;
        private readonly LgfReader _lgfReader;
        private LgpReader _lgpReader;
        private FileSystemWatcher _lgpWatcher;

        public EventLogReader(string logFolder, bool liveMode = false)
        {
            _logFolder = logFolder;
            _liveMode = liveMode;
            _lgfReader = new LgfReader(Path.Combine(_logFolder, "1Cv8.lgf"));
        }

        public EventLogItem ReadNextEventLogItem(CancellationToken cancellationToken)
        {
            if (_liveMode)
                StartLgpFilesWatcher();

            if (_lgpReader == null)
                SetNextLgpReader();

            EventLogItem item = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                item = _lgpReader.ReadNextEventLogItem(cancellationToken);

                if (item == null)
                {
                    var newReader = SetNextLgpReader();

                    if (_liveMode)
                    {
                        if (!newReader)
                            Thread.Sleep(5000);
                    }
                    else
                    {
                        if (!newReader)
                            break;
                    }
                }
                else
                    break;
            }

            return item;
        }

        private void StartLgpFilesWatcher()
        {
            _lgpWatcher = new FileSystemWatcher(_logFolder)
            {
                Filter = "*.lgp",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite
            };
            _lgpWatcher.Created += LgpWatcher_Created;

            _lgpWatcher.EnableRaisingEvents = true;
        }

        private bool SetNextLgpReader()
        {
            var currentReaderLastWriteDateTime = DateTime.MinValue;

            if (_lgpReader != null)
                currentReaderLastWriteDateTime = new FileInfo(_lgpReader.LgpPath).LastWriteTime;

            var files = Directory.GetFiles(_logFolder, "*.lgp");

            foreach (var file in files)
            {
                var writeDateTime = new FileInfo(file).LastWriteTime;

                if (writeDateTime > currentReaderLastWriteDateTime)
                {
                    if (_lgpReader != null)
                        _lgpReader.Dispose();

                    _lgpReader = new LgpReader(file, _lgfReader);

                    return true;
                }
            }

            return false;
        }

        private void LgpWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
            {

            }
        }

        public void Dispose()
        {
            if (_lgpWatcher != null)
                _lgpWatcher.Dispose();

            if (_lgfReader != null)
                _lgfReader.Dispose();
        }
    }
}