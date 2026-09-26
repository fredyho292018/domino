# SERVER-3 architecture

```text
Unity -> future HTTPS/WSS -> Cloudflare (NOT CONFIGURED YET)
      -> future Tunnel -> 127.0.0.1:38080 -> API:8080
API -> private backend Docker network -> Redis (no host port)
API -> separate egress Docker network -> Firebase Auth / Firestore TEST
```

API: Java 21, Spring Boot 4.0.8, UID/GID 10001, 4 CPU / 4 GiB, Xms512m / Xmx2g.
Read-only root filesystem, no capabilities, no-new-privileges, bounded json-file logs 10m x 5.
Container /tmp is writable noexec tmpfs. Netty native loading can fall back; the SERVER-3 Firestore
probe passed with this configuration. Do not relax host mounts to suppress informational messages.

Redis: pinned 7.4 image, 1 GiB container budget, maxmemory 512 MiB, noeviction, AOF/RDB disabled.
Firestore remains durable authority. Redis has no host port and no egress network.

SPRING_BOOT_INSTANCE_COUNT=1
MULTI_INSTANCE_GAMEPLAY_SCALE_OUT=BLOCKED

Known distributed Match delivery issue prevents claiming multi-instance gameplay support.
RETENTION_CLEANUP_READY=NO
PUBLIC_PRODUCTION_READY=NO

These limits permit bounded TEST; they do not establish production readiness or capacity.
