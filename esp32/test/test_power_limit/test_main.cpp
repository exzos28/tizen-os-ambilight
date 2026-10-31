// Host tests for PowerLimit.h:  pio test -e native
#include <unity.h>
#include "PowerLimit.h"

using PowerLimit::apply;

void setUp() {}
void tearDown() {}

void test_full_white_power_scales_with_pixels_and_brightness() {
    // 100 pixels full white (765 each), 0.72 W/pixel, brightness 255 → 72 W, no limit.
    PowerLimit::Result r = apply(100u * 765u, 255, 0.72f, 0);
    TEST_ASSERT_FLOAT_WITHIN(0.01f, 72.0f, r.watts);
    TEST_ASSERT_EQUAL_UINT8(255, r.brightness);
    TEST_ASSERT_FALSE(r.limited);

    r = apply(100u * 765u, 128, 0.72f, 0);
    TEST_ASSERT_FLOAT_WITHIN(0.1f, 36.1f, r.watts);
}

void test_frame_under_the_limit_is_untouched() {
    PowerLimit::Result r = apply(100u * 765u, 255, 0.72f, 100);
    TEST_ASSERT_EQUAL_UINT8(255, r.brightness);
    TEST_ASSERT_FALSE(r.limited);
}

void test_frame_over_the_limit_is_scaled_down_to_fit() {
    // 72 W requested, 36 W allowed → about half brightness, never above the limit.
    PowerLimit::Result r = apply(100u * 765u, 255, 0.72f, 36);
    TEST_ASSERT_TRUE(r.limited);
    TEST_ASSERT_UINT8_WITHIN(1, 127, r.brightness);
    TEST_ASSERT_TRUE(r.watts <= 36.0f);
    TEST_ASSERT_TRUE(r.watts > 35.0f);
}

void test_dark_frame_draws_nothing() {
    PowerLimit::Result r = apply(0, 255, 0.72f, 10);
    TEST_ASSERT_EQUAL_FLOAT(0.0f, r.watts);
    TEST_ASSERT_FALSE(r.limited);
}

void test_colour_counts_by_channel() {
    // Pure red on 100 pixels = one third of full white.
    PowerLimit::Result r = apply(100u * 255u, 255, 0.72f, 0);
    TEST_ASSERT_FLOAT_WITHIN(0.01f, 24.0f, r.watts);
}

int main() {
    UNITY_BEGIN();
    RUN_TEST(test_full_white_power_scales_with_pixels_and_brightness);
    RUN_TEST(test_frame_under_the_limit_is_untouched);
    RUN_TEST(test_frame_over_the_limit_is_scaled_down_to_fit);
    RUN_TEST(test_dark_frame_draws_nothing);
    RUN_TEST(test_colour_counts_by_channel);
    return UNITY_END();
}
