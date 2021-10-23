﻿using System;

namespace KdSoft.EtwEvents.Client
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class EventSinkAttribute: Attribute
    {
        public EventSinkAttribute(string sinkType) {
            this.SinkType = sinkType;
        }

        public string SinkType { get; }
    }
}
