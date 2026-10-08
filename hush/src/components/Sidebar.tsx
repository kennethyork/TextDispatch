import { formatBytes } from "../api";
import type { Health, ModelInfo, RunningModel } from "../types";

interface SidebarProps {
  health: Health | null;
  models: ModelInfo[];
  running: RunningModel[];
  activeModel: string;
  busy: boolean;
  onSelect: (model: string) => void;
  onRefresh: () => void;
  onUnload: (model: string) => void;
}

export function Sidebar({
  health,
  models,
  running,
  activeModel,
  busy,
  onSelect,
  onRefresh,
  onUnload,
}: SidebarProps) {
  const loadedNames = new Set(running.map((model) => model.name));

  return (
    <aside className="sidebar">
      <div className="sidebar__section">
        <header className="sidebar__header">
          <h2>Models</h2>
          <button
            type="button"
            className="ghost-button"
            onClick={onRefresh}
            disabled={busy}
          >
            Refresh
          </button>
        </header>

        {models.length === 0 ? (
          <p className="sidebar__empty">
            {health?.reachable
              ? "No models installed yet. Pull one with `ollama pull llama3.2`."
              : "Ollama not reachable."}
          </p>
        ) : (
          <ul className="model-list">
            {models.map((model) => {
              const loaded = loadedNames.has(model.name);
              return (
                <li key={model.name}>
                  <button
                    type="button"
                    className={`model-card${activeModel === model.name ? " model-card--active" : ""}`}
                    onClick={() => onSelect(model.name)}
                  >
                    <span className="model-card__top">
                      <span className="model-card__name">{model.name}</span>
                      {loaded && <span className="badge badge--hot">VRAM</span>}
                    </span>
                    <span className="model-card__meta">
                      {model.details?.parameter_size ?? "—"}
                      {model.details?.quantization_level
                        ? ` · ${model.details.quantization_level}`
                        : ""}
                      {model.size ? ` · ${formatBytes(model.size)}` : ""}
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </div>

      <div className="sidebar__section sidebar__section--grow">
        <header className="sidebar__header">
          <h2>In VRAM</h2>
          <span className="sidebar__count">{running.length}</span>
        </header>

        {running.length === 0 ? (
          <p className="sidebar__empty">
            GPU idle. Nothing is holding VRAM, so the fans can stay off.
          </p>
        ) : (
          <ul className="running-list">
            {running.map((model) => (
              <li key={model.name} className="running-card">
                <span className="running-card__name">{model.name}</span>
                <span className="running-card__meta">
                  {formatBytes(model.size_vram)} in VRAM
                </span>
                <button
                  type="button"
                  className="ghost-button ghost-button--danger"
                  onClick={() => onUnload(model.name)}
                >
                  Unload
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </aside>
  );
}
