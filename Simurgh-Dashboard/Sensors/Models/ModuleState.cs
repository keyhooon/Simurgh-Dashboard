using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SimurghDashboard.Sensors.Models
{

        /// <summary>
        /// Operational status and connectivity state flags for modules, peripheral telemetry, and services.
        /// Designed for low-allocation network serialization, state-machine transitions, and direct WPF XAML triggers.
        /// </summary>
        [DataContract]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum ModuleState : byte
        {
            /// <summary>
            /// Module or remote service is disconnected, unreachable, or powered down.
            /// </summary>
            [EnumMember(Value = "Offline")]
            [Description("Offline")]
            Offline = 0,

            /// <summary>
            /// Module is connected, synchronized, and operating within nominal parameters.
            /// </summary>
            [EnumMember(Value = "Online")]
            [Description("Online")]
            Online = 1,

            /// <summary>
            /// Module has crossed safe operational thresholds or requires user intervention.
            /// </summary>
            [EnumMember(Value = "Warning")]
            [Description("Warning")]
            Warning = 2,

            /// <summary>
            /// Module has experienced a functional breakdown, fault condition, or unhandled failure.
            /// </summary>
            [EnumMember(Value = "Error")]
            [Description("Error")]
            Error = 3
        }
    }