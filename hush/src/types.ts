export interface ModelDetails {
  format?: string | null;
  family?: string | null;
  parameter_size?: string | null;
  quantization_level?: string | null;
}

export interface ModelInfo {
  name: string;
  model?: string | null;
  size: number;
  digest?: string | null;
  modified_at?: string | null;
  details?: ModelDetails | null;
}

export interface RunningModel {
  name: string;
  size: number;
  size_vram: number;
  expires_at?: string | null;
}

export interface Health {
  reachable: boolean;
  base_url: string;
  version?: string | null;
  model_count: number;
  error?: string | null;
}

export interface GpuTelemetry {
  available: boolean;
  name?: string | null;
  temperature_c?: number | null;
  fan_percent?: number | null;
  fan_rpm?: number | null;
  power_watts?: number | null;
  power_limit_watts?: number | null;
  utilization_gpu?: number | null;
  utilization_mem?: number | null;
  vram_used_mb?: number | null;
  vram_total_mb?: number | null;
  core_clock_mhz?: number | null;
  mem_clock_mhz?: number | null;
  throttle_reasons?: number | null;
  error?: string | null;
}

export interface TelemetrySample {
  ts_ms: number;
  gpu: GpuTelemetry;
  cpu_usage: number;
  cpu_temp_c?: number | null;
  mem_used_mb: number;
  mem_total_mb: number;
}

export interface ChatStats {
  total_duration_ms?: number | null;
  load_duration_ms?: number | null;
  prompt_eval_count?: number | null;
  prompt_eval_duration_ms?: number | null;
  eval_count?: number | null;
  eval_duration_ms?: number | null;
  tokens_per_second?: number | null;
}

export interface ChatMessage {
  role: "system" | "user" | "assistant";
  content: string;
}

export interface ChatChunkEvent {
  stream_id: string;
  content: string;
}

export interface ChatDoneEvent {
  stream_id: string;
  cancelled: boolean;
  stats: ChatStats;
}

export interface ChatErrorEvent {
  stream_id: string;
  message: string;
}
