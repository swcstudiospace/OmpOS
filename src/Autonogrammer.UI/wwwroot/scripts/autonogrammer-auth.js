// Classic boot script (no export). index.html loads it before blazor.webassembly.js so a sign-in callback is
// scrubbed before the router renders. Holds nothing across reloads: no browser storage API is used.
// The asking tab binds through autonogrammerAuth.bindAskingTab and exchanges the code after it acknowledges.
(function () {
  var channelName = "autonogrammer-auth";
  var waitMs = 3000;
  var otherTab = "You are signed in in your other tab.";
  var wrongTab = "That link opened in a different tab. Use the code from the email.";
  var status = "none";
  var message = null;
  var scrubbedFirst = false;
  var asking = null;
  var channel = null;

  function currentUrl() {
    return new URL(window.location.href);
  }

  function readCallback(url) {
    var params = url.searchParams;
    return {
      code: params.get("code"),
      error: params.get("error"),
      errorCode: params.get("error_code"),
    };
  }

  function scrub(url) {
    url.searchParams.delete("code");
    url.searchParams.delete("error");
    url.searchParams.delete("error_code");
    url.searchParams.delete("error_description");
    var next = url.pathname + url.search + url.hash;
    window.history.replaceState(window.history.state, "", next);
    scrubbedFirst = true;
  }

  function post(body) {
    if (channel) {
      channel.postMessage(body);
    }
  }

  function finish(kind, text) {
    status = kind;
    message = text;
  }

  try {
    channel = new BroadcastChannel(channelName);
    channel.onmessage = function (event) {
      var data = event && event.data;
      if (!data || typeof data.type !== "string") {
        return;
      }

      if (data.type === "ack" && status === "pending") {
        finish("acknowledged", otherTab);
        return;
      }

      if (data.type === "code" && typeof data.code === "string" && status !== "pending") {
        post({ type: "ack" });
        if (asking && typeof asking.invokeMethodAsync === "function") {
          asking.invokeMethodAsync("ReceiveCode", data.code);
        }
      }
    };
  } catch (fault) {
    finish("error", wrongTab);
  }

  var url = currentUrl();
  var callback = readCallback(url);
  if (callback.code || callback.error || callback.errorCode) {
    scrub(url);
    if (callback.error || callback.errorCode || !callback.code) {
      finish("error", wrongTab);
    } else if (channel) {
      status = "pending";
      post({ type: "code", code: callback.code });
      window.setTimeout(function () {
        if (status === "pending") {
          finish("unacknowledged", wrongTab);
        }
      }, waitMs);
    } else {
      finish("error", wrongTab);
    }
  }

  var api = {
    channelName: channelName,
    acknowledgementTimeoutMs: waitMs,
    otherTabSentence: otherTab,
    wrongTabSentence: wrongTab,
    outcome: function () {
      return status;
    },
    message: function () {
      return message;
    },
    scrubbedBeforePost: function () {
      return scrubbedFirst;
    },
    bindAskingTab: function (dotNet) {
      asking = dotNet;
    },
    postCode: function (code) {
      post({ type: "code", code: code });
    },
    acknowledge: function () {
      post({ type: "ack" });
    },
  };
  Object.freeze(api);
  Object.defineProperty(window, "autonogrammerAuth", {
    value: api,
    writable: false,
    configurable: false,
  });
})();
