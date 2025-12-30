#!/bin/bash

set -e

echo "▶️ Checking booking-service deployment..."
kubectl get pods -l app=task4 --namespace task4

echo
echo "▶️ Checking service..."
kubectl get svc task4 --namespace task4 || echo "(No service found)"

echo
echo "▶️ Helm release:"
helm list -n task4 | grep task4 || echo "(No release found)"

echo
echo "▶️ Port-forward to test service locally:"
echo "  kubectl port-forward svc/booking-service 8080:80"
echo "  Then in another terminal:"
echo "    curl http://localhost:8080/ping"

echo
echo "▶️ Quick curl (if port-forward already running):"
curl --fail http://localhost:8080/ping && echo "✅ Reachable" || echo "❌ Not responding"
