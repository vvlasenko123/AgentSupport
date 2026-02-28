FROM mcr.microsoft.com/dotnet/sdk:10.0-bookworm-slim AS build
WORKDIR /src

COPY SmtpConnector.sln ./
COPY Api/SmtpConnector.Api.csproj Api/
COPY Dal/SmtpConnector.Dal.csproj Dal/
COPY Logic/SmtpConnector.Logic.csproj Logic/

RUN dotnet restore SmtpConnector.sln

COPY . ./
RUN dotnet publish Api/SmtpConnector.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-bookworm-slim AS runtime
WORKDIR /app

ENV CLOUDSDK_CORE_DISABLE_PROMPTS=1

RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        gnupg \
        apt-transport-https \
        python3; \
    mkdir -p /usr/share/keyrings; \
    curl -fsSL https://packages.cloud.google.com/apt/doc/apt-key.gpg | gpg --dearmor -o /usr/share/keyrings/cloud.google.gpg; \
    echo "deb [signed-by=/usr/share/keyrings/cloud.google.gpg] https://packages.cloud.google.com/apt cloud-sdk main" > /etc/apt/sources.list.d/google-cloud-sdk.list; \
    apt-get update; \
    if apt-cache show google-cloud-cli >/dev/null 2>&1; then \
        apt-get install -y --no-install-recommends google-cloud-cli; \
    else \
        apt-get install -y --no-install-recommends google-cloud-sdk; \
    fi; \
    apt-get clean; \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

RUN set -eux; \
    useradd -m -u 10001 appuser; \
    mkdir -p /app/secrets; \
    chown -R appuser:appuser /app; \
    mkdir -p /home/appuser/.config; \
    chown -R appuser:appuser /home/appuser

RUN set -eux; \
    cat > /app/entrypoint.sh << 'EOF'
#!/usr/bin/env bash
set -euo pipefail

KEY_FILE="${GOOGLE_APPLICATION_CREDENTIALS:-}"

if [ -z "${KEY_FILE}" ] && [ -n "${PubSubOptions__ServiceAccountJsonPath:-}" ]; then
  KEY_FILE="${PubSubOptions__ServiceAccountJsonPath}"
fi

if [ -n "${KEY_FILE}" ] && [ -f "${KEY_FILE}" ]; then
  export CLOUDSDK_CONFIG="${CLOUDSDK_CONFIG:-/home/appuser/.config/gcloud}"
  gcloud auth activate-service-account --key-file="${KEY_FILE}" --quiet || true

  if [ -n "${GOOGLE_CLOUD_PROJECT:-}" ]; then
    gcloud config set project "${GOOGLE_CLOUD_PROJECT}" --quiet || true
  fi
fi

exec dotnet SmtpConnector.Api.dll
EOF
    chmod +x /app/entrypoint.sh; \
    chown appuser:appuser /app/entrypoint.sh

USER appuser

ENTRYPOINT ["/app/entrypoint.sh"]