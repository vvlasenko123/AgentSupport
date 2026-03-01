FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . ./

RUN dotnet restore backend/Apps/SmtpConnector/SmtpConnector.sln

RUN dotnet publish backend/Apps/SmtpConnector/Api/SmtpConnector.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    gnupg \
    python3 \
    python3-pip \
    bash \
    libmagic1 \
    file \
    jq \
    tar \
    gzip \
    librdkafka1 \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

ENV PATH=$PATH:/google-cloud-sdk/bin

RUN mkdir -p /google-cloud-sdk && \
    curl -O https://dl.google.com/dl/cloudsdk/channels/rapid/downloads/google-cloud-cli-485.0.0-linux-x86_64.tar.gz && \
    tar -xzf google-cloud-cli-485.0.0-linux-x86_64.tar.gz -C /google-cloud-sdk --strip-components=1 && \
    rm google-cloud-cli-485.0.0-linux-x86_64.tar.gz && \
    /google-cloud-sdk/install.sh --quiet --usage-reporting=false

COPY --from=build /app/publish ./

RUN groupadd -g 10001 appuser && \
    useradd -u 10001 -g 10001 -m -d /home/appuser -s /usr/sbin/nologin appuser && \
    mkdir -p /home/appuser/.config && \
    chown -R appuser:appuser /home/appuser && \
    chown -R appuser:appuser /app

RUN cat > /app/entrypoint.sh << 'EOF'
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

RUN chmod +x /app/entrypoint.sh && \
    chown appuser:appuser /app/entrypoint.sh

USER appuser
ENTRYPOINT ["/app/entrypoint.sh"]