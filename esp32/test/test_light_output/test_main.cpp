// Host tests for LightOutput.h:  pio test -e native
#include <unity.h>
#include "LightOutput.h"

using namespace LightOutput;
namespace Id = LightOutput::Identify;

static constexpr uint8_t PLAIN   = protocol::LED_MODE_PLAIN;
static constexpr uint8_t DYNAMIC = protocol::LED_MODE_DYNAMIC;

void setUp() {}
void tearDown() {}

void test_power_on_follows_the_mode() {
    TEST_ASSERT_TRUE(source(true, PLAIN, false, false) == Source::Plain);
    TEST_ASSERT_TRUE(source(true, DYNAMIC, false, false) == Source::Dynamic);
}

void test_power_off_is_dark_in_both_modes() {
    TEST_ASSERT_TRUE(source(false, PLAIN, false, false) == Source::Dark);
    TEST_ASSERT_TRUE(source(false, DYNAMIC, false, false) == Source::Dark);
}

void test_preview_and_identify_work_with_power_off() {
    TEST_ASSERT_TRUE(source(false, DYNAMIC, false, true) == Source::Preview);
    TEST_ASSERT_TRUE(source(false, PLAIN, true, false) == Source::Identify);
}

void test_identify_takes_priority_over_preview() {
    TEST_ASSERT_TRUE(source(true, DYNAMIC, true, true) == Source::Identify);
    TEST_ASSERT_TRUE(source(true, PLAIN, false, true) == Source::Preview);
}

void test_frames_are_applied_only_when_on_dynamic_and_not_previewing() {
    TEST_ASSERT_TRUE(acceptsFrames(true, DYNAMIC, false));
    TEST_ASSERT_FALSE(acceptsFrames(false, DYNAMIC, false));   // lighting off
    TEST_ASSERT_FALSE(acceptsFrames(true, PLAIN, false));      // plain mode
    TEST_ASSERT_FALSE(acceptsFrames(true, DYNAMIC, true));     // calibration preview
}

// An identify blink keeps the strip: no other redraw (LED count, repaint after a mode,
// power or color change, preview) draws over it, whatever the rest of the state is.
void test_redraws_wait_for_a_running_identify_blink() {
    const bool    flags[] = {false, true};
    const uint8_t modes[] = {PLAIN, DYNAMIC};
    for (bool power : flags)
        for (uint8_t mode : modes)
            for (bool preview : flags) {
                TEST_ASSERT_FALSE(redrawsNow(source(power, mode, true, preview)));
                TEST_ASSERT_TRUE(redrawsNow(source(power, mode, false, preview)));
            }
}

void test_identify_uses_the_protocol_blink_count_and_duration() {
    TEST_ASSERT_EQUAL_UINT8(protocol::ESP32_IDENTIFY_BLINKS, Id::BLINKS);
    TEST_ASSERT_EQUAL_UINT32(protocol::ESP32_IDENTIFY_DURATION_MS, Id::DURATION_MS);
}

void test_identify_starts_lit_and_alternates() {
    TEST_ASSERT_TRUE(Id::phase(0) == Id::Phase::On);
    const uint32_t slot = Id::DURATION_MS / (2u * Id::BLINKS);   // ≈ length of one on or off
    TEST_ASSERT_TRUE(Id::phase(slot / 2) == Id::Phase::On);
    TEST_ASSERT_TRUE(Id::phase(slot + slot / 2) == Id::Phase::Off);
    TEST_ASSERT_TRUE(Id::phase(2 * slot + slot / 2) == Id::Phase::On);
}

// BLINKS blinks of equal length, ending after a dark slot exactly at DURATION_MS.
void test_identify_blinks_the_protocol_count_over_exactly_the_duration() {
    int blinks = 0;
    Id::Phase prev = Id::Phase::Off;
    uint32_t runStart = 0, shortest = UINT32_MAX, longest = 0;
    uint32_t t = 0;
    for (; Id::phase(t) != Id::Phase::Done; t++) {
        Id::Phase p = Id::phase(t);
        if (t > 0 && p != prev) {
            uint32_t run = t - runStart;
            if (run < shortest) shortest = run;
            if (run > longest)  longest = run;
            runStart = t;
        }
        if (p == Id::Phase::On && prev != Id::Phase::On) blinks++;
        prev = p;
    }
    TEST_ASSERT_EQUAL_INT(Id::BLINKS, blinks);
    TEST_ASSERT_TRUE(prev == Id::Phase::Off);   // restored output is visibly separate
    TEST_ASSERT_EQUAL_UINT32(Id::DURATION_MS, t);
    TEST_ASSERT_TRUE(Id::phase(Id::DURATION_MS - 1) == Id::Phase::Off);
    TEST_ASSERT_TRUE(longest - shortest <= 1);    // slots differ by integer rounding only
    TEST_ASSERT_TRUE(Id::phase(10 * Id::DURATION_MS) == Id::Phase::Done);
}

void test_identify_covers_every_led_that_may_be_lit() {
    TEST_ASSERT_EQUAL_UINT16(300, Id::coverage(300, 0, 32, 1500));     // the driven strip
    TEST_ASSERT_EQUAL_UINT16(500, Id::coverage(300, 500, 32, 1500));   // a longer preview's LEDs
    TEST_ASSERT_EQUAL_UINT16(400, Id::coverage(32, 0, 400, 1500));     // the calibrated layout
    TEST_ASSERT_EQUAL_UINT16(1500, Id::coverage(1500, 0, 1020, 1500));
    TEST_ASSERT_EQUAL_UINT16(1000, Id::coverage(1200, 1300, 0, 1000)); // capped at the buffer
}

void test_identify_draws_only_on_phase_changes() {
    TEST_ASSERT_TRUE(Id::step(0, Id::Phase::Done) == Id::Step::Draw);   // first frame
    TEST_ASSERT_TRUE(Id::step(1, Id::Phase::On) == Id::Step::Keep);
    uint32_t firstOff = 1;
    while (Id::phase(firstOff) == Id::Phase::On) firstOff++;
    TEST_ASSERT_TRUE(Id::step(firstOff - 1, Id::Phase::On) == Id::Step::Keep);
    TEST_ASSERT_TRUE(Id::step(firstOff, Id::Phase::On) == Id::Step::Draw);
    TEST_ASSERT_TRUE(Id::step(firstOff, Id::Phase::Off) == Id::Step::Keep);
}

// Another redraw request during the blink resets the drawn phase to Done: the blink
// re-applies its current phase on the next update instead of leaving the strip to it.
void test_identify_reapplies_its_phase_after_another_redraw() {
    uint32_t t = 10;
    TEST_ASSERT_TRUE(Id::step(t, Id::Phase::On) == Id::Step::Keep);
    TEST_ASSERT_TRUE(Id::step(t, Id::Phase::Done) == Id::Step::Draw);
    TEST_ASSERT_TRUE(Id::phase(t) == Id::Phase::On);
}

void test_identify_finishes_at_the_duration() {
    TEST_ASSERT_TRUE(Id::step(Id::DURATION_MS - 1, Id::Phase::Off) == Id::Step::Keep);
    TEST_ASSERT_TRUE(Id::step(Id::DURATION_MS, Id::Phase::Off) == Id::Step::Finish);
    TEST_ASSERT_TRUE(Id::step(Id::DURATION_MS, Id::Phase::Done) == Id::Step::Finish);
    TEST_ASSERT_TRUE(Id::step(Id::DURATION_MS + 500, Id::Phase::On) == Id::Step::Finish);
}

int main() {
    UNITY_BEGIN();
    RUN_TEST(test_power_on_follows_the_mode);
    RUN_TEST(test_power_off_is_dark_in_both_modes);
    RUN_TEST(test_preview_and_identify_work_with_power_off);
    RUN_TEST(test_identify_takes_priority_over_preview);
    RUN_TEST(test_frames_are_applied_only_when_on_dynamic_and_not_previewing);
    RUN_TEST(test_redraws_wait_for_a_running_identify_blink);
    RUN_TEST(test_identify_uses_the_protocol_blink_count_and_duration);
    RUN_TEST(test_identify_starts_lit_and_alternates);
    RUN_TEST(test_identify_blinks_the_protocol_count_over_exactly_the_duration);
    RUN_TEST(test_identify_covers_every_led_that_may_be_lit);
    RUN_TEST(test_identify_draws_only_on_phase_changes);
    RUN_TEST(test_identify_reapplies_its_phase_after_another_redraw);
    RUN_TEST(test_identify_finishes_at_the_duration);
    return UNITY_END();
}
