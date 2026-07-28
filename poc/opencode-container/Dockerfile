# OpenCode Runner container — SLICE-STAB-002.
# Canonical image for the OpenCode Server referenced by the bridge.
# Hardening profile per docs/discovery/009-security-discovery.md:
#   - non-root UID 10001 (SH-FR-001)
#   - cap_drop ALL + no-new-privileges set in compose
#   - read_only rootfs via compose (not enforced here; the runtime
#     layer is expected to keep /workspace as the only writable bind)
#   - pids / cpus / memory limits defined in the parent compose service.
#
# Pinned version per docs/adr/0017-pin-opencode-version.md (ADR-0017).
# Installs the OpenCode binary by downloading the musl asset for the
# pinned release from the official GitHub release with SHA-256
# verification. Fails fast on any error; no silent fallbacks, no
# bind-mount of the host binary.

FROM node:20-alpine

# Install bash (busybox ash does not support `-o pipefail`),
# curl (for downloading the OpenCode asset; not present in node:20-alpine
# by default — only wget from busybox is available) and ca-certificates
# (required for HTTPS to github.com) before declaring the SHELL directive
# that uses bash with pipefail. Order matters: bash and curl must already
# be present in the layer before the SHELL directive is consumed by
# subsequent RUN steps.
RUN apk add --no-cache ca-certificates bash curl

SHELL ["/bin/bash", "-o", "pipefail", "-c"]

# Pinned per ADR-0017. OPENCODE_VERSION is the GitHub release tag;
# OPENCODE_ASSET is the musl variant required for Alpine; OPENCODE_SHA256
# is the SHA-256 of the asset published by the upstream release.
ARG OPENCODE_VERSION=v1.18.8
ARG OPENCODE_ASSET=opencode-linux-x64-musl.tar.gz
ARG OPENCODE_SHA256=7e7a991aff33ae330308e88bfa8e6a5ea4125b468f4de6657b93d76200897a41

RUN set -eux; \
    curl -fsSL \
      "https://github.com/anomalyco/opencode/releases/download/${OPENCODE_VERSION}/${OPENCODE_ASSET}" \
      -o /tmp/opencode.tar.gz; \
    echo "${OPENCODE_SHA256}  /tmp/opencode.tar.gz" | sha256sum -c -; \
    mkdir -p /usr/local/bin; \
    tar -xzf /tmp/opencode.tar.gz -C /usr/local/bin opencode; \
    chmod +x /usr/local/bin/opencode; \
    rm /tmp/opencode.tar.gz; \
    command -v opencode; \
    opencode --version

RUN addgroup -S -g 10001 ocab && \
    adduser -S -u 10001 -G ocab -h /home/ocab -s /sbin/nologin ocab

USER ocab
WORKDIR /home/ocab

ENV OPENCODE_HOST=0.0.0.0
ENV OPENCODE_PORT=4096
ENV OPENCODE_LOG_LEVEL=INFO

WORKDIR /workspace
EXPOSE 4096

# Liveness check — hits the official /global/health endpoint, not
# /health (which falls into the SPA fallback of the embedded web UI).
HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:4096/global/health || exit 1

ENTRYPOINT ["/usr/local/bin/opencode"]
CMD ["serve", "--hostname", "0.0.0.0", "--port", "4096"]
