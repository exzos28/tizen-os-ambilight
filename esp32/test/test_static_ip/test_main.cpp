// Host tests for StaticIp.h:  pio test -e native
#include <unity.h>
#include "StaticIp.h"

using namespace StaticIp;

static const uint32_t MASK24 = pack(255, 255, 255, 0);

void setUp() {}
void tearDown() {}

void test_typical_home_network() {
    uint32_t out = 0;
    TEST_ASSERT_NULL(preferredAddress(pack(192, 168, 1, 1), MASK24, 245, out));
    TEST_ASSERT_EQUAL_HEX32(pack(192, 168, 1, 245), out);
}

void test_octet_equal_to_router_is_rejected() {
    // ISP boxes often sit at .254.
    uint32_t out = 0;
    TEST_ASSERT_NOT_NULL(preferredAddress(pack(192, 168, 1, 254), MASK24, 254, out));
}

void test_address_outside_a_small_subnet_is_rejected() {
    // /25 with the router at .1: valid hosts are .1–.126, so .245 is outside.
    uint32_t out = 0;
    TEST_ASSERT_NOT_NULL(preferredAddress(pack(192, 168, 1, 1), pack(255, 255, 255, 128), 245, out));
    TEST_ASSERT_NULL(preferredAddress(pack(192, 168, 1, 1), pack(255, 255, 255, 128), 100, out));
}

void test_network_and_broadcast_addresses_are_rejected() {
    uint32_t out = 0;
    TEST_ASSERT_NOT_NULL(preferredAddress(pack(192, 168, 1, 1), MASK24, 0, out));
    TEST_ASSERT_NOT_NULL(preferredAddress(pack(192, 168, 1, 1), MASK24, 255, out));
    // /25 upper half: .128 is the network address, .255 the broadcast.
    TEST_ASSERT_NOT_NULL(preferredAddress(pack(192, 168, 1, 129), pack(255, 255, 255, 128), 128, out));
}

void test_larger_subnet_keeps_the_gateways_third_octet() {
    uint32_t out = 0;
    TEST_ASSERT_NULL(preferredAddress(pack(10, 0, 5, 1), pack(255, 255, 0, 0), 245, out));
    TEST_ASSERT_EQUAL_HEX32(pack(10, 0, 5, 245), out);
}

void test_missing_dhcp_info_is_rejected() {
    uint32_t out = 0;
    TEST_ASSERT_NOT_NULL(preferredAddress(0, MASK24, 245, out));
    TEST_ASSERT_NOT_NULL(preferredAddress(pack(192, 168, 1, 1), 0, 245, out));
}

int main() {
    UNITY_BEGIN();
    RUN_TEST(test_typical_home_network);
    RUN_TEST(test_octet_equal_to_router_is_rejected);
    RUN_TEST(test_address_outside_a_small_subnet_is_rejected);
    RUN_TEST(test_network_and_broadcast_addresses_are_rejected);
    RUN_TEST(test_larger_subnet_keeps_the_gateways_third_octet);
    RUN_TEST(test_missing_dhcp_info_is_rejected);
    return UNITY_END();
}
