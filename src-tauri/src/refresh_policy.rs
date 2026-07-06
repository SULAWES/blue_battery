const MIN_REFRESH_SPACING_MS: i64 = 15_000;
const FAILURE_BACKOFF_MS: i64 = 30_000;

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum RefreshDecision {
    Run,
    UseCached { reason: &'static str },
    Skip { reason: &'static str },
}

#[derive(Debug, Default)]
pub struct RefreshGate {
    running: bool,
    last_started_at_ms: Option<i64>,
    last_failed_at_ms: Option<i64>,
}

impl RefreshGate {
    pub fn begin(&mut self, now_ms: i64, cached_refreshed_at_ms: Option<i64>) -> RefreshDecision {
        if self.running {
            return cached_or_skip(cached_refreshed_at_ms, "refresh already running");
        }

        if self
            .last_failed_at_ms
            .is_some_and(|failed_at_ms| now_ms.saturating_sub(failed_at_ms) < FAILURE_BACKOFF_MS)
        {
            return cached_or_skip(cached_refreshed_at_ms, "refresh failure backoff active");
        }

        if self.last_started_at_ms.is_some_and(|started_at_ms| {
            now_ms.saturating_sub(started_at_ms) < MIN_REFRESH_SPACING_MS
        }) {
            return cached_or_skip(cached_refreshed_at_ms, "refresh interval not elapsed");
        }

        self.running = true;
        self.last_started_at_ms = Some(now_ms);
        RefreshDecision::Run
    }

    pub fn finish_success(&mut self, _now_ms: i64) {
        self.running = false;
        self.last_failed_at_ms = None;
    }

    pub fn finish_failure(&mut self, now_ms: i64) {
        self.running = false;
        self.last_failed_at_ms = Some(now_ms);
    }
}

fn cached_or_skip(cached_refreshed_at_ms: Option<i64>, reason: &'static str) -> RefreshDecision {
    if cached_refreshed_at_ms.is_some() {
        RefreshDecision::UseCached { reason }
    } else {
        RefreshDecision::Skip { reason }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn running_refresh_reuses_cached_result_without_starting_another_scan() {
        let mut gate = RefreshGate::default();

        assert_eq!(gate.begin(1_000, None), RefreshDecision::Run);

        assert_eq!(
            gate.begin(1_500, Some(1_000)),
            RefreshDecision::UseCached {
                reason: "refresh already running"
            }
        );
    }

    #[test]
    fn recent_refresh_reuses_cached_result_to_avoid_dense_scans() {
        let mut gate = RefreshGate::default();

        assert_eq!(gate.begin(1_000, None), RefreshDecision::Run);
        gate.finish_success(2_000);

        assert_eq!(
            gate.begin(10_000, Some(2_000)),
            RefreshDecision::UseCached {
                reason: "refresh interval not elapsed"
            }
        );
    }

    #[test]
    fn read_failure_enters_short_backoff_without_cached_result() {
        let mut gate = RefreshGate::default();

        assert_eq!(gate.begin(1_000, None), RefreshDecision::Run);
        gate.finish_failure(2_000);

        assert_eq!(
            gate.begin(10_000, None),
            RefreshDecision::Skip {
                reason: "refresh failure backoff active"
            }
        );
    }

    #[test]
    fn refresh_can_run_after_backoff_expires() {
        let mut gate = RefreshGate::default();

        assert_eq!(gate.begin(1_000, None), RefreshDecision::Run);
        gate.finish_failure(2_000);

        assert_eq!(gate.begin(40_000, None), RefreshDecision::Run);
    }
}
