#!/usr/bin/env bash
# setup-hve-core.sh — install the GitHub Copilot CLI + hve-core-all plugin.
#
# Shared by the dev container (postCreateCommand) and the "Copilot Setup
# Steps" workflow. Best-effort, non-fatal, and idempotent by design: the
# cloud sandbox / dev container must still start even if any step here fails,
# so every fallible command degrades to a warning instead of aborting.
set -euo pipefail

echo "==> Setting up GitHub Copilot CLI + hve-core-all plugin..."

# 1. Install the Copilot CLI only if it is not already available (idempotent).
if command -v copilot >/dev/null 2>&1; then
  echo "  ✓ copilot CLI already installed ($(command -v copilot))"
else
  npm install -g @github/copilot || echo "::warning::copilot CLI install failed"
fi

# 2. Register the hve-core marketplace and install the plugin (non-fatal).
copilot plugin marketplace add microsoft/hve-core || echo "::warning::marketplace add failed"
copilot plugin install hve-core-all@hve-core       || echo "::warning::hve-core-all install failed"

# 3. List installed plugins for verification (never fail the script on this).
copilot plugin list || true

echo "==> hve-core-all setup step complete."
