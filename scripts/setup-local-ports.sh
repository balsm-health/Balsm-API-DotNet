#!/usr/bin/env bash
# Transparently forward the standard web ports to the Balsm dev ports so the
# admin panel is reachable WITHOUT a port in the URL:
#
#   http://balsm.local/admin    ->  127.0.0.1:5050
#   https://balsm.local/admin   ->  127.0.0.1:5051
#
# This is a KERNEL redirect (pf on macOS, nftables/iptables on Linux). The app
# keeps listening on 5050/5051 — nothing binds 80/443, the URL is not rewritten,
# the browser just talks to :80/:443 and the kernel swaps the destination port.
#
# Requires sudo (editing the firewall NAT table). Reversible:  setup-local-ports.sh down
#
# Usage:
#   sudo bash scripts/setup-local-ports.sh up      # install (default)
#   sudo bash scripts/setup-local-ports.sh down    # remove
#   sudo bash scripts/setup-local-ports.sh status
#
# Override ports:  HTTP_PORT=5000 HTTPS_PORT=5001 sudo bash scripts/setup-local-ports.sh up
set -euo pipefail

HTTP_PORT="${HTTP_PORT:-5050}"
HTTPS_PORT="${HTTPS_PORT:-5051}"
ACTION="${1:-up}"
ANCHOR="balsm.local"
PF_ANCHOR_FILE="/etc/pf.anchors/${ANCHOR}"

require_root() {
  if [ "$(id -u)" -ne 0 ]; then
    echo "Needs root. Re-run: sudo bash scripts/setup-local-ports.sh ${ACTION}" >&2
    exit 1
  fi
}

# ── macOS (pf) ─────────────────────────────────────────────────────────────────
mac_up() {
  require_root
  cat > "$PF_ANCHOR_FILE" <<EOF
rdr pass inet proto tcp from any to any port 80  -> 127.0.0.1 port ${HTTP_PORT}
rdr pass inet proto tcp from any to any port 443 -> 127.0.0.1 port ${HTTPS_PORT}
EOF

  # Reference our anchor from the main pf.conf (idempotent) so its rdr rules are
  # evaluated, without disturbing Apple's existing anchors.
  if ! grep -q "rdr-anchor \"${ANCHOR}\"" /etc/pf.conf; then
    cp /etc/pf.conf "/etc/pf.conf.balsm.bak.$(date +%s)"
    # rdr-anchor lines must precede filter rules; insert after the last existing
    # rdr-anchor (Apple ships one near the top).
    awk -v a="${ANCHOR}" '
      !done && /^rdr-anchor/ { print; print "rdr-anchor \"" a "\""; print "load anchor \"" a "\" from \"/etc/pf.anchors/" a "\""; done=1; next }
      { print }
      END { if (!done) { print "rdr-anchor \"" a "\""; print "load anchor \"" a "\" from \"/etc/pf.anchors/" a "\"" } }
    ' /etc/pf.conf > /etc/pf.conf.balsm.tmp && mv /etc/pf.conf.balsm.tmp /etc/pf.conf
  fi

  pfctl -f /etc/pf.conf >/dev/null 2>&1 || true
  pfctl -E >/dev/null 2>&1 || true
  echo "Port redirect active (pf):  :80 -> :${HTTP_PORT}   :443 -> :${HTTPS_PORT}"
  echo "Open: http://balsm.local/admin"
}

mac_down() {
  require_root
  rm -f "$PF_ANCHOR_FILE"
  if grep -q "rdr-anchor \"${ANCHOR}\"" /etc/pf.conf; then
    grep -v "\"${ANCHOR}\"" /etc/pf.conf > /etc/pf.conf.balsm.tmp && mv /etc/pf.conf.balsm.tmp /etc/pf.conf
    pfctl -f /etc/pf.conf >/dev/null 2>&1 || true
  fi
  echo "Port redirect removed (pf)."
}

mac_status() {
  echo "pf anchor ${ANCHOR}:"
  pfctl -a "${ANCHOR}" -s nat 2>/dev/null || echo "  (none)"
}

# ── Linux (nftables, fallback iptables) ────────────────────────────────────────
linux_up() {
  require_root
  if command -v nft >/dev/null 2>&1; then
    nft list table ip balsm_local >/dev/null 2>&1 && nft delete table ip balsm_local
    nft -f - <<EOF
table ip balsm_local {
  chain prerouting { type nat hook prerouting priority dstnat; policy accept;
    tcp dport 80  redirect to :${HTTP_PORT}
    tcp dport 443 redirect to :${HTTPS_PORT}
  }
  chain output { type nat hook output priority -100; policy accept;
    ip daddr 127.0.0.1 tcp dport 80  redirect to :${HTTP_PORT}
    ip daddr 127.0.0.1 tcp dport 443 redirect to :${HTTPS_PORT}
  }
}
EOF
    echo "Port redirect active (nftables)."
  else
    iptables -t nat -A PREROUTING -p tcp --dport 80  -j REDIRECT --to-port "${HTTP_PORT}"
    iptables -t nat -A PREROUTING -p tcp --dport 443 -j REDIRECT --to-port "${HTTPS_PORT}"
    iptables -t nat -A OUTPUT -o lo -p tcp --dport 80  -j REDIRECT --to-port "${HTTP_PORT}"
    iptables -t nat -A OUTPUT -o lo -p tcp --dport 443 -j REDIRECT --to-port "${HTTPS_PORT}"
    echo "Port redirect active (iptables). Note: not persistent across reboot."
  fi
  echo "Open: http://balsm.local/admin"
}

linux_down() {
  require_root
  if command -v nft >/dev/null 2>&1 && nft list table ip balsm_local >/dev/null 2>&1; then
    nft delete table ip balsm_local
    echo "Port redirect removed (nftables)."
  else
    iptables -t nat -D PREROUTING -p tcp --dport 80  -j REDIRECT --to-port "${HTTP_PORT}" 2>/dev/null || true
    iptables -t nat -D PREROUTING -p tcp --dport 443 -j REDIRECT --to-port "${HTTPS_PORT}" 2>/dev/null || true
    iptables -t nat -D OUTPUT -o lo -p tcp --dport 80  -j REDIRECT --to-port "${HTTP_PORT}" 2>/dev/null || true
    iptables -t nat -D OUTPUT -o lo -p tcp --dport 443 -j REDIRECT --to-port "${HTTPS_PORT}" 2>/dev/null || true
    echo "Port redirect removed (iptables)."
  fi
}

case "$(uname -s)" in
  Darwin) case "$ACTION" in up) mac_up ;; down) mac_down ;; status) mac_status ;; *) echo "up|down|status" >&2; exit 1 ;; esac ;;
  Linux)  case "$ACTION" in up) linux_up ;; down) linux_down ;; status) iptables -t nat -L -n 2>/dev/null | grep -i redirect || nft list table ip balsm_local 2>/dev/null || echo "(none)" ;; *) echo "up|down|status" >&2; exit 1 ;; esac ;;
  *) echo "Unsupported OS: $(uname -s)" >&2; exit 1 ;;
esac
