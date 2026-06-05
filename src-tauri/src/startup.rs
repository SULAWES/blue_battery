use std::{env, path::Path};

use windows::{
    Win32::{
        Foundation::{ERROR_FILE_NOT_FOUND, ERROR_SUCCESS, WIN32_ERROR},
        System::Registry::{
            HKEY, HKEY_CURRENT_USER, KEY_READ, KEY_SET_VALUE, REG_OPTION_NON_VOLATILE, REG_SZ,
            REG_VALUE_TYPE, RegCloseKey, RegCreateKeyExW, RegDeleteValueW, RegOpenKeyExW,
            RegQueryValueExW, RegSetValueExW,
        },
    },
    core::PCWSTR,
};

const RUN_KEY_PATH: &str = r"Software\Microsoft\Windows\CurrentVersion\Run";
const RUN_VALUE_NAME: &str = "Blue Battery";

pub fn is_enabled() -> Result<bool, String> {
    let exe = current_exe()?;
    let Some(value) = read_run_value()? else {
        return Ok(false);
    };

    Ok(startup_value_matches(&value, &exe))
}

pub fn set_enabled(enabled: bool) -> Result<bool, String> {
    if enabled {
        write_run_value(&startup_command(&current_exe()?))?;
    } else {
        delete_run_value()?;
    }

    is_enabled()
}

fn current_exe() -> Result<std::path::PathBuf, String> {
    env::current_exe().map_err(|error| format!("Failed to resolve current executable: {error}"))
}

fn startup_command(exe: &Path) -> String {
    format!("\"{}\"", exe.display())
}

fn startup_value_matches(value: &str, exe: &Path) -> bool {
    value.trim() == startup_command(exe)
}

fn read_run_value() -> Result<Option<String>, String> {
    let Some(key) = open_run_key(KEY_READ, "open startup key")? else {
        return Ok(None);
    };

    let value_name = wide_null(RUN_VALUE_NAME);
    let mut value_type = REG_VALUE_TYPE::default();
    let mut byte_len = 0u32;
    let status = unsafe {
        RegQueryValueExW(
            key.raw(),
            PCWSTR(value_name.as_ptr()),
            None,
            Some(&mut value_type),
            None,
            Some(&mut byte_len),
        )
    };

    if status == ERROR_FILE_NOT_FOUND {
        return Ok(None);
    }
    win32_ok(status, "query startup value size")?;

    if value_type != REG_SZ || byte_len == 0 {
        return Ok(None);
    }

    let mut bytes = vec![0u8; byte_len as usize];
    let status = unsafe {
        RegQueryValueExW(
            key.raw(),
            PCWSTR(value_name.as_ptr()),
            None,
            Some(&mut value_type),
            Some(bytes.as_mut_ptr()),
            Some(&mut byte_len),
        )
    };
    win32_ok(status, "query startup value")?;

    Ok(Some(string_from_reg_sz(&bytes[..byte_len as usize])))
}

fn write_run_value(command: &str) -> Result<(), String> {
    let key = create_run_key()?;
    let value_name = wide_null(RUN_VALUE_NAME);
    let value = wide_null(command);
    let bytes = wide_as_bytes(&value);
    let status = unsafe {
        RegSetValueExW(
            key.raw(),
            PCWSTR(value_name.as_ptr()),
            None,
            REG_SZ,
            Some(bytes),
        )
    };

    win32_ok(status, "write startup value")
}

fn delete_run_value() -> Result<(), String> {
    let Some(key) = open_run_key(KEY_SET_VALUE, "open startup key")? else {
        return Ok(());
    };

    let value_name = wide_null(RUN_VALUE_NAME);
    let status = unsafe { RegDeleteValueW(key.raw(), PCWSTR(value_name.as_ptr())) };
    if status == ERROR_FILE_NOT_FOUND {
        return Ok(());
    }

    win32_ok(status, "delete startup value")
}

fn open_run_key(
    access: windows::Win32::System::Registry::REG_SAM_FLAGS,
    context: &str,
) -> Result<Option<RegistryKey>, String> {
    let path = wide_null(RUN_KEY_PATH);
    let mut key = HKEY::default();
    let status = unsafe {
        RegOpenKeyExW(
            HKEY_CURRENT_USER,
            PCWSTR(path.as_ptr()),
            None,
            access,
            &mut key,
        )
    };

    if status == ERROR_FILE_NOT_FOUND {
        return Ok(None);
    }
    win32_ok(status, context)?;
    Ok(Some(RegistryKey(key)))
}

fn create_run_key() -> Result<RegistryKey, String> {
    let path = wide_null(RUN_KEY_PATH);
    let mut key = HKEY::default();
    let status = unsafe {
        RegCreateKeyExW(
            HKEY_CURRENT_USER,
            PCWSTR(path.as_ptr()),
            None,
            PCWSTR::null(),
            REG_OPTION_NON_VOLATILE,
            KEY_SET_VALUE | KEY_READ,
            None,
            &mut key,
            None,
        )
    };

    win32_ok(status, "create startup key")?;
    Ok(RegistryKey(key))
}

fn win32_ok(status: WIN32_ERROR, context: &str) -> Result<(), String> {
    if status == ERROR_SUCCESS {
        Ok(())
    } else {
        Err(format!("{context}: Win32 error {}", status.0))
    }
}

fn wide_null(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}

fn wide_as_bytes(value: &[u16]) -> &[u8] {
    unsafe { std::slice::from_raw_parts(value.as_ptr() as *const u8, std::mem::size_of_val(value)) }
}

fn string_from_reg_sz(bytes: &[u8]) -> String {
    let wide = bytes
        .chunks_exact(2)
        .map(|chunk| u16::from_le_bytes([chunk[0], chunk[1]]))
        .take_while(|unit| *unit != 0)
        .collect::<Vec<_>>();

    String::from_utf16_lossy(&wide)
}

struct RegistryKey(HKEY);

impl RegistryKey {
    fn raw(&self) -> HKEY {
        self.0
    }
}

impl Drop for RegistryKey {
    fn drop(&mut self) {
        let _ = unsafe { RegCloseKey(self.0) };
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::Path;

    #[test]
    fn startup_command_quotes_the_executable_path() {
        let command = startup_command(Path::new(r"C:\Program Files\Blue Battery\blue-battery.exe"));

        assert_eq!(
            command,
            r#""C:\Program Files\Blue Battery\blue-battery.exe""#
        );
    }

    #[test]
    fn startup_value_matches_current_executable_command() {
        let exe = Path::new(r"D:\Dev\blue_battery\blue-battery.exe");
        let command = startup_command(exe);

        assert!(startup_value_matches(&command, exe));
        assert!(startup_value_matches(&format!("  {command}  "), exe));
        assert!(!startup_value_matches(
            r#""D:\Other\blue-battery.exe""#,
            exe
        ));
    }
}
