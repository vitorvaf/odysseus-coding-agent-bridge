# OpenCode Runner container — slice 1.1.3.
# Canonical image for the OpenCode Server referenced by the bridge.
# Hardening profile per docs/discovery/009-security-discovery.md:
#   - non-root UID 10001 (SH-FR-001)
#   - cap_drop ALL + no-new-privileges set in compose
#   - read_only rootfs via compose (not enforced here; the runtime
#     layer is expected to keep /workspace as the only writable bind)
#   - pids / cpus / memory limits defined in the parent compose service.
#
# The official OpenCode installer downloads a Go binary from
# https://opencode.ai/install at build time. If outbound internet is
# unavailable in the build context, the build emits a warning and the
# entrypoint remains the OpenCode binary — build/runtime owners can
# mount the host-installed binary instead.

FROM node:20-alpine

RUN addgroup -S -g 10001 ocab && \
    adduser -S -u 10001 -G ocab -h /home/ocab -s /sbin/nologin ocab

USER ocab
WORKDIR /home/ocab

# Best-effort install. If this fails (offline build context), the
# compose layer can either retry later or bind-mount a host OpenCode
# binary into /usr/local/bin/opencode at runtime.
RUN curl -fsSL https://opencode.ai/install -o /tmp/ocab-opencode-install.sh && \
    sh /tmp/ocab-opencode-install.sh 2>&1 | tee /tmp/ocab-install.log \
      || echo "WARN: opencode.ai install failed; bind-mount OpenCode binary at runtime via compose"

ENV OPENCODE_HOST=0.0.0.0
ENV OPENCODE_PORT=4096

WORKDIR /workspace
EXPOSE 4096

# Liveness check — hits /health on the OpenCode Server.
HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:4096/health || exit 1

ENTRYPOINT ["/usr/local/bin/opencode"]
CMD ["serve", "--hostname", "0.0.0.0", "--port", "4096"]
