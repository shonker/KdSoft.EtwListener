﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using KdSoft.EtwEvents.Client.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;

namespace KdSoft.EtwEvents.AgentManager.EventSinks
{
    class EventSinkService
    {
        readonly IHostEnvironment _env;
        readonly IStringLocalizer<EventSinkService> _;
        readonly string[] _runtimeAssemblyPaths;
        const string SinkAssemblyFilter = "*Sink.dll";

        public EventSinkService(IHostEnvironment env, IStringLocalizer<EventSinkService> localize) {
            this._env = env;
            this._ = localize;
            this._runtimeAssemblyPaths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        }

        /// <summary>
        /// Returns event sink types in configured container directory.
        /// The subdirectory name defines the event sink type.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<EventSinkInfo> GetEventSinkInfos() {
            var eventSinksDir = Path.Combine(_env.ContentRootPath, "EventSinks");
            var eventSinksConfigDir = Path.Combine(_env.ContentRootPath, "src", "eventSinks");

            var eventSinksDirInfo = new DirectoryInfo(eventSinksDir);
            var eventSinksConfigDirInfo = new DirectoryInfo(eventSinksConfigDir);

            // trailing '/' is important for building relative Uris
            var eventSinksDirUri = new Uri($"file:///{eventSinksDirInfo.FullName}/");
            var evtSinkDirectories = eventSinksDirInfo.EnumerateDirectories();

            var assemblyPaths = new List<string>(_runtimeAssemblyPaths);
            assemblyPaths.Add(typeof(IEventSinkFactory).Assembly.Location);
            foreach (var evtSinkDir in evtSinkDirectories) {
                var evtSinkFile = evtSinkDir.GetFiles(SinkAssemblyFilter).FirstOrDefault();
                if (evtSinkFile != null) {
                    assemblyPaths.Add(evtSinkFile.FullName);
                }
            }

            // Create PathAssemblyResolver that can resolve assemblies using the created list.
            var resolver = new PathAssemblyResolver(assemblyPaths);
            using (var metaLoadContext = new MetadataLoadContext(resolver)) {
                foreach (var evtSinkDir in evtSinkDirectories) {
                    var evtSinkFile = evtSinkDir.GetFiles(SinkAssemblyFilter).FirstOrDefault();
                    if (evtSinkFile != null) {
                        var evtSinkType = metaLoadContext.GetEventSinkTypes(evtSinkFile.FullName).FirstOrDefault();
                        if (evtSinkType != null) {
                            var sinkRelativeDir = Path.GetRelativePath(eventSinksDir, evtSinkDir.FullName);
                            var sinkConfigDir = Path.Combine(sinkRelativeDir, "config");
                            var configView = eventSinksConfigDirInfo.GetFiles(@$"{sinkConfigDir}/*-config.js").First();
                            var configViewUri = new Uri($"file:///{configView.FullName}");
                            var configModel = eventSinksConfigDirInfo.GetFiles(@$"{sinkConfigDir}/*-config-model.js").First();
                            var configModelUri = new Uri($"file:///{configModel.FullName}");
                            yield return new EventSinkInfo {
                                SinkType = evtSinkType,
                                Description = _.GetString(evtSinkType),
                                // relative Uri does not include "EventSinks" path component (has a trailing '/')
                                ConfigViewUrl = eventSinksDirUri.MakeRelativeUri(configViewUri),
                                ConfigModelUrl = eventSinksDirUri.MakeRelativeUri(configModelUri),
                            };
                        }
                    }
                }
            }
        }
    }
}
