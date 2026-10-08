//! Minimal, dependency-light client for the local Ollama HTTP API.
//!
//! Only the endpoints we actually need: `/api/version`, `/api/tags`,
//! `/api/ps`, `/api/show`, `/api/chat` (streaming NDJSON) and `/api/generate`
//! (used purely to unload a model).

use crate::error::{HushError, Result};
use serde::{Deserialize, Serialize};
use serde_json::{json, Value};

pub const DEFAULT_BASE_URL: &str = "http://127.0.0.1:11434";

// ---------------------------------------------------------------------------
// Model metadata
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct ModelDetails {
    #[serde(default)]
    pub format: Option<String>,
    #[serde(default)]
    pub family: Option<String>,
    #[serde(default)]
    pub parameter_size: Option<String>,
    #[serde(default)]
    pub quantization_level: Option<String>,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct ModelInfo {
    pub name: String,
    #[serde(default)]
    pub model: Option<String>,
    #[serde(default)]
    pub size: u64,
    #[serde(default)]
    pub digest: Option<String>,
    #[serde(default)]
    pub modified_at: Option<String>,
    #[serde(default)]
    pub details: Option<ModelDetails>,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct RunningModel {
    pub name: String,
    #[serde(default)]
    pub model: Option<String>,
    #[serde(default)]
    pub size: u64,
    #[serde(default)]
    pub size_vram: u64,
    #[serde(default)]
    pub digest: Option<String>,
    #[serde(default)]
    pub expires_at: Option<String>,
}

#[derive(Debug, Deserialize)]
struct TagsResponse {
    #[serde(default)]
    models: Vec<ModelInfo>,
}

#[derive(Debug, Deserialize)]
struct PsResponse {
    #[serde(default)]
    models: Vec<RunningModel>,
}

#[derive(Debug, Deserialize)]
struct VersionResponse {
    #[serde(default)]
    version: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct Health {
    pub reachable: bool,
    pub base_url: String,
    pub version: Option<String>,
    pub model_count: usize,
    pub error: Option<String>,
}

// ---------------------------------------------------------------------------
// Chat wire format
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct ToolCallFunction {
    pub name: String,
    #[serde(default)]
    pub arguments: Value,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct ToolCall {
    #[serde(default)]
    pub function: Option<ToolCallFunction>,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct ChatMessage {
    pub role: String,
    #[serde(default)]
    pub content: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub images: Option<Vec<String>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub tool_calls: Option<Vec<ToolCall>>,
}

impl ChatMessage {
    pub fn new(role: impl Into<String>, content: impl Into<String>) -> Self {
        ChatMessage {
            role: role.into(),
            content: content.into(),
            images: None,
            tool_calls: None,
        }
    }
}

/// One NDJSON line from `/api/chat`.
#[derive(Debug, Clone, Default, Deserialize)]
pub struct ChatChunk {
    #[serde(default)]
    pub model: Option<String>,
    #[serde(default)]
    pub message: Option<ChatMessage>,
    #[serde(default)]
    pub done: bool,
    #[serde(default)]
    pub total_duration: Option<u64>,
    #[serde(default)]
    pub load_duration: Option<u64>,
    #[serde(default)]
    pub prompt_eval_count: Option<u64>,
    #[serde(default)]
    pub prompt_eval_duration: Option<u64>,
    #[serde(default)]
    pub eval_count: Option<u64>,
    #[serde(default)]
    pub eval_duration: Option<u64>,
}

/// Timing summary for a completed turn. All durations are milliseconds, and
/// `tokens_per_second` is the number the governor watches when pacing.
#[derive(Debug, Clone, Default, Serialize)]
pub struct ChatStats {
    pub total_duration_ms: Option<f64>,
    pub load_duration_ms: Option<f64>,
    pub prompt_eval_count: Option<u64>,
    pub prompt_eval_duration_ms: Option<f64>,
    pub eval_count: Option<u64>,
    pub eval_duration_ms: Option<f64>,
    pub tokens_per_second: Option<f64>,
}

fn ns_to_ms(value: Option<u64>) -> Option<f64> {
    value.map(|v| v as f64 / 1_000_000.0)
}

impl ChatChunk {
    pub fn into_stats(self) -> ChatStats {
        let tokens_per_second = match (self.eval_count, self.eval_duration) {
            (Some(count), Some(duration)) if duration > 0 => {
                Some(count as f64 / (duration as f64 / 1_000_000_000.0))
            }
            _ => None,
        };

        ChatStats {
            total_duration_ms: ns_to_ms(self.total_duration),
            load_duration_ms: ns_to_ms(self.load_duration),
            prompt_eval_count: self.prompt_eval_count,
            prompt_eval_duration_ms: ns_to_ms(self.prompt_eval_duration),
            eval_count: self.eval_count,
            eval_duration_ms: ns_to_ms(self.eval_duration),
            tokens_per_second,
        }
    }
}

// ---------------------------------------------------------------------------
// Client helpers
// ---------------------------------------------------------------------------

/// Build the shared HTTP client.
///
/// `no_proxy` matters: a corporate or VPN proxy in the environment would
/// otherwise swallow requests aimed at `127.0.0.1:11434`.
pub fn build_client() -> Result<reqwest::Client> {
    reqwest::Client::builder()
        .no_proxy()
        .connect_timeout(std::time::Duration::from_secs(5))
        .build()
        .map_err(HushError::from)
}

async fn read_json<T: for<'de> Deserialize<'de>>(resp: reqwest::Response) -> Result<T> {
    let status = resp.status();
    let text = resp.text().await?;
    if !status.is_success() {
        return Err(HushError::Ollama(format!(
            "{} {}",
            status.as_u16(),
            text.trim()
        )));
    }
    serde_json::from_str(&text)
        .map_err(|err| HushError::Decode(format!("{err} (body was: {})", text.trim())))
}

pub async fn health(client: &reqwest::Client, base_url: &str) -> Health {
    let version = match client
        .get(format!("{base_url}/api/version"))
        .send()
        .await
    {
        Ok(resp) => match read_json::<VersionResponse>(resp).await {
            Ok(v) => Some(v.version),
            Err(_) => None,
        },
        Err(err) => {
            return Health {
                reachable: false,
                base_url: base_url.to_string(),
                version: None,
                model_count: 0,
                error: Some(err.to_string()),
            }
        }
    };

    let model_count = list_models(client, base_url)
        .await
        .map(|m| m.len())
        .unwrap_or(0);

    Health {
        reachable: true,
        base_url: base_url.to_string(),
        version,
        model_count,
        error: None,
    }
}

pub async fn list_models(client: &reqwest::Client, base_url: &str) -> Result<Vec<ModelInfo>> {
    let resp = client
        .get(format!("{base_url}/api/tags"))
        .send()
        .await
        .map_err(|_| HushError::Unreachable(base_url.to_string()))?;
    let tags: TagsResponse = read_json(resp).await?;
    Ok(tags.models)
}

pub async fn running_models(client: &reqwest::Client, base_url: &str) -> Result<Vec<RunningModel>> {
    let resp = client
        .get(format!("{base_url}/api/ps"))
        .send()
        .await
        .map_err(|_| HushError::Unreachable(base_url.to_string()))?;
    let ps: PsResponse = read_json(resp).await?;
    Ok(ps.models)
}

/// Ask Ollama to evict a model from VRAM immediately.
///
/// This is the single highest-leverage quiet-mode action: once the weights
/// leave VRAM the GPU falls back to idle clocks and the fans stop.
pub async fn unload(client: &reqwest::Client, base_url: &str, model: &str) -> Result<()> {
    let resp = client
        .post(format!("{base_url}/api/generate"))
        .json(&json!({ "model": model, "keep_alive": 0 }))
        .send()
        .await
        .map_err(|_| HushError::Unreachable(base_url.to_string()))?;

    let status = resp.status();
    if !status.is_success() {
        let text = resp.text().await.unwrap_or_default();
        return Err(HushError::Ollama(format!("{status}: {}", text.trim())));
    }
    Ok(())
}

/// Body for `/api/chat`. `keep_alive` is passed through as raw JSON because
/// Ollama accepts either a duration string ("5m", "-1") or a number of seconds.
pub fn chat_body(
    model: &str,
    messages: &[ChatMessage],
    options: Option<Value>,
    keep_alive: Option<Value>,
    tools: Option<Value>,
) -> Value {
    let mut body = json!({
        "model": model,
        "messages": messages,
        "stream": true,
        "options": options.unwrap_or_else(|| json!({})),
        "keep_alive": keep_alive.unwrap_or_else(|| json!("5m")),
    });

    if let Some(tools) = tools {
        body["tools"] = tools;
    }

    body
}
