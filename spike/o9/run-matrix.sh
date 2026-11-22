#!/usr/bin/env bash
# Publishes every (variant, mode) of the WebAssembly spike and logs the output, so sizes and trim warnings can be read back.
# usage: run-matrix.sh "<variants>" "<modes>"
set -u
cd "$(dirname "$0")/wasm"
for mode in $2; do
  for variant in $1; do
    name="$variant-$mode"
    echo "== $name $(date +%T)"
    start=$(date +%s)
    dotnet publish -c Release -p:Variant="$variant" -p:Mode="$mode" -o "../publish/$name" > "../results/publish-$name.log" 2>&1
    echo "   exit=$? seconds=$(( $(date +%s) - start ))"
  done
done
