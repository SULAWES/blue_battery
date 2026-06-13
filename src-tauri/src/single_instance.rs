use tauri::AppHandle;
use windows::{
    Win32::{
        Foundation::{CloseHandle, ERROR_ALREADY_EXISTS, GetLastError, HANDLE, WAIT_OBJECT_0},
        System::Threading::{
            CreateEventW, CreateMutexW, EVENT_MODIFY_STATE, INFINITE, OpenEventW, SetEvent,
            WaitForSingleObject,
        },
    },
    core::PCWSTR,
};

#[cfg(test)]
use windows::Win32::Foundation::WAIT_TIMEOUT;

pub const SINGLE_INSTANCE_MUTEX_NAME: &str = r"Local\BlueBattery.SingleInstance";
pub const SINGLE_INSTANCE_EVENT_NAME: &str = r"Local\BlueBattery.Activate";

pub enum InstanceClaim {
    Primary(InstanceGuard),
    SecondarySignaled,
}

pub struct InstanceGuard {
    _mutex: OwnedHandle,
    activation_event: OwnedHandle,
}

struct OwnedHandle(HANDLE);

unsafe impl Send for OwnedHandle {}
unsafe impl Sync for OwnedHandle {}

#[derive(Clone, Copy)]
struct BorrowedHandle(HANDLE);

unsafe impl Send for BorrowedHandle {}
unsafe impl Sync for BorrowedHandle {}

impl BorrowedHandle {
    fn raw(self) -> HANDLE {
        self.0
    }
}

impl InstanceGuard {
    fn activation_event(&self) -> HANDLE {
        self.activation_event.0
    }
}

impl Drop for OwnedHandle {
    fn drop(&mut self) {
        if !self.0.is_invalid() {
            let _ = unsafe { CloseHandle(self.0) };
        }
    }
}

pub fn claim_or_signal_existing() -> Result<InstanceClaim, String> {
    claim_named(SINGLE_INSTANCE_MUTEX_NAME, SINGLE_INSTANCE_EVENT_NAME)
}

pub fn start_activation_listener(guard: &InstanceGuard, app: AppHandle) -> Result<(), String> {
    let activation_event = BorrowedHandle(guard.activation_event());

    std::thread::Builder::new()
        .name("blue-battery-activation".to_string())
        .spawn(move || {
            loop {
                match wait_for_activation_event_forever(activation_event.raw()) {
                    Ok(()) => {
                        let app = app.clone();
                        let panel_app = app.clone();
                        let _ = app.run_on_main_thread(move || {
                            let _ = crate::panel_window::show(&panel_app, None);
                        });
                    }
                    Err(error) => {
                        eprintln!("Blue Battery activation listener stopped: {error}");
                        break;
                    }
                }
            }
        })
        .map(|_| ())
        .map_err(|error| format!("Failed to start activation listener: {error}"))
}

fn claim_named(mutex_name: &str, event_name: &str) -> Result<InstanceClaim, String> {
    let mutex_name = wide_null(mutex_name);
    let mutex = unsafe { CreateMutexW(None, true, PCWSTR(mutex_name.as_ptr())) }
        .map_err(|error| format!("Failed to create single-instance mutex: {error}"))?;

    if unsafe { GetLastError() } == ERROR_ALREADY_EXISTS {
        let _mutex = OwnedHandle(mutex);
        signal_existing_instance(event_name)?;
        return Ok(InstanceClaim::SecondarySignaled);
    }

    let event_name = wide_null(event_name);
    let activation_event = unsafe { CreateEventW(None, false, false, PCWSTR(event_name.as_ptr())) }
        .map_err(|error| format!("Failed to create activation event: {error}"))?;

    Ok(InstanceClaim::Primary(InstanceGuard {
        _mutex: OwnedHandle(mutex),
        activation_event: OwnedHandle(activation_event),
    }))
}

fn signal_existing_instance(event_name: &str) -> Result<(), String> {
    let event_name = wide_null(event_name);
    let event = unsafe { OpenEventW(EVENT_MODIFY_STATE, false, PCWSTR(event_name.as_ptr())) }
        .map_err(|error| format!("Failed to open activation event: {error}"))?;
    let _event = OwnedHandle(event);

    unsafe { SetEvent(event) }
        .map_err(|error| format!("Failed to signal activation event: {error}"))
}

fn wait_for_activation_event_forever(event: HANDLE) -> Result<(), String> {
    match unsafe { WaitForSingleObject(event, INFINITE) } {
        WAIT_OBJECT_0 => Ok(()),
        status => Err(format!("activation wait returned {status:?}")),
    }
}

#[cfg(test)]
fn wait_for_activation_event(event: HANDLE, timeout: std::time::Duration) -> Result<bool, String> {
    let timeout_ms = u32::try_from(timeout.as_millis()).unwrap_or(u32::MAX);
    match unsafe { WaitForSingleObject(event, timeout_ms) } {
        WAIT_OBJECT_0 => Ok(true),
        WAIT_TIMEOUT => Ok(false),
        status => Err(format!("activation wait returned {status:?}")),
    }
}

fn wide_null(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::{Duration, SystemTime, UNIX_EPOCH};

    #[test]
    fn instance_names_stay_in_the_current_user_namespace() {
        assert!(SINGLE_INSTANCE_MUTEX_NAME.starts_with(r"Local\"));
        assert!(SINGLE_INSTANCE_EVENT_NAME.starts_with(r"Local\"));
    }

    #[test]
    fn second_claim_is_reported_as_secondary_and_signals_activation() {
        let suffix = unique_test_suffix();
        let mutex_name = format!(r"Local\BlueBattery.Test.Mutex.{suffix}");
        let event_name = format!(r"Local\BlueBattery.Test.Event.{suffix}");

        let first_claim = claim_named(&mutex_name, &event_name).expect("first claim");
        let InstanceClaim::Primary(primary) = first_claim else {
            panic!("first claim must own the instance");
        };

        let second_claim = claim_named(&mutex_name, &event_name).expect("second claim");
        assert!(matches!(second_claim, InstanceClaim::SecondarySignaled));
        assert!(
            wait_for_activation_event(primary.activation_event(), Duration::from_millis(250))
                .expect("activation wait"),
            "secondary launch should signal the primary activation event"
        );
    }

    fn unique_test_suffix() -> String {
        let now = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap_or_default()
            .as_nanos();
        format!("{}-{now}", std::process::id())
    }
}
