using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace SimurghDashboard.Sensors.Models;

/// <summary>
/// Categorizes telemetry sensors, hardware peripherals, environmental probes,
/// and medical-grade monitoring instrumentation across the system.
/// </summary>
[DataContract]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SensorType : ushort
{
    /// <summary>
    /// Unspecified or uninitialized sensor category.
    /// </summary>
    [EnumMember(Value = "Unknown")]
    [Description("Unknown")]
    Unknown = 0,

    // ==========================================
    // Environmental & Climate Telemetry
    // ==========================================

    /// <summary>
    /// Ambient or surface temperature sensor (Celsius/Fahrenheit/Kelvin).
    /// </summary>
    [EnumMember(Value = "Temperature")]
    [Description("Temperature Sensor")]
    Temperature = 100,

    /// <summary>
    /// Relative humidity (RH%) sensor.
    /// </summary>
    [EnumMember(Value = "Humidity")]
    [Description("Humidity Sensor")]
    Humidity = 101,

    /// <summary>
    /// Atmospheric or enclosed room differential pressure transducer.
    /// </summary>
    [EnumMember(Value = "Pressure")]
    [Description("Pressure Transducer")]
    Pressure = 102,

    /// <summary>
    /// Ambient light, lux level, or ultraviolet radiation monitor.
    /// </summary>
    [EnumMember(Value = "Illuminance")]
    [Description("Illuminance / Light Sensor")]
    Illuminance = 103,

    /// <summary>
    /// Total Volatile Organic Compounds (TVOC) and indoor air quality (IAQ) index probe.
    /// </summary>
    [EnumMember(Value = "AirQuality")]
    [Description("Air Quality / IAQ Sensor")]
    AirQuality = 104,

    /// <summary>
    /// Carbon Dioxide (CO2) NDIR concentration monitor.
    /// </summary>
    [EnumMember(Value = "CarbonDioxide")]
    [Description("CO2 Sensor")]
    CarbonDioxide = 105,

    // ==========================================
    // Medical & Life-Support Instrumentation
    // ==========================================

    /// <summary>
    /// Pulse oximeter photoplethysmogram measuring oxygen saturation (SpO2).
    /// </summary>
    [EnumMember(Value = "PulseOximetry")]
    [Description("Pulse Oximetry (SpO2)")]
    PulseOximetry = 200,

    /// <summary>
    /// Electrocardiogram (ECG) telemetry lead sensor for cardiac electrical activity.
    /// </summary>
    [EnumMember(Value = "Ecg")]
    [Description("Electrocardiogram (ECG)")]
    Ecg = 201,

    /// <summary>
    /// Invasive or non-invasive arterial blood pressure transducer.
    /// </summary>
    [EnumMember(Value = "BloodPressure")]
    [Description("Blood Pressure Sensor")]
    BloodPressure = 202,

    /// <summary>
    /// Capnography infrared absorption sensor measuring end-tidal CO2 (EtCO2).
    /// </summary>
    [EnumMember(Value = "Capnography")]
    [Description("Capnography (EtCO2)")]
    Capnography = 203,

    /// <summary>
    /// Medical gas supply line pressure monitor (Oxygen, Nitrous Oxide, Vacuum, Medical Air).
    /// </summary>
    [EnumMember(Value = "MedicalGasPressure")]
    [Description("Medical Gas Pressure")]
    MedicalGasPressure = 204,

    /// <summary>
    /// Anesthetic agent analyzer for volatile vapor detection and concentration monitoring.
    /// </summary>
    [EnumMember(Value = "AnestheticAgent")]
    [Description("Anesthetic Agent Analyzer")]
    AnestheticAgent = 205,

    // ==========================================
    // Kinematic, Spatial & Physical Transducers
    // ==========================================

    /// <summary>
    /// Passive Infrared (PIR) or microwave Doppler occupancy and presence detector.
    /// </summary>
    [EnumMember(Value = "Presence")]
    [Description("Presence / Motion Detector")]
    Presence = 300,

    /// <summary>
    /// 3-Axis accelerometer and vibration monitor for mechanical resonance and stability.
    /// </summary>
    [EnumMember(Value = "Vibration")]
    [Description("Vibration / Accelerometer")]
    Vibration = 301,

    /// <summary>
    /// Optical or magnetic rotary/linear position encoder.
    /// </summary>
    [EnumMember(Value = "PositionEncoder")]
    [Description("Position / Displacement Encoder")]
    PositionEncoder = 302,

    /// <summary>
    /// Strain gauge load cell measuring weight, tension, or mechanical force.
    /// </summary>
    [EnumMember(Value = "LoadCell")]
    [Description("Load Cell / Force Transducer")]
    LoadCell = 303,

    /// <summary>
    /// Magnetic hall-effect or optical proximity limit switch for gantry/door closure.
    /// </summary>
    [EnumMember(Value = "Proximity")]
    [Description("Proximity / Limit Switch")]
    Proximity = 304,

    // ==========================================
    // Electrical, Power & Thermal Diagnostics
    // ==========================================

    /// <summary>
    /// Voltage monitoring probe (AC mains or DC rail telemetry).
    /// </summary>
    [EnumMember(Value = "Voltage")]
    [Description("Voltage Telemetry")]
    Voltage = 400,

    /// <summary>
    /// Current shunt or Hall-effect current transducer (Amperage telemetry).
    /// </summary>
    [EnumMember(Value = "Current")]
    [Description("Current Telemetry")]
    Current = 401,

    /// <summary>
    /// Active, reactive, or apparent power / energy consumption meter (Watts/Joules).
    /// </summary>
    [EnumMember(Value = "Power")]
    [Description("Power / Energy Meter")]
    Power = 402,

    /// <summary>
    /// Radiation dosimetry ionization chamber or solid-state detector (e.g., LINAC telemetry).
    /// </summary>
    [EnumMember(Value = "RadiationDosimetry")]
    [Description("Radiation Dosimeter")]
    RadiationDosimetry = 500
}