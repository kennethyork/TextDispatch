//! Hardware telemetry.
//!
//! This is the sensory half of the quiet-mode governor. NVML is loaded
//! in-process (the driver ships `nvml.dll` into System32), so we read GPU
//! temperature, fan duty, power draw and VRAM without shelling out to
//! `nvidia-smi` once per second.

use serde::Serialize;
use std::time::{SystemTime, UNIX_EPOCH};
use sysinfo::{Components, System};

pub fn now_ms() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_millis() as u64)
        .unwrap_or(0)
}

/// A single GPU reading. Every field is optional because NVML reports
/// `NotSupported` for plenty of values depending on the board and driver.
#[derive(Debug, Clone, Default, Serialize)]
pub struct GpuTelemetry {
    pub available: bool,
    pub name: Option<String>,
    pub temperature_c: Option<u32>,
    /// Fan duty cycle in percent. The number that matters for "is it loud".
    pub fan_percent: Option<u32>,
    pub fan_rpm: Option<u32>,
    pub power_watts: Option<f64>,
    pub power_limit_watts: Option<f64>,
    pub utilization_gpu: Option<u32>,
    pub utilization_mem: Option<u32>,
    pub vram_used_mb: Option<u64>,
    pub vram_total_mb: Option<u64>,
    pub core_clock_mhz: Option<u32>,
    pub mem_clock_mhz: Option<u32>,
    pub throttle_reasons: Option<u64>,
    pub error: Option<String>,
}

#[derive(Debug, Clone, Default, Serialize)]
pub struct TelemetrySample {
    pub ts_ms: u64,
    pub gpu: GpuTelemetry,
    pub cpu_usage: f32,
    pub cpu_temp_c: Option<f32>,
    pub mem_used_mb: u64,
    pub mem_total_mb: u64,
}

impl GpuTelemetry {
    fn unavailable(err: impl Into<String>) -> Self {
        GpuTelemetry {
            error: Some(err.into()),
            ..Default::default()
        }
    }
}

/// Read the first GPU via NVML.
///
/// The `Nvml` handle is created and dropped per call. That keeps us free of
/// any `Send`/`Sync` gymnastics around a long-lived driver handle, and nvml's
/// init/shutdown pair is reference-counted and cheap enough at 1 Hz.
pub fn sample_gpu() -> GpuTelemetry {
    use nvml_wrapper::enum_wrappers::device::{Clock, TemperatureSensor};
    use nvml_wrapper::Nvml;

    let nvml = match Nvml::init() {
        Ok(nvml) => nvml,
        Err(err) => return GpuTelemetry::unavailable(format!("NVML init failed: {err}")),
    };

    let device = match nvml.device_by_index(0) {
        Ok(device) => device,
        Err(err) => return GpuTelemetry::unavailable(format!("no NVIDIA GPU found: {err}")),
    };

    let mut out = GpuTelemetry {
        available: true,
        ..Default::default()
    };

    out.name = device.name().ok();
    out.temperature_c = device.temperature(TemperatureSensor::Gpu).ok();
    out.fan_percent = device.fan_speed(0).ok();
    out.fan_rpm = device.fan_speed_rpm(0).ok();
    out.power_watts = device.power_usage().ok().map(|mw| mw as f64 / 1000.0);
    out.power_limit_watts = device.enforced_power_limit().ok().map(|mw| mw as f64 / 1000.0);

    if let Ok(util) = device.utilization_rates() {
        out.utilization_gpu = Some(util.gpu);
        out.utilization_mem = Some(util.memory);
    }

    if let Ok(mem) = device.memory_info() {
        out.vram_used_mb = Some(mem.used / 1024 / 1024);
        out.vram_total_mb = Some(mem.total / 1024 / 1024);
    }

    out.core_clock_mhz = device.clock_info(Clock::Graphics).ok();
    out.mem_clock_mhz = device.clock_info(Clock::Memory).ok();
    out.throttle_reasons = device.current_throttle_reasons().ok();

    out
}

/// CPU + RAM sampler that keeps one `System` alive so that CPU usage is a
/// real delta between refreshes rather than a meaningless first reading.
pub struct SystemSampler {
    sys: System,
}

impl SystemSampler {
    pub fn new() -> Self {
        let mut sys = System::new();
        sys.refresh_cpu_usage();
        sys.refresh_memory();
        Self { sys }
    }

    pub fn sample(&mut self) -> (f32, Option<f32>, u64, u64) {
        self.sys.refresh_cpu_usage();
        self.sys.refresh_memory();
        (
            self.sys.global_cpu_usage(),
            read_cpu_temp(),
            self.sys.used_memory(),
            self.sys.total_memory(),
        )
    }
}

impl Default for SystemSampler {
    fn default() -> Self {
        Self::new()
    }
}

/// Best-effort CPU package temperature. Sensor labels vary wildly across
/// vendors, so we pattern-match on the common ones and take the hottest.
fn read_cpu_temp() -> Option<f32> {
    let components = Components::new_with_refreshed_list();
    let mut hottest: Option<f32> = None;

    for component in components.iter() {
        let label = component.label().to_ascii_lowercase();
        let looks_like_cpu = label.contains("cpu")
            || label.contains("package")
            || label.contains("tctl")
            || label.contains("tdie")
            || label.contains("core");

        if !looks_like_cpu {
            continue;
        }

        let temp = component.temperature();
        if temp > 0.0 && temp < 150.0 {
            hottest = Some(match hottest {
                Some(current) if current >= temp => current,
                _ => temp,
            });
        }
    }

    hottest
}
