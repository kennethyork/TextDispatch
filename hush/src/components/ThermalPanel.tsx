import type { ChatStats, TelemetrySample } from "../types";
import { Sparkline } from "./Sparkline";

interface ThermalPanelProps {
  latest: TelemetrySample | null;
  history: TelemetrySample[];
  tokHistory: number[];
  lastStats: ChatStats | null;
}

function numbers(
  history: TelemetrySample[],
  pick: (sample: TelemetrySample) => number | null | undefined,
): number[] {
  return history
    .map(pick)
    .filter((value): value is number => typeof value === "number");
}

function heatLevel(value: number | null | undefined, warm: number, hot: number) {
  if (typeof value !== "number") return "neutral";
  if (value >= hot) return "hot";
  if (value >= warm) return "warm";
  return "cool";
}

function Metric({
  label,
  value,
  unit,
  level = "neutral",
}: {
  label: string;
  value: string;
  unit?: string;
  level?: string;
}) {
  return (
    <div className={`metric metric--${level}`}>
      <span className="metric__label">{label}</span>
      <span className="metric__value">
        {value}
        {unit && <span className="metric__unit">{unit}</span>}
      </span>
    </div>
  );
}

function Chart({
  title,
  values,
  max,
  color,
  current,
  unit,
}: {
  title: string;
  values: number[];
  max?: number;
  color: string;
  current: string;
  unit?: string;
}) {
  return (
    <div className="chart">
      <div className="chart__header">
        <span className="chart__title">{title}</span>
        <span className="chart__current">
          {current}
          {unit && <span className="metric__unit">{unit}</span>}
        </span>
      </div>
      <Sparkline values={values} max={max} color={color} height={40} />
    </div>
  );
}

export function ThermalPanel({
  latest,
  history,
  tokHistory,
  lastStats,
}: ThermalPanelProps) {
  const gpu = latest?.gpu;

  const temp = gpu?.temperature_c ?? null;
  const fan = gpu?.fan_percent ?? null;
  const power = gpu?.power_watts ?? null;
  const powerLimit = gpu?.power_limit_watts ?? null;

  const vramUsed = gpu?.vram_used_mb ?? 0;
  const vramTotal = gpu?.vram_total_mb ?? 0;
  const vramPct = vramTotal > 0 ? (vramUsed / vramTotal) * 100 : 0;

  const memPct =
    latest && latest.mem_total_mb > 0
      ? (latest.mem_used_mb / latest.mem_total_mb) * 100
      : 0;

  const tokensPerSecond = lastStats?.tokens_per_second ?? null;
  const idle = !latest || vramUsed < 64;

  return (
    <aside className="thermal">
      <header className="thermal__header">
        <h2>Thermals</h2>
        <span className={`pill pill--${idle ? "idle" : "busy"}`}>
          {idle ? "GPU idle" : "Generating"}
        </span>
      </header>

      {gpu?.error && <p className="thermal__error">{gpu.error}</p>}
      {gpu?.name && <p className="thermal__device">{gpu.name}</p>}

      <div className="metric-grid">
        <Metric
          label="Temp"
          value={temp === null ? "—" : String(temp)}
          unit="°C"
          level={heatLevel(temp, 60, 75)}
        />
        <Metric
          label="Fan"
          value={fan === null ? "off" : String(fan)}
          unit={fan === null ? undefined : "%"}
          level={heatLevel(fan, 40, 70)}
        />
        <Metric
          label="Power"
          value={power === null ? "—" : power.toFixed(0)}
          unit="W"
          level={heatLevel(
            powerLimit && power !== null ? (power / powerLimit) * 100 : null,
            70,
            90,
          )}
        />
        <Metric
          label="Tokens"
          value={tokensPerSecond === null ? "—" : tokensPerSecond.toFixed(1)}
          unit="/s"
        />
      </div>

      <Chart
        title="Temperature"
        values={numbers(history, (sample) => sample.gpu.temperature_c)}
        color="#f97316"
        current={temp === null ? "—" : String(temp)}
        unit="°C"
      />
      <Chart
        title="Power draw"
        values={numbers(history, (sample) => sample.gpu.power_watts)}
        max={powerLimit ?? undefined}
        color="#eab308"
        current={power === null ? "—" : power.toFixed(0)}
        unit="W"
      />
      <Chart
        title="Fan duty"
        values={numbers(history, (sample) => sample.gpu.fan_percent)}
        max={100}
        color="#38bdf8"
        current={fan === null ? "off" : String(fan)}
        unit="%"
      />
      <Chart
        title="Tokens / second"
        values={tokHistory}
        color="#5eead4"
        current={tokensPerSecond === null ? "—" : tokensPerSecond.toFixed(1)}
      />

      <div className="bars">
        <div className="bar">
          <div className="bar__header">
            <span>VRAM</span>
            <span>
              {vramTotal > 0
                ? `${(vramUsed / 1024).toFixed(1)} / ${(vramTotal / 1024).toFixed(1)} GB`
                : "—"}
            </span>
          </div>
          <div className="bar__track">
            <div
              className="bar__fill bar__fill--vram"
              style={{ width: `${vramPct}%` }}
            />
          </div>
        </div>

        <div className="bar">
          <div className="bar__header">
            <span>System RAM</span>
            <span>
              {latest
                ? `${(latest.mem_used_mb / 1024).toFixed(1)} / ${(latest.mem_total_mb / 1024).toFixed(1)} GB`
                : "—"}
            </span>
          </div>
          <div className="bar__track">
            <div
              className="bar__fill bar__fill--ram"
              style={{ width: `${memPct}%` }}
            />
          </div>
        </div>
      </div>

      <div className="metric-grid metric-grid--footer">
        <Metric
          label="CPU"
          value={latest ? latest.cpu_usage.toFixed(0) : "—"}
          unit="%"
        />
        <Metric
          label="CPU temp"
          value={
            latest?.cpu_temp_c == null ? "—" : latest.cpu_temp_c.toFixed(0)
          }
          unit="°C"
          level={heatLevel(latest?.cpu_temp_c ?? null, 70, 85)}
        />
        <Metric
          label="GPU util"
          value={gpu?.utilization_gpu == null ? "—" : String(gpu.utilization_gpu)}
          unit="%"
        />
        <Metric
          label="VRAM used"
          value={gpu?.vram_used_mb == null ? "—" : String(gpu.vram_used_mb)}
          unit="MB"
        />
      </div>

      {lastStats?.eval_count != null && (
        <p className="thermal__note">
          Last turn: {lastStats.eval_count} tokens
          {lastStats.prompt_eval_count
            ? ` · ${lastStats.prompt_eval_count} prompt`
            : ""}
          {lastStats.total_duration_ms
            ? ` · ${(lastStats.total_duration_ms / 1000).toFixed(1)}s total`
            : ""}
        </p>
      )}
    </aside>
  );
}
