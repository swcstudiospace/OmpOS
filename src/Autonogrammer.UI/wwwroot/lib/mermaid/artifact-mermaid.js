// Host script for the vendored Mermaid bundle (mermaid.min.js, same folder). It runs only in a frame that holds one <pre class="mermaid">
// with the diagram source. It is loaded with <script src> beside the bundle, never inline: a srcdoc frame inherits the page's
// script-src 'self', which forbids inline script. The keys below are fixed; the diagram source is never interpolated here, and a
// directive inside the source cannot raise the secure keys (securityLevel, maxTextSize, maxEdges, startOnLoad).
(function () {
  "use strict";
  var host = document.querySelector("pre.mermaid");
  if (!host || typeof mermaid === "undefined") {
    return;
  }
  mermaid.initialize({
    securityLevel: "strict",
    startOnLoad: false,
    maxTextSize: 50000,
    maxEdges: 500,
    htmlLabels: false,
    deterministicIds: true,
    deterministicIDSeed: "autonogrammer-artifact",
  });
  mermaid.run({ nodes: [host] }).catch(function () {
    // The source stays visible in the frame when the diagram cannot be drawn; nothing else is reported from inside the frame.
    host.setAttribute("data-render", "failed");
  });
})();
