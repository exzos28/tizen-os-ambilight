# Apps2Samsung catalog entry

`exzos28__tizen-os-ambilight.json` is the entry for [Apps2Samsung/tizen-community-packages](https://github.com/Apps2Samsung/tizen-community-packages) (copy it to `packages/` there in a PR; schema: `packages.schema.json` in that repo).

It points at the `ambilight-tv-v<version>.wgt` asset that `.github/workflows/tv-package.yml` attaches to every release. If you rename that file, update `match` here and in the catalog.
