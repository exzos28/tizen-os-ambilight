"""
Pre-build script for PlatformIO: passes the project version to the firmware.

The version lives in the repository's root package.json and is bumped by changesets
(see .changeset/README.md), so a firmware built from a release tag reports that release.
Available in C++ as the string macro AMBILIGHT_VERSION.
"""

Import('env')  # noqa: F821 — injected by PlatformIO

import json
import os

package_json = os.path.join(env['PROJECT_DIR'], '..', 'package.json')  # noqa: F821
try:
    with open(package_json, encoding='utf-8') as f:
        version = json.load(f)['version']
except (OSError, KeyError, ValueError) as e:
    print(f'[version] could not read {package_json} ({e}) — using "unknown"')
    version = 'unknown'

env.Append(CPPDEFINES=[('AMBILIGHT_VERSION', env.StringifyMacro(version))])  # noqa: F821
print(f'[version] AMBILIGHT_VERSION = {version}')
