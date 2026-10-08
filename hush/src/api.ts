import { invoke } from "@tauri-apps/api/core";
import { listen, type UnlistenFn } from "@tauri-apps/api/event";
import type {
  ChatChunkEvent,
  ChatDoneEvent,
  ChatErrorEvent,
  ChatMessage,
  ChatStats,
  Health,
  ModelInfo,
  RunningModel,
  TelemetrySample,
} from "./types";

export const EVENTS = {
  telemetry: "hush://telemetry",
  chatChunk: "hush://chat/chunk",
  chatTool: "hush://chat/tool",
  chatDone: "hush://chat/done",
  chatError: "hush://chat/error",
} as const;

export interface StartChatArgs {
  streamId: string;
  model: string;
  messages: ChatMessage[];
  options?: Record<string, unknown> | null;
  keepAlive?: string | number | null;
}

export const api = {
  health: () => invoke<Health>("ollama_health"),
  getBaseUrl: () => invoke<string>("get_base_url"),
  setBaseUrl: (baseUrl: string) => invoke<string>("set_base_url", { baseUrl }),
  listModels: () => invoke<ModelInfo[]>("list_models"),
  runningModels: () => invoke<RunningModel[]>("running_models"),
  unloadModel: (model: string) => invoke<void>("unload_model", { model }),
  getTelemetry: () => invoke<TelemetrySample>("get_telemetry"),
  sampleTelemetry: () => invoke<TelemetrySample>("sample_telemetry"),

  startChat: (args: StartChatArgs) =>
    invoke<void>("start_chat", {
      request: {
        stream_id: args.streamId,
        model: args.model,
        messages: args.messages,
        options: args.options ?? null,
        keep_alive: args.keepAlive ?? null,
      },
    }),

  cancelChat: (streamId: string) => invoke<boolean>("cancel_chat", { streamId }),
};

export function onTelemetry(
  handler: (sample: TelemetrySample) => void,
): Promise<UnlistenFn> {
  return listen<TelemetrySample>(EVENTS.telemetry, (event) =>
    handler(event.payload),
  );
}

export function onChatChunk(
  handler: (payload: ChatChunkEvent) => void,
): Promise<UnlistenFn> {
  return listen<ChatChunkEvent>(EVENTS.chatChunk, (event) =>
    handler(event.payload),
  );
}

export function onChatDone(
  handler: (payload: ChatDoneEvent) => void,
): Promise<UnlistenFn> {
  return listen<ChatDoneEvent>(EVENTS.chatDone, (event) =>
    handler(event.payload),
  );
}

export function onChatError(
  handler: (payload: ChatErrorEvent) => void,
): Promise<UnlistenFn> {
  return listen<ChatErrorEvent>(EVENTS.chatError, (event) =>
    handler(event.payload),
  );
}

export function formatBytes(bytes: number): string {
  if (!bytes || bytes <= 0) return "0 B";
  const units = ["B", "KB", "MB", "GB", "TB"];
  const exponent = Math.min(
    Math.floor(Math.log(bytes) / Math.log(1024)),
    units.length - 1,
  );
  const value = bytes / Math.pow(1024, exponent);
  return `${value.toFixed(value >= 10 || exponent === 0 ? 0 : 1)} ${units[exponent]}`;
}

export type { ChatStats, ChatMessage };
