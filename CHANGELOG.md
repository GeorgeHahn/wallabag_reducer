# Unreleased

- Removed `DATABASE_FILE` env var
- Added `DATA_DIR` env var to set database & config directory
- Added processor configuration file (`$DATA_DIR/config.json`)
- Converted `GoodreadsProcessor` to generic website to tag mapper
- Refactored core to enable further extensibility
