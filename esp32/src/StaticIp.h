#pragma once
// Validation of the "preferred last octet" static address, free of Arduino deps for host
// tests (pio test -e native, see test/test_static_ip). Addresses are host-order uint32
// (a.b.c.d → a<<24 | b<<16 | c<<8 | d).
#include <stdint.h>

namespace StaticIp {

inline uint32_t pack(uint8_t a, uint8_t b, uint8_t c, uint8_t d) {
    return ((uint32_t)a << 24) | ((uint32_t)b << 16) | ((uint32_t)c << 8) | d;
}

// Builds <gateway's first three octets>.<octet> and checks it is usable on the network the
// router gave us. Returns nullptr and sets `out` if it is; otherwise returns the reason and
// the caller should keep the DHCP address.
inline const char* preferredAddress(uint32_t gateway, uint32_t mask, uint8_t octet, uint32_t& out) {
    uint32_t candidate = (gateway & 0xFFFFFF00u) | octet;
    if (mask == 0 || gateway == 0)                    return "no gateway/subnet from DHCP";
    if ((candidate & mask) != (gateway & mask))       return "outside the router's subnet";
    if (candidate == gateway)                         return "same as the router's address";
    uint32_t host = candidate & ~mask;
    if (host == 0)                                    return "is the subnet's network address";
    if (host == (~mask))                              return "is the subnet's broadcast address";
    out = candidate;
    return nullptr;
}

}  // namespace StaticIp
