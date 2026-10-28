#!/usr/bin/env bash
set -euo pipefail

# Bruno v4 API Conformance Test Runner
# Executes the OpenCollection 1.0.0 test suite against a target API server.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COLLECTION_DIR="${SCRIPT_DIR}/Logistics.Backend.APITests"
REPORTS_DIR="${COLLECTION_DIR}/reports"

ENV_NAME="${BRUNO_ENV:-local}"
BASE_URL="${BASE_URL:-}"
TAGS="${TAGS:-}"
FOLDER="${FOLDER:-}"

usage() {
  cat <<EOF
Usage: $(basename "$0") [options] [-- <extra bru arguments>]

Options:
  -u, --base-url <url>   Override the baseUrl variable (e.g. http://localhost:5000/v1)
  -e, --env <name>       Environment file name in environments/ (default: local)
  -t, --tags <tags>      Comma-separated tags to include (e.g. Vehicles,ErrorContract)
  -f, --folder <folder>  Run only a specific folder (Vehicles, Drivers, or Routes)
  -r, --reports          Generate JUnit XML, JSON, and HTML test reports in reports/
  -h, --help             Show this help message
EOF
}

GENERATE_REPORTS=false
EXTRA_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    -u|--base-url)
      BASE_URL="$2"
      shift 2
      ;;
    -e|--env)
      ENV_NAME="$2"
      shift 2
      ;;
    -t|--tags)
      TAGS="$2"
      shift 2
      ;;
    -f|--folder)
      FOLDER="$2"
      shift 2
      ;;
    -r|--reports)
      GENERATE_REPORTS=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      EXTRA_ARGS+=("$@")
      break
      ;;
    *)
      EXTRA_ARGS+=("$1")
      shift
      ;;
  esac
done

if ! command -v bru >/dev/null 2>&1; then
  echo "Error: Bruno CLI ('bru') is not installed or not in PATH." >&2
  echo "Install Bruno CLI v4+: npm install -g @usebruno/cli" >&2
  exit 1
fi

BRU_ARGS=()
if [[ -n "${FOLDER}" ]]; then
  BRU_ARGS+=("${FOLDER}")
fi

BRU_ARGS+=("-r" "--env" "${ENV_NAME}")

if [[ -n "${BASE_URL}" ]]; then
  BRU_ARGS+=("--env-var" "baseUrl=${BASE_URL}")
fi

if [[ -n "${TAGS}" ]]; then
  BRU_ARGS+=("--tags" "${TAGS}")
fi

if [[ "${GENERATE_REPORTS}" == "true" ]]; then
  mkdir -p "${REPORTS_DIR}"
  BRU_ARGS+=(
    "--reporter-junit" "${REPORTS_DIR}/junit.xml"
    "--reporter-json" "${REPORTS_DIR}/results.json"
    "--reporter-html" "${REPORTS_DIR}/report.html"
  )
fi

if [[ ${#EXTRA_ARGS[@]} -gt 0 ]]; then
  BRU_ARGS+=("${EXTRA_ARGS[@]}")
fi

cd "${COLLECTION_DIR}"
exec bru run "${BRU_ARGS[@]}"
