import { useCallback, useEffect, useRef, useState } from "react";
import { api, onChatChunk, onChatDone, onChatError, onTelemetry } from "./api";
import { ChatPanel } from "./components/ChatPanel";
import { Sidebar } from "./components/Sidebar";
import { ThermalPanel } from "./components/ThermalPanel";
import type {
  ChatMessage,
  ChatStats,
  Health,
  ModelInfo,
  RunningModel,
  TelemetrySample,
} from "./types";
import "./App.css";

/** Rolling window for the thermal charts, in samples (one per second). */
const HISTORY_LIMIT = 120;

function uid(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function push(series: number[], value: number): number[] {
  const next = [...series, value];
  return next.length > HISTORY_LIMIT
    ? next.slice(next.length - HISTORY_LIMIT)
    : next;
}

export default function App() {
  const [health, setHealth] = useState<Health | null>(null);
  const [models, setModels] = useState<ModelInfo[]>([]);
  const [running, setRunning] = useState<RunningModel[]>([]);
  const [activeModel, setActiveModel] = useState("");

  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [streaming, setStreaming] = useState(false);
  const [streamingText, setStreamingText] = useState("");
  const [error, setError] = useState<string | null>(null);

  const [latest, setLatest] = useState<TelemetrySample | null>(null);
  const [history, setHistory] = useState<TelemetrySample[]>([]);
  const [tokHistory, setTokHistory] = useState<number[]>([]);
  const [lastStats, setLastStats] = useState<ChatStats | null>(null);

  const streamIdRef = useRef<string | null>(null);
  const bufferRef = useRef("");
  const activeModelRef = useRef("");

  useEffect(() => {
    activeModelRef.current = activeModel;
  }, [activeModel]);

  // ---------------------------------------------------------------------
  // Ollama state
  // ---------------------------------------------------------------------

  const refreshModels = useCallback(async () => {
    try {
      const [nextHealth, list] = await Promise.all([
        api.health(),
        api.listModels(),
      ]);
      setHealth(nextHealth);
      setModels(list);
      setActiveModel((previous) => previous || list[0]?.name || "");
      setError(null);
    } catch (err) {
      setError(String(err));
    }
  }, []);

  const refreshRunning = useCallback(async () => {
    try {
      setRunning(await api.runningModels());
    } catch {
      // Ollama can be briefly unresponsive mid-load; the next poll catches up.
    }
  }, []);

  useEffect(() => {
    refreshModels();
  }, [refreshModels]);

  useEffect(() => {
    refreshRunning();
    const timer = window.setInterval(refreshRunning, 5000);
    return () => window.clearInterval(timer);
  }, [refreshRunning]);

  // ---------------------------------------------------------------------
  // Backend event subscriptions
  // ---------------------------------------------------------------------

  useEffect(() => {
    let disposed = false;
    const unlisteners: Array<() => void> = [];

    const register = (pending: Promise<() => void>) => {
      pending
        .then((unlisten) => {
          if (disposed) unlisten();
          else unlisteners.push(unlisten);
        })
        .catch(() => undefined);
    };

    register(
      onTelemetry((sample) => {
        setLatest(sample);
        setHistory((previous) => {
          const next = [...previous, sample];
          return next.length > HISTORY_LIMIT
            ? next.slice(next.length - HISTORY_LIMIT)
            : next;
        });
      }),
    );

    register(
      onChatChunk((payload) => {
        if (payload.stream_id !== streamIdRef.current) return;
        bufferRef.current += payload.content;
        setStreamingText(bufferRef.current);
      }),
    );

    register(
      onChatDone((payload) => {
        if (payload.stream_id !== streamIdRef.current) return;

        const text = bufferRef.current;
        bufferRef.current = "";
        streamIdRef.current = null;

        setStreaming(false);
        setStreamingText("");
        if (text.trim()) {
          setMessages((previous) => [
            ...previous,
            { role: "assistant", content: text },
          ]);
        }

        setLastStats(payload.stats);
        const throughput = payload.stats?.tokens_per_second;
        if (typeof throughput === "number" && throughput > 0) {
          setTokHistory((previous) => push(previous, throughput));
        }

        refreshRunning();
      }),
    );

    register(
      onChatError((payload) => {
        if (payload.stream_id !== streamIdRef.current) return;
        streamIdRef.current = null;
        bufferRef.current = "";
        setStreaming(false);
        setStreamingText("");
        setError(payload.message);
      }),
    );

    return () => {
      disposed = true;
      unlisteners.forEach((unlisten) => unlisten());
    };
  }, [refreshRunning]);

  // ---------------------------------------------------------------------
  // Actions
  // ---------------------------------------------------------------------

  const handleSend = useCallback(
    async (text: string) => {
      const model = activeModelRef.current;
      if (!model) return;

      setError(null);
      const outgoing: ChatMessage[] = [
        ...messages,
        { role: "user", content: text },
      ];
      setMessages(outgoing);

      bufferRef.current = "";
      setStreamingText("");
      setStreaming(true);

      const streamId = uid();
      streamIdRef.current = streamId;

      try {
        await api.startChat({ streamId, model, messages: outgoing });
      } catch (err) {
        streamIdRef.current = null;
        setStreaming(false);
        setError(String(err));
      }
    },
    [messages],
  );

  const handleStop = useCallback(async () => {
    const streamId = streamIdRef.current;
    if (!streamId) return;
    try {
      await api.cancelChat(streamId);
    } catch (err) {
      setError(String(err));
    }
  }, []);

  const handleUnload = useCallback(
    async (model: string) => {
      try {
        await api.unloadModel(model);
        await refreshRunning();
      } catch (err) {
        setError(String(err));
      }
    },
    [refreshRunning],
  );

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="brand__mark" aria-hidden="true" />
          <span className="brand__name">Hush</span>
        </div>
        <div className="topbar__meta">
          <span className={`pill pill--${health?.reachable ? "ok" : "bad"}`}>
            {health?.reachable
              ? `Ollama ${health.version ?? ""}`.trim()
              : "Ollama offline"}
          </span>
          <span className="topbar__url">{health?.base_url ?? ""}</span>
          <span className="topbar__models">
            {models.length} model{models.length === 1 ? "" : "s"}
          </span>
        </div>
      </header>

      <main className="layout">
        <Sidebar
          health={health}
          models={models}
          running={running}
          activeModel={activeModel}
          busy={streaming}
          onSelect={setActiveModel}
          onRefresh={refreshModels}
          onUnload={handleUnload}
        />

        <ChatPanel
          messages={messages}
          streamingText={streamingText}
          streaming={streaming}
          error={error}
          model={activeModel}
          onSend={handleSend}
          onStop={handleStop}
        />

        <ThermalPanel
          latest={latest}
          history={history}
          tokHistory={tokHistory}
          lastStats={lastStats}
        />
      </main>
    </div>
  );
}
