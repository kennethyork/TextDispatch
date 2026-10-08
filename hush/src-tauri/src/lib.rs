mod commands;
mod error;
mod ollama;
mod state;
mod telemetry;

use state::AppState;
use std::time::Duration;
use tauri::{Emitter, Manager};

pub const EVENT_CHAT_CHUNK: &str = "hush://chat/chunk";
pub const EVENT_CHAT_TOOL: &str = "hush://chat/tool";
pub const EVENT_CHAT_DONE: &str = "hush://chat/done";
pub const EVENT_CHAT_ERROR: &str = "hush://chat/error";
pub const EVENT_TELEMETRY: &str = "hush://telemetry";

const TELEMETRY_INTERVAL_MS: u64 = 1000;

/// Pumps one hardware sample per second to the frontend.
///
/// This runs whether or not anything is generating, so the thermal dashboard
/// shows the idle-to-busy transition — the whole point of the app is being
/// able to see the GPU settle back down.
fn spawn_telemetry_pump(app: tauri::AppHandle) {
    tauri::async_runtime::spawn(async move {
        let mut ticker = tokio::time::interval(Duration::from_millis(TELEMETRY_INTERVAL_MS));
        ticker.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Delay);

        loop {
            ticker.tick().await;
            let sample = {
                let state = app.state::<AppState>();
                state.sample_now()
            };
            let _ = app.emit(EVENT_TELEMETRY, sample);
        }
    });
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .manage(AppState::new())
        .setup(|app| {
            spawn_telemetry_pump(app.handle().clone());
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            commands::ollama_health,
            commands::get_base_url,
            commands::set_base_url,
            commands::list_models,
            commands::running_models,
            commands::unload_model,
            commands::get_telemetry,
            commands::sample_telemetry,
            commands::start_chat,
            commands::cancel_chat,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
