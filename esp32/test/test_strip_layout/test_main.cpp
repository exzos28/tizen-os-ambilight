// Host tests for StripLayout.h:  pio test -e native
#include <unity.h>
#include <stdio.h>
#include <string.h>
#include "StripLayout.h"

using namespace StripLayout;

static const uint8_t COUNTS[4] = {3, 2, 3, 2};   // top, right, bottom, left
static const char* NAMES = "TRBL";

// Renders the walk as "T0 T1 R0 …" for readable assertions.
static void walk(uint8_t corner, bool cw, const uint8_t counts[4], uint16_t maxLeds, char* out) {
    out[0] = '\0';
    forEach(corner, cw, counts, maxLeds, [&](uint16_t idx, uint8_t side, uint8_t i) {
        char buf[8];
        snprintf(buf, sizeof(buf), "%s%c%u", idx ? " " : "", NAMES[side], i);
        strcat(out, buf);
    });
}

void setUp() {}
void tearDown() {}

void test_clockwise_from_top_left_follows_natural_order() {
    char s[128];
    walk(0, true, COUNTS, 100, s);
    TEST_ASSERT_EQUAL_STRING("T0 T1 T2 R0 R1 B0 B1 B2 L0 L1", s);
}

void test_clockwise_from_bottom_right_starts_with_bottom() {
    char s[128];
    walk(2, true, COUNTS, 100, s);
    TEST_ASSERT_EQUAL_STRING("B0 B1 B2 L0 L1 T0 T1 T2 R0 R1", s);
}

void test_counter_clockwise_from_top_left_goes_down_the_left_side() {
    // From the top-left corner going CCW: down the left side (LEFT is natural bottom→top,
    // so reversed), along the bottom left→right (BOTTOM is natural right→left), …
    char s[128];
    walk(0, false, COUNTS, 100, s);
    TEST_ASSERT_EQUAL_STRING("L1 L0 B2 B1 B0 R1 R0 T2 T1 T0", s);
}

void test_counter_clockwise_from_top_right_starts_with_top_reversed() {
    char s[128];
    walk(1, false, COUNTS, 100, s);
    TEST_ASSERT_EQUAL_STRING("T2 T1 T0 L1 L0 B2 B1 B0 R1 R0", s);
}

void test_walk_stops_at_max_leds() {
    char s[128];
    TEST_ASSERT_EQUAL_UINT16(4, forEach(0, true, COUNTS, 4, [](uint16_t, uint8_t, uint8_t) {}));
    walk(0, true, COUNTS, 4, s);
    TEST_ASSERT_EQUAL_STRING("T0 T1 T2 R0", s);
}

void test_every_led_is_visited_exactly_once_for_all_corners_and_directions() {
    const uint8_t counts[4] = {7, 4, 6, 5};
    for (uint8_t corner = 0; corner < 4; corner++) {
        for (int cw = 0; cw < 2; cw++) {
            uint8_t seen[4][8] = {};
            uint16_t expectedIdx = 0;
            bool ordered = true;
            uint16_t n = forEach(corner, cw == 1, counts, 1000, [&](uint16_t idx, uint8_t side, uint8_t i) {
                if (idx != expectedIdx++) ordered = false;
                seen[side][i]++;
            });
            TEST_ASSERT_EQUAL_UINT16(22, n);
            TEST_ASSERT_TRUE(ordered);
            for (uint8_t side = 0; side < 4; side++)
                for (uint8_t i = 0; i < counts[side]; i++)
                    TEST_ASSERT_EQUAL_UINT8_MESSAGE(1, seen[side][i], "LED visited more or less than once");
        }
    }
}

void test_empty_sides_are_skipped() {
    const uint8_t counts[4] = {2, 0, 2, 0};
    char s[64];
    walk(0, true, counts, 100, s);
    TEST_ASSERT_EQUAL_STRING("T0 T1 B0 B1", s);
}

void test_interp_first_and_last_led_sit_on_first_and_last_sample() {
    InterpPos first = interpPosition(0, 10, 4);
    InterpPos last = interpPosition(9, 10, 4);
    TEST_ASSERT_EQUAL_UINT8(0, first.lo);
    TEST_ASSERT_EQUAL_UINT8(0, first.frac);
    TEST_ASSERT_EQUAL_UINT8(3, last.lo);
    TEST_ASSERT_EQUAL_UINT8(3, last.hi);
}

void test_interp_midpoint_blends_neighbours() {
    // 7 LEDs over 4 samples: LED 3 sits at t = 1.5 → between samples 1 and 2, half way.
    InterpPos p = interpPosition(3, 7, 4);
    TEST_ASSERT_EQUAL_UINT8(1, p.lo);
    TEST_ASSERT_EQUAL_UINT8(2, p.hi);
    TEST_ASSERT_UINT8_WITHIN(1, 127, p.frac);
}

void test_interp_single_led_takes_the_middle_of_the_edge() {
    // Used to divide 0 by 0 (dstCount - 1 == 0).
    InterpPos p = interpPosition(0, 1, 4);
    TEST_ASSERT_EQUAL_UINT8(1, p.lo);
    TEST_ASSERT_EQUAL_UINT8(2, p.hi);
    TEST_ASSERT_UINT8_WITHIN(1, 127, p.frac);
}

void test_interp_single_sample_is_constant() {
    InterpPos p = interpPosition(5, 10, 1);
    TEST_ASSERT_EQUAL_UINT8(0, p.lo);
    TEST_ASSERT_EQUAL_UINT8(0, p.hi);
}

int main() {
    UNITY_BEGIN();
    RUN_TEST(test_clockwise_from_top_left_follows_natural_order);
    RUN_TEST(test_clockwise_from_bottom_right_starts_with_bottom);
    RUN_TEST(test_counter_clockwise_from_top_left_goes_down_the_left_side);
    RUN_TEST(test_counter_clockwise_from_top_right_starts_with_top_reversed);
    RUN_TEST(test_walk_stops_at_max_leds);
    RUN_TEST(test_every_led_is_visited_exactly_once_for_all_corners_and_directions);
    RUN_TEST(test_empty_sides_are_skipped);
    RUN_TEST(test_interp_first_and_last_led_sit_on_first_and_last_sample);
    RUN_TEST(test_interp_midpoint_blends_neighbours);
    RUN_TEST(test_interp_single_led_takes_the_middle_of_the_edge);
    RUN_TEST(test_interp_single_sample_is_constant);
    return UNITY_END();
}
