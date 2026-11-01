// Host tests for LightOutput.h:  pio test -e native
#include <unity.h>
#include "LightOutput.h"

using namespace LightOutput;
namespace Id = LightOutput::Identify;

void setUp() {}
void tearDown() {}

void test_power_on_follows_the_mode() {
    TEST_ASSERT_TRUE(source(true, 0, false, false) == Source::Plain);
    TEST_ASSERT_TRUE(source(true, 1, false, false) == Source::Dynamic);
}

void test_power_off_is_dark_in_both_modes() {
    TEST_ASSERT_TRUE(source(false, 0, false, false) == Source::Dark);
    TEST_ASSERT_TRUE(source(false, 1, false, false) == Source::Dark);
}

void test_preview_and_identify_work_with_power_off() {
    TEST_ASSERT_TRUE(source(false, 1, false, true) == Source::Preview);
    TEST_ASSERT_TRUE(source(false, 0, true, false) == Source::Identify);
}

void test_identify_takes_priority_over_preview() {
    TEST_ASSERT_TRUE(source(true, 1, true, true) == Source::Identify);
    TEST_ASSERT_TRUE(source(true, 0, false, true) == Source::Preview);
}

void test_frames_are_applied_only_when_on_dynamic_and_not_previewing() {
    TEST_ASSERT_TRUE(acceptsFrames(true, 1, false));
    TEST_ASSERT_FALSE(acceptsFrames(false, 1, false));   // lighting off
    TEST_ASSERT_FALSE(acceptsFrames(true, 0, false));    // plain mode
    TEST_ASSERT_FALSE(acceptsFrames(true, 1, true));     // calibration preview
}

void test_identify_starts_lit_and_alternates() {
    TEST_ASSERT_TRUE(Id::phase(0) == Id::Phase::On);
    TEST_ASSERT_TRUE(Id::phase(Id::ON_MS - 1) == Id::Phase::On);
    TEST_ASSERT_TRUE(Id::phase(Id::ON_MS) == Id::Phase::Off);
    TEST_ASSERT_TRUE(Id::phase(Id::PERIOD_MS - 1) == Id::Phase::Off);
    TEST_ASSERT_TRUE(Id::phase(Id::PERIOD_MS) == Id::Phase::On);
}

void test_identify_blinks_three_times_in_about_two_seconds() {
    int blinks = 0;
    Id::Phase prev = Id::Phase::Off;
    uint32_t t = 0;
    for (; Id::phase(t) != Id::Phase::Done; t++) {
        Id::Phase p = Id::phase(t);
        if (p == Id::Phase::On && prev != Id::Phase::On) blinks++;
        prev = p;
    }
    TEST_ASSERT_EQUAL_INT(3, blinks);
    // Ends after the last dark gap, so the restored output is visibly separate.
    TEST_ASSERT_TRUE(prev == Id::Phase::Off);
    TEST_ASSERT_UINT32_WITHIN(100, 2000, t);
    TEST_ASSERT_TRUE(Id::phase(10000) == Id::Phase::Done);
}

int main() {
    UNITY_BEGIN();
    RUN_TEST(test_power_on_follows_the_mode);
    RUN_TEST(test_power_off_is_dark_in_both_modes);
    RUN_TEST(test_preview_and_identify_work_with_power_off);
    RUN_TEST(test_identify_takes_priority_over_preview);
    RUN_TEST(test_frames_are_applied_only_when_on_dynamic_and_not_previewing);
    RUN_TEST(test_identify_starts_lit_and_alternates);
    RUN_TEST(test_identify_blinks_three_times_in_about_two_seconds);
    return UNITY_END();
}
