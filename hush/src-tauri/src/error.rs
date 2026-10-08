use serde::{Serialize, Serializer};

/// Every failure that can cross the Tauri command boundary.
///
/// `Serialize` is hand-written so the frontend always receives a plain,
/// human-readable string instead of a tagged enum it would have to guess at.
#[derive(Debug, thiserror::Error)]
pub enum HushError {
    #[error("could not reach Ollama at {0} — is `ollama serve` running?")]
    Unreachable(String),

    #[error("Ollama returned an error: {0}")]
    Ollama(String),

    #[error("unexpected response from Ollama: {0}")]
    Decode(String),

    #[error("network error: {0}")]
    Http(#[from] reqwest::Error),

    #[error("GPU telemetry unavailable: {0}")]
    Gpu(String),

    #[error("{0}")]
    Other(String),
}

impl From<anyhow::Error> for HushError {
    fn from(value: anyhow::Error) -> Self {
        HushError::Other(value.to_string())
    }
}

impl From<serde_json::Error> for HushError {
    fn from(value: serde_json::Error) -> Self {
        HushError::Decode(value.to_string())
    }
}

impl From<std::io::Error> for HushError {
    fn from(value: std::io::Error) -> Self {
        HushError::Other(value.to_string())
    }
}

impl Serialize for HushError {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        serializer.serialize_str(&self.to_string())
    }
}

pub type Result<T> = std::result::Result<T, HushError>;
