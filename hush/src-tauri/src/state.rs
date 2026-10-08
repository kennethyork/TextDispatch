use crate::ollama;
use crate::telemetry::{now_ms, sample_gpu, SystemSampler, TelemetrySample};
use std::collections::HashMap;
use std::sync::atomic::AtomicBool;
use std::sync::{Arc, Mutex, MutexGuard};

/// Recover from a poisoned lock instead of propagating a panic into every
/// later command. A poisoned telemetry mutex should never brick the app.
pub fn lock<T>(mutex: &Mutex<T>) -> MutexGuard<'_, T> {
    mutex.lock().unwrap_or_else(|poisoned| poisoned.into_inner())
}

pub struct AppState {
    pub http: reqwest::Client,
    pub base_url: Mutex<String>,
    /// stream id -> cancellation flag, checked between NDJSON lines.
    pub streams: Mutex<HashMap<String, Arc<AtomicBool>>>,
    pub sampler: Mutex<SystemSampler>,
    pub last_sample: Mutex<Option<TelemetrySample>>,
}

impl AppState {
    pub fn new() -> Self {
        let http = ollama::build_client().expect("failed to build HTTP client");
        Self {
            http,
            base_url: Mutex::new(ollama::DEFAULT_BASE_URL.to_string()),
            streams: Mutex::new(HashMap::new()),
            sampler: Mutex::new(SystemSampler::new()),
            last_sample: Mutex::new(None),
        }
    }

    pub fn base_url(&self) -> String {
        lock(&self.base_url).clone()
    }

    /// Take one full system + GPU reading and cache it.
    pub fn sample_now(&self) -> TelemetrySample {
        let (cpu_usage, cpu_temp_c, mem_used, mem_total) = {
            let mut sampler = lock(&self.sampler);
            sampler.sample()
        };

        let sample = TelemetrySample {
            ts_ms: now_ms(),
            gpu: sample_gpu(),
            cpu_usage,
            cpu_temp_c,
            mem_used_mb: mem_used / 1024 / 1024,
            mem_total_mb: mem_total / 1024 / 1024,
        };

        *lock(&self.last_sample) = Some(sample.clone());
        sample
    }
}

impl Default for AppState {
    fn default() -> Self {
        Self::new()
    }
}
