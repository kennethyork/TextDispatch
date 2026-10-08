import { useEffect, useRef, useState } from "react";
import type { ChatMessage } from "../types";

interface ChatPanelProps {
  messages: ChatMessage[];
  streamingText: string;
  streaming: boolean;
  error: string | null;
  model: string;
  onSend: (text: string) => void;
  onStop: () => void;
}

export function ChatPanel({
  messages,
  streamingText,
  streaming,
  error,
  model,
  onSend,
  onStop,
}: ChatPanelProps) {
  const [input, setInput] = useState("");
  const scrollRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  // Follow the tail as tokens arrive.
  useEffect(() => {
    const node = scrollRef.current;
    if (node) {
      node.scrollTop = node.scrollHeight;
    }
  }, [messages, streamingText]);

  const submit = () => {
    const text = input.trim();
    if (!text || streaming) return;
    onSend(text);
    setInput("");
  };

  const handleKeyDown = (event: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      submit();
    }
  };

  return (
    <section className="chat">
      <div className="chat__scroll" ref={scrollRef}>
        {messages.length === 0 && !streamingText && (
          <div className="chat__placeholder">
            <h1>Hush</h1>
            <p>
              Local models, paced so your GPU stays quiet. Pick a model and ask
              something &mdash; watch the fan duty stay flat on the right.
            </p>
          </div>
        )}

        {messages.map((message, index) => (
          <article
            key={`${message.role}-${index}`}
            className={`bubble bubble--${message.role}`}
          >
            <header className="bubble__role">{message.role}</header>
            <div className="bubble__body">{message.content}</div>
          </article>
        ))}

        {streamingText && (
          <article className="bubble bubble--assistant">
            <header className="bubble__role">assistant</header>
            <div className="bubble__body">
              {streamingText}
              <span className="caret" />
            </div>
          </article>
        )}

        {streaming && !streamingText && (
          <article className="bubble bubble--assistant">
            <header className="bubble__role">assistant</header>
            <div className="bubble__body bubble__body--muted">
              loading model into VRAM&hellip;
              <span className="caret" />
            </div>
          </article>
        )}
      </div>

      {error && <div className="chat__error">{error}</div>}

      <footer className="composer">
        <textarea
          ref={textareaRef}
          className="composer__input"
          value={input}
          placeholder={
            model
              ? `Message ${model} — Enter to send, Shift+Enter for a newline`
              : "Select a model to begin"
          }
          onChange={(event) => setInput(event.target.value)}
          onKeyDown={handleKeyDown}
          disabled={!model}
          rows={3}
        />
        <div className="composer__actions">
          {streaming ? (
            <button
              type="button"
              className="button button--stop"
              onClick={onStop}
            >
              Stop
            </button>
          ) : (
            <button
              type="button"
              className="button"
              onClick={submit}
              disabled={!input.trim() || !model}
            >
              Send
            </button>
          )}
        </div>
      </footer>
    </section>
  );
}
