#!/bin/bash

set -e

echo "▶️ Running in-cluster DNS test..."

kubectl run dns-test --rm -it \
  --image=busybox \
  --restart=Never \
  --namespace=task4 \
  -- wget -qO- http://task4/ping && echo "✅ Success" || echo "❌ Failed"