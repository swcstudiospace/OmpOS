import { execFileSync } from "node:child_process";
import path from "node:path";

// Build-time configuration for the hosted web head (Q-VERCEL-CSP). vercel.ts executes at build time,
// so connect-src is generated from one validated API origin. scripts/vercel/render-csp.sh fails closed
// on a missing or invalid OMPOS_API_ORIGIN; that aborts config evaluation, so the deploy fails rather
// than shipping a wildcard or a stale origin.
const connectSrc = execFileSync("bash", [path.join(process.cwd(), "scripts/vercel/render-csp.sh")], {
  encoding: "utf8",
  stdio: ["ignore", "pipe", "inherit"],
  env: process.env,
}).trim();

const contentSecurityPolicy = [
  "default-src 'self'",
  "script-src 'self' 'wasm-unsafe-eval'",
  "style-src 'self' 'unsafe-inline'",
  connectSrc,
  "img-src 'self' data:",
  "object-src 'none'",
  "base-uri 'self'",
  "frame-ancestors 'none'",
].join("; ");

export const config = {
  framework: null,
  installCommand:
    "curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh && chmod +x /tmp/dotnet-install.sh && /tmp/dotnet-install.sh --jsonfile global.json --install-dir ./.dotnet",
  buildCommand: "./.dotnet/dotnet publish apps/OmpOS.Web -c Release -o output",
  outputDirectory: "output/wwwroot",
  env: {
    DOTNET_CLI_TELEMETRY_OPTOUT: "1",
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT: "1",
  },
  rewrites: [{ source: "/(.*)", destination: "/index.html" }],
  headers: [
    {
      source: "/_framework/(.*)",
      headers: [{ key: "Cache-Control", value: "public, max-age=31536000, immutable" }],
    },
    {
      source: "/(.*)",
      headers: [
        { key: "Content-Security-Policy", value: contentSecurityPolicy },
        { key: "X-Content-Type-Options", value: "nosniff" },
        { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
        { key: "X-Frame-Options", value: "DENY" },
      ],
    },
  ],
};
