// Host tests for ColorPipeline.h:  pio test -e native
#include <unity.h>
#include "ColorPipeline.h"

using namespace ColorPipeline;

void setUp() {}
void tearDown() {}

static void assertRgb(Rgb want, Rgb got) {
    TEST_ASSERT_EQUAL_UINT8(want.r, got.r);
    TEST_ASSERT_EQUAL_UINT8(want.g, got.g);
    TEST_ASSERT_EQUAL_UINT8(want.b, got.b);
}

static int spread(Rgb c) {   // max − min channel: 0 for grey
    int mx = c.r > c.g ? c.r : c.g, mn = c.r < c.g ? c.r : c.g;
    if (c.b > mx) mx = c.b;
    if (c.b < mn) mn = c.b;
    return mx - mn;
}

// ---------------------------------------------------------------- luma, saturation

void test_luma_is_rec709() {
    TEST_ASSERT_EQUAL_UINT8(0, luma(0, 0, 0));
    TEST_ASSERT_EQUAL_UINT8(255, luma(255, 255, 255));
    TEST_ASSERT_EQUAL_UINT8(54, luma(255, 0, 0));     // 0.2126
    TEST_ASSERT_EQUAL_UINT8(182, luma(0, 255, 0));    // 0.7152
    TEST_ASSERT_EQUAL_UINT8(19, luma(0, 0, 255));     // 0.0722
    TEST_ASSERT_EQUAL_UINT8(100, luma(100, 100, 100));
}

void test_saturation_one_is_exactly_the_identity() {
    TEST_ASSERT_EQUAL_UINT16(256, saturationQ8(1.0f));
    for (int r = 0; r < 256; r += 15)
        for (int g = 0; g < 256; g += 17)
            for (int b = 0; b < 256; b += 13) {
                Rgb c{(uint8_t)r, (uint8_t)g, (uint8_t)b};
                assertRgb(c, saturate(c, 256));
            }
}

void test_saturation_zero_is_the_grey_of_the_same_luma() {
    Rgb c{200, 40, 90};
    Rgb g = saturate(c, saturationQ8(0.0f));
    TEST_ASSERT_EQUAL_INT(0, spread(g));
    TEST_ASSERT_EQUAL_UINT8(luma(c), g.r);
}

void test_saturation_keeps_greys_and_scales_around_luma() {
    assertRgb({80, 80, 80}, saturate({80, 80, 80}, saturationQ8(2.0f)));
    Rgb c{120, 100, 80};
    Rgb more = saturate(c, saturationQ8(1.5f));
    Rgb less = saturate(c, saturationQ8(0.5f));
    TEST_ASSERT_TRUE(spread(more) > spread(c));
    TEST_ASSERT_TRUE(spread(less) < spread(c));
    // Hue order and luma stay (within rounding).
    TEST_ASSERT_TRUE(more.r > more.g && more.g > more.b);
    TEST_ASSERT_INT_WITHIN(1, luma(c), luma(more));
}

void test_saturation_clamps_to_the_channel_range() {
    Rgb c = saturate({250, 10, 10}, saturationQ8(2.0f));
    TEST_ASSERT_EQUAL_UINT8(255, c.r);
    TEST_ASSERT_EQUAL_UINT8(0, c.g);
    TEST_ASSERT_EQUAL_UINT8(0, c.b);
    TEST_ASSERT_EQUAL_UINT16(0, saturationQ8(-1.0f));
}

// ---------------------------------------------------------------- interpolation

void test_lerp_hits_both_ends_and_the_middle() {
    assertRgb({10, 200, 0}, lerp({10, 200, 0}, {250, 0, 100}, 0));
    assertRgb({250, 0, 100}, lerp({10, 200, 0}, {250, 0, 100}, 255));
    assertRgb({130, 100, 50}, lerp({10, 200, 0}, {250, 0, 100}, 128));
}

// Between two dark, nearly grey samples whose hues are far apart the LEDs stay dark and
// nearly grey: no hue is invented (HSV interpolation went around the wheel here).
void test_interpolation_between_dark_greys_invents_no_color() {
    Rgb src[2] = {{12, 10, 10}, {10, 10, 12}};   // reddish and bluish near-black
    Rgb dst[9];
    interpolateSide(src, 2, dst, 9);
    for (const Rgb& c : dst) {
        TEST_ASSERT_TRUE(spread(c) <= 2);
        TEST_ASSERT_TRUE(c.r <= 12 && c.g <= 10 && c.b <= 12);
    }
}

void test_interpolation_is_linear_in_rgb_and_ends_on_the_samples() {
    Rgb src[3] = {{255, 0, 0}, {0, 0, 255}, {0, 255, 0}};
    Rgb dst[5];
    interpolateSide(src, 3, dst, 5);
    assertRgb({255, 0, 0}, dst[0]);
    assertRgb({128, 0, 127}, dst[1]);   // halfway red → blue, through purple, not green
    assertRgb({0, 0, 255}, dst[2]);
    assertRgb({0, 255, 0}, dst[4]);
}

void test_interpolation_of_a_side_without_samples_is_dark() {
    Rgb dst[3] = {{9, 9, 9}, {9, 9, 9}, {9, 9, 9}};
    interpolateSide(static_cast<const Rgb*>(nullptr), 0, dst, 3);
    for (const Rgb& c : dst) assertRgb({0, 0, 0}, c);
}

// ---------------------------------------------------------------- 1-2-1 ring smoothing

void test_ring_smoothing_is_1_2_1_with_neighbours() {
    Rgb in[4] = {{0, 0, 0}, {100, 0, 0}, {0, 0, 0}, {0, 0, 0}};
    Rgb out[4];
    ringSmooth(in, out, 4);
    TEST_ASSERT_EQUAL_UINT8(25, out[0].r);
    TEST_ASSERT_EQUAL_UINT8(50, out[1].r);
    TEST_ASSERT_EQUAL_UINT8(25, out[2].r);
    TEST_ASSERT_EQUAL_UINT8(0, out[3].r);
}

void test_ring_smoothing_wraps_from_the_last_sample_to_the_first() {
    // 14 samples like the TV sends (4 + 3 + 4 + 3): the first (top-left of the top edge)
    // and the last (top of the left edge) are neighbours at the top-left corner.
    Rgb in[14] = {};
    in[13] = {0, 200, 0};
    Rgb out[14];
    ringSmooth(in, out, 14);
    TEST_ASSERT_EQUAL_UINT8(50, out[0].g);
    TEST_ASSERT_EQUAL_UINT8(100, out[13].g);
    TEST_ASSERT_EQUAL_UINT8(50, out[12].g);
    TEST_ASSERT_EQUAL_UINT8(0, out[1].g);
}

void test_ring_smoothing_keeps_a_flat_ring_and_short_rings() {
    Rgb flat[5] = {{30, 60, 90}, {30, 60, 90}, {30, 60, 90}, {30, 60, 90}, {30, 60, 90}};
    Rgb out[5];
    ringSmooth(flat, out, 5);
    for (const Rgb& c : out) assertRgb({30, 60, 90}, c);
    Rgb two[2] = {{1, 2, 3}, {4, 5, 6}};
    ringSmooth(two, out, 2);
    assertRgb({1, 2, 3}, out[0]);
    assertRgb({4, 5, 6}, out[1]);
}

void test_average_luma() {
    Rgb s[2] = {{0, 0, 0}, {200, 200, 200}};
    TEST_ASSERT_EQUAL_UINT8(100, averageLuma(s, 2));
    TEST_ASSERT_EQUAL_UINT8(0, averageLuma(s, 0));
}

// ---------------------------------------------------------------- output LUT

void test_output_lut_is_white_balance_then_gamma() {
    uint8_t gamma[256];
    buildGammaTable(2.2f, gamma);
    TEST_ASSERT_EQUAL_UINT8(0, gamma[0]);
    TEST_ASSERT_EQUAL_UINT8(255, gamma[255]);
    TEST_ASSERT_EQUAL_UINT8(56, gamma[128]);   // round(255 · 0.502^2.2)
    OutputLut lut;
    buildOutputLut(gamma, 255, 128, 0, lut);
    assertRgb({56, 12, 0}, lut.apply({128, 128, 128}));   // g: gamma[128 · 128 / 255 = 64]
    TEST_ASSERT_EQUAL_UINT8(gamma[64], lut.g[128]);
    TEST_ASSERT_EQUAL_UINT8(255, lut.r[255]);
}

// ---------------------------------------------------------------- dark threshold

void test_dark_threshold_has_hysteresis() {
    TEST_ASSERT_FALSE(nextLit(false, DARK_ON_LUMA));       // needs more than ON to light
    TEST_ASSERT_TRUE(nextLit(false, DARK_ON_LUMA + 1));
    TEST_ASSERT_TRUE(nextLit(true, DARK_OFF_LUMA));         // stays lit down to OFF
    TEST_ASSERT_FALSE(nextLit(true, DARK_OFF_LUMA - 1));
    TEST_ASSERT_TRUE(nextLit(true, 8));                     // in the gap: keeps its state
    TEST_ASSERT_FALSE(nextLit(false, 8));
}

void test_dark_threshold_blackens_an_unlit_target_without_flicker() {
    LedState s{};
    // A value hovering 7–9 around the threshold never lights a dark LED…
    const uint8_t hover[] = {7, 9, 8, 9, 7};
    for (uint8_t y : hover) {
        Rgb t{30, 30, 30};
        darkThreshold(s, t, y);
        assertRgb({0, 0, 0}, t);
    }
    // …lights above ON, then stays lit while it hovers in the gap…
    Rgb t{40, 40, 40};
    darkThreshold(s, t, 11);
    assertRgb({40, 40, 40}, t);
    const uint8_t gap[] = {7, 9, 6, 8};
    for (uint8_t y : gap) {
        Rgb u{30, 30, 30};
        darkThreshold(s, u, y);
        assertRgb({30, 30, 30}, u);
    }
    // …and goes dark below OFF.
    Rgb u{20, 20, 20};
    darkThreshold(s, u, 5);
    assertRgb({0, 0, 0}, u);
    TEST_ASSERT_EQUAL_UINT8(0, s.flags & LED_LIT);
}

// ---------------------------------------------------------------- temporal smoothing

static LedState showing(Rgb c) {
    LedState s{};
    s.c[0] = (uint16_t)(c.r << 8);
    s.c[1] = (uint16_t)(c.g << 8);
    s.c[2] = (uint16_t)(c.b << 8);
    return s;
}

// Steps one LED for totalMs in 16 ms updates.
static void run(LedState& s, Rgb target, uint16_t smoothingMs, uint32_t totalMs, uint32_t dtMs = 16) {
    for (uint32_t t = 0; t < totalMs; t += dtMs) {
        Alphas a = alphas(dtMs, smoothingMs);
        step(s, target, a.slow, a.fast);
    }
}

void test_alpha_follows_the_time_constant() {
    TEST_ASSERT_EQUAL_UINT16(ALPHA_ONE, alphaQ12(16, 0));                 // no smoothing
    TEST_ASSERT_INT_WITHIN(2, 2589, alphaQ12(200, 200));                  // 1 − 1/e
    TEST_ASSERT_INT_WITHIN(2, 314, alphaQ12(16, 200));
    TEST_ASSERT_TRUE(alphaQ12(1, 1000) >= 1);
    TEST_ASSERT_EQUAL_UINT16(ALPHA_ONE, alphaQ12(100000, 50));
    Alphas a = alphas(16, 10);   // a setting below FAST_TAU_MS: fast is never slower
    TEST_ASSERT_TRUE(a.fast >= a.slow);
}

void test_approach_never_overshoots_and_settles_exactly() {
    uint16_t v = 0;
    for (int i = 0; i < 1000; i++) {
        uint16_t next = approach(v, 200, alphaQ12(16, 1000));
        TEST_ASSERT_TRUE(next >= v && next <= 200u << 8);
        v = next;
    }
    TEST_ASSERT_EQUAL_UINT16(200u << 8, v);
    v = 255u << 8;
    for (int i = 0; i < 1000; i++) v = approach(v, 0, alphaQ12(16, 1000));
    TEST_ASSERT_EQUAL_UINT16(0, v);
}

// Time based: the same time constant reaches the same point whatever the update rate.
void test_smoothing_converges_in_real_time() {
    LedState a = showing({0, 0, 0}), b = showing({0, 0, 0});
    run(a, {100, 100, 100}, 200, 192, 16);
    run(b, {100, 100, 100}, 200, 192, 32);
    TEST_ASSERT_INT_WITHIN(2, shown(a).r, shown(b).r);
    // After one time constant ≈ 63 %, after five ≈ there.
    LedState c = showing({0, 0, 0});
    run(c, {100, 100, 100}, 200, 208);
    TEST_ASSERT_INT_WITHIN(5, 64, shown(c).r);
    run(c, {100, 100, 100}, 200, 1000);
    TEST_ASSERT_INT_WITHIN(1, 100, shown(c).r);
}

void test_zero_smoothing_jumps() {
    LedState s = showing({10, 20, 30});
    onFrame(s, {11, 21, 31}, false);
    run(s, {11, 21, 31}, 0, 16);
    assertRgb({11, 21, 31}, shown(s));
}

void test_small_change_takes_the_slow_path() {
    LedState s = showing({100, 100, 100});
    onFrame(s, {120, 120, 120}, false);
    TEST_ASSERT_EQUAL_UINT8(0, s.flags & LED_FAST);
    run(s, {120, 120, 120}, 400, 48);
    TEST_ASSERT_TRUE(shown(s).r < 105);   // 3 updates into τ = 400 ms: barely moved
}

// The rainbow noise: same brightness, hue jumping between frames — slow, not followed.
void test_hue_only_change_stays_slow() {
    LedState s = showing({60, 50, 50});
    onFrame(s, {50, 52, 62}, false);
    TEST_ASSERT_EQUAL_UINT8(0, s.flags & LED_FAST);
}

void test_large_change_to_dark_goes_through_at_once() {
    LedState s = showing({220, 200, 180});
    onFrame(s, {0, 0, 0}, false);
    TEST_ASSERT_TRUE((s.flags & LED_FAST) != 0);
    run(s, {0, 0, 0}, 1000, 112);   // fast τ = 40 ms whatever the setting
    TEST_ASSERT_TRUE(luma(shown(s)) < 15);
}

void test_large_change_out_of_dark_appears_at_once() {
    LedState s = showing({0, 0, 0});
    onFrame(s, {200, 40, 40}, false);   // luma ≈ 79
    TEST_ASSERT_TRUE((s.flags & LED_FAST) != 0);
    run(s, {200, 40, 40}, 1000, 112);
    TEST_ASSERT_TRUE(shown(s).r > 185);
}

// A scene cut makes every LED fast, even one whose own change is small.
void test_scene_cut_uses_the_fast_constant_for_every_led() {
    TEST_ASSERT_TRUE(sceneCut(200, 40));
    TEST_ASSERT_TRUE(sceneCut(10, 120));
    TEST_ASSERT_FALSE(sceneCut(100, 100 + LARGE_CHANGE_LUMA));
    LedState s = showing({50, 50, 50});
    onFrame(s, {60, 60, 60}, true);
    TEST_ASSERT_TRUE((s.flags & LED_FAST) != 0);
    // The next frame without a cut is slow again.
    onFrame(s, {62, 62, 62}, false);
    TEST_ASSERT_EQUAL_UINT8(0, s.flags & LED_FAST);
}

void test_fast_flag_lasts_for_the_whole_transition() {
    LedState s = showing({0, 0, 0});
    onFrame(s, {255, 255, 255}, false);
    run(s, {255, 255, 255}, 400, 80);   // most of the way already: still fast until the next frame
    TEST_ASSERT_TRUE((s.flags & LED_FAST) != 0);
}

void test_clear_returns_to_black_and_unlit() {
    LedState s = showing({100, 100, 100});
    s.flags = LED_LIT | LED_FAST;
    clear(s);
    assertRgb({0, 0, 0}, shown(s));
    TEST_ASSERT_EQUAL_UINT8(0, s.flags);
}

int main() {
    UNITY_BEGIN();
    RUN_TEST(test_luma_is_rec709);
    RUN_TEST(test_saturation_one_is_exactly_the_identity);
    RUN_TEST(test_saturation_zero_is_the_grey_of_the_same_luma);
    RUN_TEST(test_saturation_keeps_greys_and_scales_around_luma);
    RUN_TEST(test_saturation_clamps_to_the_channel_range);
    RUN_TEST(test_lerp_hits_both_ends_and_the_middle);
    RUN_TEST(test_interpolation_between_dark_greys_invents_no_color);
    RUN_TEST(test_interpolation_is_linear_in_rgb_and_ends_on_the_samples);
    RUN_TEST(test_interpolation_of_a_side_without_samples_is_dark);
    RUN_TEST(test_ring_smoothing_is_1_2_1_with_neighbours);
    RUN_TEST(test_ring_smoothing_wraps_from_the_last_sample_to_the_first);
    RUN_TEST(test_ring_smoothing_keeps_a_flat_ring_and_short_rings);
    RUN_TEST(test_average_luma);
    RUN_TEST(test_output_lut_is_white_balance_then_gamma);
    RUN_TEST(test_dark_threshold_has_hysteresis);
    RUN_TEST(test_dark_threshold_blackens_an_unlit_target_without_flicker);
    RUN_TEST(test_alpha_follows_the_time_constant);
    RUN_TEST(test_approach_never_overshoots_and_settles_exactly);
    RUN_TEST(test_smoothing_converges_in_real_time);
    RUN_TEST(test_zero_smoothing_jumps);
    RUN_TEST(test_small_change_takes_the_slow_path);
    RUN_TEST(test_hue_only_change_stays_slow);
    RUN_TEST(test_large_change_to_dark_goes_through_at_once);
    RUN_TEST(test_large_change_out_of_dark_appears_at_once);
    RUN_TEST(test_scene_cut_uses_the_fast_constant_for_every_led);
    RUN_TEST(test_fast_flag_lasts_for_the_whole_transition);
    RUN_TEST(test_clear_returns_to_black_and_unlit);
    return UNITY_END();
}
